using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace gishadev.tools.Editor.SfxPrep
{
    /// <summary>
    /// Runs <c>sfx_prep.py</c> (in this package's <c>Tools~/SfxPrep</c>) in the background and collects its progress lines
    /// (<c>[3/42] pig-idle: 14 clips</c>). Call <see cref="Poll"/> from the editor update loop.
    /// </summary>
    public class SfxPrepRunner
    {
        private static readonly Regex ProgressLine = new(@"^\[(\d+)/(\d+)\]");

        private readonly ConcurrentQueue<string> _output = new();
        private readonly ConcurrentQueue<string> _errors = new();
        private Process _process;

        public bool IsRunning => _process != null;
        public int Done { get; private set; }
        public int Total { get; private set; }
        public string LastLine { get; private set; } = "";
        public List<string> ErrorLines { get; } = new();

        /// <summary>Fires on the main thread when the script exits; true when it succeeded.</summary>
        public event Action<bool> Finished;

        public void Start(string python, string script, string projectRoot, string arguments, int expectedTotal)
        {
            Done = 0;
            Total = expectedTotal;
            LastLine = "Starting Python...";
            ErrorLines.Clear();

            var info = new ProcessStartInfo(python, $"-I -u \"{script}\" {arguments}")
            {
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            _process = new Process { StartInfo = info, EnableRaisingEvents = true };
            _process.OutputDataReceived += (_, e) => { if (e.Data != null) _output.Enqueue(e.Data); };
            _process.ErrorDataReceived += (_, e) => { if (e.Data != null) _errors.Enqueue(e.Data); };

            try
            {
                _process.Start();
            }
            catch (Exception e)
            {
                _process = null;
                ErrorLines.Add($"Couldn't start '{python}': {e.Message}. Set the Python path in the window's settings.");
                Finished?.Invoke(false);
                return;
            }

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        public void Cancel()
        {
            if (_process == null)
                return;

            try
            {
                if (!_process.HasExited)
                    _process.Kill();
            }
            catch (InvalidOperationException)
            {
                // Already gone.
            }

            _process.Dispose();
            _process = null;
            LastLine = "Cancelled.";
        }

        /// <summary>Drains output; returns true when something changed (repaint).</summary>
        public bool Poll()
        {
            if (_process == null)
                return false;

            var changed = Drain();
            if (!_process.HasExited)
                return changed;

            _process.WaitForExit(); // flushes the async readers
            Drain();
            var ok = _process.ExitCode == 0;
            _process.Dispose();
            _process = null;
            Finished?.Invoke(ok);
            return true;
        }

        private bool Drain()
        {
            var changed = false;
            while (_output.TryDequeue(out var line))
            {
                LastLine = line;
                var match = ProgressLine.Match(line);
                if (match.Success)
                {
                    Done = int.Parse(match.Groups[1].Value);
                    Total = int.Parse(match.Groups[2].Value);
                }

                changed = true;
            }

            while (_errors.TryDequeue(out var line))
            {
                ErrorLines.Add(line);
                changed = true;
            }

            return changed;
        }
    }
}
