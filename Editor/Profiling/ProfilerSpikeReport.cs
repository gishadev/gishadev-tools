using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace gishadev.tools.Editor
{
    /// <summary>
    /// Reads a saved Profiler capture (.data) and writes a plain-text report of what runs in the spike frames
    /// (main thread), next to the capture as <c>&lt;name&gt;.report.txt</c>. Spike = frame time above
    /// 2x the median and at least 8 ms over it.
    /// </summary>
    public static class ProfilerSpikeReport
    {
        private const int MainThread = 0;
        private const int SpikeDetailFrames = 12;
        private const float HotPathShare = 0.15f;

        [MenuItem("Tools/gishadev/Analyze Profiler Capture...")]
        private static void Analyze()
        {
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "ProfilerCaptures");
            string path = EditorUtility.OpenFilePanel("Profiler capture", folder, "data");
            if (string.IsNullOrEmpty(path))
                return;

            if (!ProfilerDriver.LoadProfile(path, false))
            {
                Debug.LogError($"Profiler report: couldn't load {path}.");
                return;
            }

            try
            {
                string report = BuildReport(path);
                string reportPath = Path.ChangeExtension(path, ".report.txt");
                File.WriteAllText(reportPath, report);
                Debug.Log($"Profiler report written: {reportPath}");
                EditorUtility.RevealInFinder(reportPath);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static string BuildReport(string path)
        {
            int first = ProfilerDriver.firstFrameIndex;
            int last = ProfilerDriver.lastFrameIndex;

            var frameTimes = new Dictionary<int, float>();
            for (int frame = first; frame <= last; frame++)
            {
                using var view = GetView(frame);
                if (view != null && view.valid)
                    frameTimes[frame] = view.frameTimeMs;
            }

            float median = frameTimes.Values.OrderBy(t => t).ElementAt(frameTimes.Count / 2);
            var spikes = frameTimes.Where(f => f.Value > Mathf.Max(median * 2f, median + 8f))
                .Select(f => f.Key).ToList();
            var normal = frameTimes.Keys.Except(spikes).ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"Capture: {Path.GetFileName(path)}");
            sb.AppendLine($"Frames {first}..{last} ({frameTimes.Count}), median {median:F2} ms, " +
                          $"spikes {spikes.Count} (> {Mathf.Max(median * 2f, median + 8f):F1} ms)");
            sb.AppendLine();

            // Which markers take much more self time in spike frames than in normal frames.
            var spikeSelf = AverageSelfTimes(spikes, "Spike frames");
            var normalSelf = AverageSelfTimes(normal.Where((_, i) => i % 4 == 0).ToList(), "Normal frames");

            sb.AppendLine("=== Markers that grow the most in spike frames (avg self ms: spike vs normal) ===");
            foreach (var entry in spikeSelf
                         .Select(kv => (name: kv.Key, spike: kv.Value,
                             normal: normalSelf.TryGetValue(kv.Key, out var n) ? n : 0f))
                         .OrderByDescending(x => x.spike - x.normal).Take(30))
                sb.AppendLine($"{entry.spike - entry.normal,8:F2} ms  (spike {entry.spike,7:F2} / normal {entry.normal,6:F2})  {entry.name}");
            sb.AppendLine();

            // The worst spikes in detail: the hot path down the tree and the top self-time markers.
            sb.AppendLine($"=== {SpikeDetailFrames} worst spike frames ===");
            foreach (int frame in spikes.OrderByDescending(f => frameTimes[f]).Take(SpikeDetailFrames))
            {
                using var view = GetView(frame);
                if (view == null || !view.valid)
                    continue;

                sb.AppendLine($"--- Frame {frame}: {view.frameTimeMs:F1} ms, GC {GcBytes(view, view.GetRootItemID())} B");
                AppendHotPath(sb, view, view.GetRootItemID(), view.frameTimeMs, 1);

                var self = new Dictionary<string, float>();
                CollectSelf(view, view.GetRootItemID(), self);
                sb.AppendLine("  top self time:");
                foreach (var kv in self.OrderByDescending(kv => kv.Value).Take(8))
                    sb.AppendLine($"    {kv.Value,7:F2} ms  {kv.Key}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static Dictionary<string, float> AverageSelfTimes(List<int> frames, string label)
        {
            var totals = new Dictionary<string, float>();
            for (int i = 0; i < frames.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Profiler report", label, (float)i / Mathf.Max(1, frames.Count));
                using var view = GetView(frames[i]);
                if (view == null || !view.valid)
                    continue;

                CollectSelf(view, view.GetRootItemID(), totals);
            }

            return totals.ToDictionary(kv => kv.Key, kv => kv.Value / Mathf.Max(1, frames.Count));
        }

        private static void CollectSelf(HierarchyFrameDataView view, int item, Dictionary<string, float> totals)
        {
            var children = new List<int>();
            view.GetItemChildren(item, children);
            foreach (int child in children)
            {
                string name = view.GetItemName(child);
                float self = view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnSelfTime);
                totals[name] = (totals.TryGetValue(name, out var t) ? t : 0f) + self;
                CollectSelf(view, child, totals);
            }
        }

        /// <summary>Follows every child that takes a big share of the frame, so the expensive branch is visible.</summary>
        private static void AppendHotPath(StringBuilder sb, HierarchyFrameDataView view, int item, float frameMs, int depth)
        {
            if (depth > 14)
                return;

            var children = new List<int>();
            view.GetItemChildren(item, children);
            foreach (int child in children
                         .OrderByDescending(c => view.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnTotalTime)))
            {
                float total = view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnTotalTime);
                if (total < frameMs * HotPathShare)
                    break;

                float self = view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnSelfTime);
                sb.AppendLine($"{new string(' ', depth * 2)}{total,7:F2} ms (self {self:F2}, calls " +
                              $"{view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnCalls):F0}, " +
                              $"GC {GcBytes(view, child)} B)  {view.GetItemName(child)}");
                AppendHotPath(sb, view, child, frameMs, depth + 1);
            }
        }

        private static long GcBytes(HierarchyFrameDataView view, int item) =>
            (long)view.GetItemColumnDataAsFloat(item, HierarchyFrameDataView.columnGcMemory);

        private static HierarchyFrameDataView GetView(int frame) =>
            ProfilerDriver.GetHierarchyFrameDataView(frame, MainThread,
                HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                HierarchyFrameDataView.columnTotalTime, false);
    }
}
