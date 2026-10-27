using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace gishadev.tools.Editor.SfxPrep
{
    /// <summary>
    /// The game's sound list and the tool that turns its picks into game clips, without leaving Unity.
    /// The list (the project's sound_list.json, see <see cref="SfxPrepSettings"/>) is edited here: sounds, their
    /// categories and settings. Candidate files come from the sfx-scout skill and from audio files you drop on the
    /// window from anywhere (yours first). <b>Process</b> runs this package's sfx_prep.py, which splits takes, cuts loops and
    /// evens out loudness into the cache folder. Here you listen, tick the clips to keep, and <b>Export</b> copies them
    /// to &lt;audio root&gt;/&lt;Category&gt;/&lt;sound&gt;/ with their import settings (load type by length, Vorbis
    /// quality, mono). Wiring them into AudioMasterSO or the SFX pool stays manual.
    /// </summary>
    public class SfxPrepWindow : EditorWindow
    {

        private static readonly string[] Types = { "One-shot", "Loop" };
        // sfx_prep.py reads WAV itself and the rest through the soundfile package.
        private static readonly string[] AudioExtensions = { ".wav", ".ogg", ".mp3", ".flac", ".aif", ".aiff" };
        private static readonly string[] ChannelOptions = { "Mono", "Stereo" };
        private static readonly Regex IdPattern = new("^[a-z0-9]+(-[a-z0-9]+)*$");

        private static readonly Color KeptRowColor = new(0.35f, 0.65f, 0.3f, 0.18f);
        private static readonly Color SelectedRowColor = new(0.25f, 0.5f, 0.9f, 0.25f);
        private static readonly Color ShortColor = new(0.95f, 0.7f, 0.2f);

        private readonly SfxPrepRunner _runner = new();
        private readonly Dictionary<string, AudioClip> _previewCache = new();
        private readonly Dictionary<string, int> _exportedCount = new();
        private readonly HashSet<string> _stale = new();

        private SfxSoundList _list;
        private SfxPrepManifest _manifest;
        private SfxPrepSelection _selection;
        private string _selectedId;
        private string _runningLabel;
        private bool _showSettings;
        private bool _showCategories;
        private bool _showNewSound;
        private bool _showOverrides;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private Vector2 _categoryScroll;

        private string _newId = "";
        private int _newCategory;
        private int _newType;
        private string _newCategoryId = "";

        private GameObject _previewObject;
        private AudioSource _previewSource;
        private string _playingFile;

        private static string ProjectRoot => SfxPrepSettings.ProjectRoot;
        private static string AudioRoot => SfxPrepSettings.AudioRoot;
        private static string CacheDir => SfxPrepSettings.CacheDir;
        private static string ManifestFile => SfxPrepSettings.ManifestFile;
        private static string SelectionFile => SfxPrepSettings.SelectionFile;
        private static string ConfigFile => SfxPrepSettings.ConfigPath;
        private static string SoundListFile => SfxPrepSettings.SoundListPath;

        [MenuItem("Tools/gishadev/SFX Prep")]
        private static void Open() => GetWindow<SfxPrepWindow>("SFX Prep");

        private void OnEnable()
        {
            minSize = new Vector2(820, 400);
            _runner.Finished += OnRunFinished;
            EditorApplication.update += OnEditorUpdate;
            Reload();
        }

        private void OnDisable()
        {
            _runner.Finished -= OnRunFinished;
            EditorApplication.update -= OnEditorUpdate;
            _runner.Cancel();
            StopPreview();
            ClearPreviewCache();
            if (_previewObject != null)
                DestroyImmediate(_previewObject);
        }

        // Clips may have been moved or deleted in the Project window meanwhile.
        private void OnFocus() => CountExported();

        private void OnEditorUpdate()
        {
            var repaint = _runner.Poll();
            if (_playingFile != null && (_previewSource == null || !_previewSource.isPlaying))
            {
                _playingFile = null;
                repaint = true;
            }

            if (repaint)
                Repaint();
        }

        // ------------------------------------------------------------------ data

        private void Reload()
        {
            StopPreview();
            ClearPreviewCache();
            SfxPrepSettings.Reload();
            _list = SfxSoundList.Load(SoundListFile);
            _manifest = SfxPrepManifest.Load(ManifestFile);
            _selection = _manifest != null ? SfxPrepSelection.LoadAndReconcile(SelectionFile, _manifest) : null;
            _selection?.Save(SelectionFile);
            if (_list != null && _list.Sound(_selectedId) == null)
                _selectedId = _list.Ordered().FirstOrDefault()?.id;
            CountExported();
        }

        private void SaveList() => _list.Save(SoundListFile);

        private SfxSoundDef Selected => _list?.Sound(_selectedId);

        private SfxPrepSound Processed(string id) => _manifest?.sounds.FirstOrDefault(s => s.id == id);

        private string ExportDir(SfxSoundDef sound) =>
            $"{AudioRoot}/{_list.Category(sound.category)?.folder ?? sound.category}/{sound.id}";

        private string ExportName(SfxSoundDef sound)
        {
            var prefix = _list.Category(sound.category)?.prefix ?? "sfx";
            var id = sound.id.StartsWith(prefix + "-") ? sound.id.Substring(prefix.Length + 1) : sound.id;
            return $"{prefix}_{id.Replace('-', '_')}";
        }

        private void CountExported()
        {
            _exportedCount.Clear();
            if (_list == null)
                return;

            foreach (var sound in _list.sounds)
            {
                var dir = ExportDir(sound);
                _exportedCount[sound.id] = Directory.Exists(dir)
                    ? Directory.GetFiles(dir, ExportName(sound) + "_*.wav").Length
                    : 0;
            }
        }

        private int KeptCount(SfxSoundDef sound) => _selection?.Get(sound.id)?.keep.Count ?? 0;

        // ------------------------------------------------------------------ processing

        private void Process(SfxSoundDef only)
        {
            if (only != null)
            {
                Run($"Processing {only.id}", $"process --sound {only.id}", 1);
                _stale.Remove(only.id);
                return;
            }

            Run("Processing all sounds", "process", _list.sounds.Count);
            _stale.Clear();
        }

        private void Run(string label, string arguments, int total)
        {
            StopPreview();
            ClearPreviewCache();
            _runningLabel = label;
            var script = SfxPrepSettings.ScriptPath;
            if (script == null || !File.Exists(script))
            {
                Debug.LogError(script == null
                    ? "SFX Prep: couldn't find the com.gishadev.tools package folder, so sfx_prep.py is missing."
                    : $"SFX Prep: sfx_prep.py not found at {script}.");
                return;
            }

            // The script reads the folder and the library from the same settings files as the window.
            _runner.Start(SfxPrepSettings.Python, script, ProjectRoot, $"{arguments} --project-root \"{ProjectRoot}\"", total);
        }

        private void OnRunFinished(bool ok)
        {
            if (!ok)
            {
                var errors = string.Join("\n", _runner.ErrorLines.TakeLast(12));
                Debug.LogError($"SFX Prep: processing failed.\n{errors}");
                EditorUtility.DisplayDialog("SFX Prep", "Processing failed:\n\n" + errors, "OK");
            }

            Reload();
        }

        // ------------------------------------------------------------------ candidates

        /// <summary>Your own files become candidates of the sound (ahead of the scout's picks) and it reprocesses.</summary>
        private void AddPicks(SfxSoundDef sound, IEnumerable<string> files)
        {
            var added = 0;
            foreach (var file in files)
            {
                var path = StoredPath(file);
                sound.excluded.RemoveAll(p => SamePath(p, path));
                if (sound.candidates.Any(c => SamePath(c.path, path)))
                    continue;

                var insertAt = sound.candidates.Count(c => c.user);
                sound.candidates.Insert(insertAt, new SfxCandidate
                {
                    file = Path.GetFileName(path), path = path, library = "My picks", year = "", note = "", user = true,
                });
                added++;
            }

            SaveList();
            if (added > 0 || Processed(sound.id) == null)
                Process(sound);
        }

        private void RemovePick(SfxSoundDef sound, SfxPrepClip clip)
        {
            var what = clip.user ? "your pick" : "the scout's pick";
            if (!EditorUtility.DisplayDialog("SFX Prep",
                    $"Remove {what} '{clip.source}' from {sound.id}?\n\nAll its clips go. Drop the file on the window " +
                    "to bring it back.", "Remove", "Cancel"))
                return;

            if (clip.user)
                sound.candidates.RemoveAll(c => SamePath(c.path, clip.path));
            else if (!sound.excluded.Any(p => SamePath(p, clip.path)))
                sound.excluded.Add(clip.path);

            SaveList();
            Process(sound);
        }

        /// <summary>
        /// How a dropped file is saved in the list: relative to the SFX library when it's inside it, else relative to the
        /// project, else the full path. sfx_prep.py resolves them in that order.
        /// </summary>
        private static string StoredPath(string fullPath)
        {
            fullPath = Path.GetFullPath(fullPath).Replace('\\', '/');
            foreach (var root in new[] { SfxPrepSettings.LibraryDir, ProjectRoot })
            {
                if (string.IsNullOrEmpty(root))
                    continue;
                var prefix = Path.GetFullPath(root).Replace('\\', '/').TrimEnd('/') + "/";
                if (fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return fullPath.Substring(prefix.Length);
            }

            return fullPath;
        }

        private static bool SamePath(string a, string b) =>
            string.Equals(a?.Replace('\\', '/'), b?.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);

        private void HandleFileDrop(SfxSoundDef sound)
        {
            var e = Event.current;
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform)
                return;

            // Explorer gives full paths, the Project window gives Assets/... paths.
            var files = DragAndDrop.paths
                .Where(p => AudioExtensions.Contains(Path.GetExtension(p).ToLowerInvariant()))
                .Select(p => Path.GetFullPath(Path.IsPathRooted(p) ? p : Path.Combine(ProjectRoot, p)))
                .ToList();
            if (files.Count == 0)
                return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                AddPicks(sound, files);
            }

            e.Use();
        }

        // ------------------------------------------------------------------ sounds and categories

        private void AddSound()
        {
            var category = _list.categories[Mathf.Clamp(_newCategory, 0, _list.categories.Count - 1)].id;
            var sound = new SfxSoundDef
            {
                id = _newId,
                category = category,
                type = Types[_newType],
                order = _list.sounds.Where(s => s.category == category).Select(s => s.order).DefaultIfEmpty(0).Max() + 1,
            };
            _list.sounds.Add(sound);
            SaveList();
            _selectedId = sound.id;
            _newId = "";
            _showNewSound = false;
            CountExported();
        }

        private void RemoveSound(SfxSoundDef sound)
        {
            var exported = _exportedCount.TryGetValue(sound.id, out var n) ? n : 0;
            var note = exported > 0 ? $"\n\nIts {exported} exported clip(s) in {ExportDir(sound)} stay; delete them yourself if unused." : "";
            if (!EditorUtility.DisplayDialog("SFX Prep", $"Remove '{sound.id}' from the sound list?{note}",
                    "Remove", "Cancel"))
                return;

            StopPreview();
            _list.sounds.Remove(sound);
            SaveList();
            _selection?.sounds.RemoveAll(e => e.id == sound.id);
            _selection?.Save(SelectionFile);
            _selectedId = _list.Ordered().FirstOrDefault()?.id;
            CountExported();
        }

        private void MarkCategoryStale(string categoryId)
        {
            foreach (var sound in _list.sounds.Where(s => s.category == categoryId))
                _stale.Add(sound.id);
        }

        // ------------------------------------------------------------------ export

        private void Export(IReadOnlyList<SfxSoundDef> sounds)
        {
            sounds = sounds.Where(s => Processed(s.id) != null).ToList();
            var replacing = sounds.Where(s => _exportedCount.TryGetValue(s.id, out var n) && n > 0).ToList();
            if (replacing.Count > 0)
            {
                var names = string.Join(", ", replacing.Take(8).Select(s => s.id)) + (replacing.Count > 8 ? "..." : "");
                if (!EditorUtility.DisplayDialog("SFX Prep",
                        $"Replace the clips already in the project for {replacing.Count} sound(s)?\n\n{names}\n\n" +
                        "Files keep their names (sfx_pig_idle_01...), so references to them stay valid; " +
                        "clips you unticked are deleted.", "Replace", "Cancel"))
                    return;
            }

            StopPreview();
            var written = new List<(string path, SfxPrepClip clip)>();
            foreach (var sound in sounds)
            {
                var processed = Processed(sound.id);
                var kept = processed.clips.Where(c => _selection.IsKept(processed, c)).ToList();
                var dir = ExportDir(sound);
                var exportName = ExportName(sound);
                Directory.CreateDirectory(dir);
                var names = new HashSet<string>();
                for (var i = 0; i < kept.Count; i++)
                {
                    var path = $"{dir}/{exportName}_{i + 1:00}.wav";
                    File.Copy(Path.Combine(CacheDir, kept[i].file), path, true);
                    names.Add(Path.GetFileName(path));
                    written.Add((path, kept[i]));
                }

                foreach (var stale in Directory.GetFiles(dir, exportName + "_*.wav"))
                {
                    if (!names.Contains(Path.GetFileName(stale)))
                        AssetDatabase.DeleteAsset(stale.Replace('\\', '/'));
                }
            }

            AssetDatabase.Refresh();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var (path, clip) in written)
                {
                    if (AssetImporter.GetAtPath(path) is AudioImporter importer && ApplyImportSettings(importer, clip))
                        importer.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            CountExported();
            Debug.Log($"SFX Prep: exported {written.Count} clip(s) for {sounds.Count} sound(s).");
            if (sounds.Count == 1 && written.Count > 0)
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<AudioClip>(written[0].path));
        }

        /// <summary>Load type by length (decided by the script), Vorbis, mono for mono clips. True if anything changed.</summary>
        private static bool ApplyImportSettings(AudioImporter importer, SfxPrepClip clip)
        {
            var settings = importer.defaultSampleSettings;
            var loadType = Enum.TryParse(clip.load_type, out AudioClipLoadType parsed) ? parsed : AudioClipLoadType.DecompressOnLoad;
            var preload = loadType != AudioClipLoadType.Streaming;
            var mono = clip.channels == 1;

            var changed = settings.loadType != loadType
                          || settings.compressionFormat != AudioCompressionFormat.Vorbis
                          || !Mathf.Approximately(settings.quality, clip.quality)
                          || settings.preloadAudioData != preload
                          || importer.forceToMono != mono
                          || importer.loadInBackground != !preload;
            if (!changed)
                return false;

            settings.loadType = loadType;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = clip.quality;
            settings.preloadAudioData = preload;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = mono;
            importer.loadInBackground = !preload;
            return true;
        }

        // ------------------------------------------------------------------ preview

        private void TogglePreview(SfxPrepSound sound, SfxPrepClip clip)
        {
            if (_playingFile == clip.file)
            {
                StopPreview();
                return;
            }

            if (!_previewCache.TryGetValue(clip.file, out var audioClip) || audioClip == null)
            {
                audioClip = LoadWav(Path.Combine(CacheDir, clip.file));
                if (audioClip == null)
                    return;
                _previewCache[clip.file] = audioClip;
            }

            if (_previewObject == null)
            {
                _previewObject = new GameObject("[SFX Prep Preview]") { hideFlags = HideFlags.HideAndDontSave };
                _previewSource = _previewObject.AddComponent<AudioSource>();
                _previewSource.playOnAwake = false;
                _previewSource.spatialBlend = 0f;
            }

            _previewSource.Stop();
            _previewSource.clip = audioClip;
            _previewSource.loop = sound.loop;
            _previewSource.Play();
            _playingFile = clip.file;
        }

        private void StopPreview()
        {
            if (_previewSource != null)
                _previewSource.Stop();
            _playingFile = null;
        }

        private void ClearPreviewCache()
        {
            foreach (var clip in _previewCache.Values)
            {
                if (clip != null)
                    DestroyImmediate(clip);
            }

            _previewCache.Clear();
        }

        /// <summary>Reads the 16-bit PCM WAVs the script writes into an in-memory AudioClip.</summary>
        private static AudioClip LoadWav(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                int channels = 0, rate = 0, bits = 0, dataStart = -1, dataSize = 0;
                for (var pos = 12; pos + 8 <= bytes.Length;)
                {
                    var id = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
                    var size = BitConverter.ToInt32(bytes, pos + 4);
                    if (id == "fmt ")
                    {
                        channels = BitConverter.ToInt16(bytes, pos + 10);
                        rate = BitConverter.ToInt32(bytes, pos + 12);
                        bits = BitConverter.ToInt16(bytes, pos + 22);
                    }
                    else if (id == "data")
                    {
                        dataStart = pos + 8;
                        dataSize = Math.Min(size, bytes.Length - dataStart);
                        break;
                    }

                    pos += 8 + size + (size & 1);
                }

                if (bits != 16 || dataStart < 0 || channels == 0)
                {
                    Debug.LogWarning($"SFX Prep: can't preview {path} (expected 16-bit PCM).");
                    return null;
                }

                var samples = new float[dataSize / 2];
                for (var i = 0; i < samples.Length; i++)
                    samples[i] = BitConverter.ToInt16(bytes, dataStart + i * 2) / 32768f;

                var clip = AudioClip.Create(Path.GetFileNameWithoutExtension(path), samples.Length / channels, channels, rate, false);
                clip.SetData(samples, 0);
                clip.hideFlags = HideFlags.HideAndDontSave;
                return clip;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SFX Prep: can't preview {path}: {e.Message}");
                return null;
            }
        }

        // ------------------------------------------------------------------ GUI

        private void OnGUI()
        {
            if (_list == null)
            {
                var missing = SfxPrepSettings.ConfigExists ? $"No sound list at {SoundListFile}." : $"No config at {ConfigFile}.";
                EditorGUILayout.HelpBox($"{missing} Create them from the package's examples, or point Settings at existing ones.",
                    MessageType.Warning);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(SfxPrepSettings.ConfigExists ? "Create sound list" : "Create config and sound list",
                            GUILayout.Width(200)))
                    {
                        var written = SfxPrepSettings.CreateMissingFiles();
                        if (written.Count > 0)
                            Debug.Log($"SFX Prep: created {string.Join(", ", written)}. Set the SFX library folder in Settings.");
                        _showSettings = true;
                        Reload();
                        GUIUtility.ExitGUI();
                    }

                    if (GUILayout.Button("Reload", GUILayout.Width(80)))
                        Reload();
                }

                _showSettings = EditorGUILayout.Foldout(_showSettings, "Settings", true);
                if (_showSettings)
                    DrawSettings();
                return;
            }

            DrawToolbar();

            if (_showSettings)
                DrawSettings();

            if (_runner.IsRunning)
            {
                DrawProgress();
                return;
            }

            if (_showCategories)
                DrawCategories();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSoundList();
                DrawSoundDetails();
            }

            if (Selected != null)
                HandleFileDrop(Selected);
        }

        private void DrawToolbar()
        {
            using var _ = new EditorGUILayout.HorizontalScope(EditorStyles.toolbar);
            using (new EditorGUI.DisabledScope(_runner.IsRunning))
            {
                if (GUILayout.Button("Process all", EditorStyles.toolbarButton))
                    Process(null);
                using (new EditorGUI.DisabledScope(_manifest == null))
                {
                    if (GUILayout.Button("Export all...", EditorStyles.toolbarButton))
                    {
                        Export(_list.Ordered().ToList());
                        GUIUtility.ExitGUI(); // the dialog and reimport break this frame's layout
                    }
                }

                _showCategories = GUILayout.Toggle(_showCategories, "Categories", EditorStyles.toolbarButton);
            }

            GUILayout.FlexibleSpace();
            var kept = _list.sounds.Sum(KeptCount);
            var exported = _exportedCount.Values.Count(n => n > 0);
            GUILayout.Label($"{_list.sounds.Count} sounds · {kept} clips kept · {exported} in project", EditorStyles.miniLabel);
            _showSettings = GUILayout.Toggle(_showSettings, "Settings", EditorStyles.toolbarButton);
        }

        private void DrawSettings()
        {
            using var _ = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"This machine ({SfxPrepSettings.UserFile})", EditorStyles.miniBoldLabel);
            SfxPrepSettings.Python = EditorGUILayout.DelayedTextField(
                new GUIContent("Python", "Executable with numpy installed."), SfxPrepSettings.Python);
            using (new EditorGUILayout.HorizontalScope())
            {
                SfxPrepSettings.LibraryDir = EditorGUILayout.DelayedTextField(
                    new GUIContent("SFX library", "Folder the candidates' paths are relative to (your downloaded sound libraries)."),
                    SfxPrepSettings.LibraryDir);
                if (GUILayout.Button("…", GUILayout.Width(24)))
                {
                    var picked = EditorUtility.OpenFolderPanel("SFX library", SfxPrepSettings.LibraryDir, "");
                    if (!string.IsNullOrEmpty(picked))
                        SfxPrepSettings.LibraryDir = picked;
                }
            }

            if (string.IsNullOrEmpty(SfxPrepSettings.LibraryDir))
                EditorGUILayout.HelpBox("No SFX library set: Process can't find the candidate files.", MessageType.Warning);

            EditorGUILayout.LabelField($"This project ({SfxPrepSettings.ProjectFile})", EditorStyles.miniBoldLabel);
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                var folder = EditorGUILayout.DelayedTextField(
                    new GUIContent("SFX Prep folder", "Holds sfx_config.json, sound_list.json and sfx-scout's rules. Relative to the project root."),
                    SfxPrepSettings.Folder);
                var audioRoot = EditorGUILayout.DelayedTextField(
                    new GUIContent("Audio root", "Exported clips go to <this>/<Category folder>/<sound id>/."), SfxPrepSettings.AudioRoot);
                if (check.changed)
                {
                    SfxPrepSettings.Folder = folder;
                    SfxPrepSettings.AudioRoot = audioRoot;
                    Reload();
                }
            }

            if (!SfxPrepSettings.ConfigExists)
                EditorGUILayout.HelpBox($"No config at {ConfigFile}.", MessageType.Warning);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open config"))
                    EditorUtility.OpenWithDefaultApp(Path.Combine(ProjectRoot, ConfigFile));
                if (GUILayout.Button("Open sound list"))
                    EditorUtility.OpenWithDefaultApp(Path.Combine(ProjectRoot, SoundListFile));
                if (GUILayout.Button("Show processed clips"))
                    EditorUtility.RevealInFinder(Path.Combine(ProjectRoot, CacheDir));
                if (GUILayout.Button("Reload"))
                    Reload();
            }
        }

        private void DrawProgress()
        {
            using var _ = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
            var t = _runner.Total > 0 ? (float)_runner.Done / _runner.Total : 0f;
            var rect = GUILayoutUtility.GetRect(18, 20, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(rect, t, $"{_runningLabel}  {_runner.Done}/{_runner.Total}");
            EditorGUILayout.LabelField(_runner.LastLine, EditorStyles.miniLabel);
            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
            {
                _runner.Cancel();
                Reload();
            }
        }

        private void DrawCategories()
        {
            using var box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Categories", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Order here is the order in the list. Changing settings needs a Reprocess of its sounds.",
                EditorStyles.miniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Id", EditorStyles.miniBoldLabel, GUILayout.Width(90));
                GUILayout.Label("Export folder", EditorStyles.miniBoldLabel, GUILayout.Width(100));
                GUILayout.Label("Prefix", EditorStyles.miniBoldLabel, GUILayout.Width(50));
                GUILayout.Label("Channels", EditorStyles.miniBoldLabel, GUILayout.Width(70));
                GUILayout.Label("LUFS", EditorStyles.miniBoldLabel, GUILayout.Width(50));
                GUILayout.Label(new GUIContent("Max 1-shot s", "Longest one-shot, in seconds"), EditorStyles.miniBoldLabel, GUILayout.Width(80));
                GUILayout.Label(new GUIContent("Loop s", $"Loop length; 0 = config ({SfxPrepSettings.LoopSeconds})"),
                    EditorStyles.miniBoldLabel, GUILayout.Width(50));
                GUILayout.Label(new GUIContent("Xfade s", $"Loop crossfade; 0 = config ({SfxPrepSettings.LoopCrossfade})"),
                    EditorStyles.miniBoldLabel, GUILayout.Width(50));
                GUILayout.Label(new GUIContent("Stream > s", "Clips longer than this stream from disk; 0 = never"),
                    EditorStyles.miniBoldLabel, GUILayout.Width(65));
                GUILayout.Label(new GUIContent("Quality", "Vorbis quality, 0..1"), EditorStyles.miniBoldLabel, GUILayout.Width(50));
            }

            using var scroll = new EditorGUILayout.ScrollViewScope(_categoryScroll, GUILayout.MaxHeight(200));
            _categoryScroll = scroll.scrollPosition;
            for (var i = 0; i < _list.categories.Count; i++)
            {
                var category = _list.categories[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(category.id, GUILayout.Width(90));
                    EditorGUI.BeginChangeCheck();
                    category.folder = EditorGUILayout.TextField(category.folder, GUILayout.Width(100));
                    category.prefix = EditorGUILayout.TextField(category.prefix, GUILayout.Width(50));
                    category.channels = EditorGUILayout.Popup(category.channels == 2 ? 1 : 0, ChannelOptions, GUILayout.Width(70)) + 1;
                    category.lufs = EditorGUILayout.FloatField(category.lufs, GUILayout.Width(50));
                    category.max_len = EditorGUILayout.FloatField(category.max_len, GUILayout.Width(80));
                    category.loop_seconds = EditorGUILayout.FloatField(category.loop_seconds, GUILayout.Width(50));
                    category.crossfade = EditorGUILayout.FloatField(category.crossfade, GUILayout.Width(50));
                    category.stream_after = EditorGUILayout.FloatField(category.stream_after, GUILayout.Width(65));
                    category.quality = Mathf.Clamp01(EditorGUILayout.FloatField(category.quality, GUILayout.Width(50)));
                    if (EditorGUI.EndChangeCheck())
                    {
                        SaveList();
                        MarkCategoryStale(category.id);
                        CountExported();
                    }

                    using (new EditorGUI.DisabledScope(i == 0))
                    {
                        if (GUILayout.Button("▲", GUILayout.Width(22)))
                        {
                            (_list.categories[i - 1], _list.categories[i]) = (_list.categories[i], _list.categories[i - 1]);
                            SaveList();
                        }
                    }

                    using (new EditorGUI.DisabledScope(i == _list.categories.Count - 1))
                    {
                        if (GUILayout.Button("▼", GUILayout.Width(22)))
                        {
                            (_list.categories[i + 1], _list.categories[i]) = (_list.categories[i], _list.categories[i + 1]);
                            SaveList();
                        }
                    }

                    var used = _list.sounds.Count(s => s.category == category.id);
                    using (new EditorGUI.DisabledScope(used > 0))
                    {
                        var tip = used > 0 ? $"{used} sound(s) use it; move them first" : "Remove this category";
                        if (GUILayout.Button(new GUIContent("×", tip), GUILayout.Width(22)))
                        {
                            _list.categories.RemoveAt(i);
                            SaveList();
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _newCategoryId = EditorGUILayout.TextField(_newCategoryId, GUILayout.Width(90));
                var valid = IdPattern.IsMatch(_newCategoryId) && _list.Category(_newCategoryId) == null;
                using (new EditorGUI.DisabledScope(!valid))
                {
                    if (GUILayout.Button("Add category", GUILayout.Width(100)))
                    {
                        _list.categories.Add(new SfxCategory
                        {
                            id = _newCategoryId,
                            folder = char.ToUpperInvariant(_newCategoryId[0]) + _newCategoryId.Substring(1),
                        });
                        SaveList();
                        _newCategoryId = "";
                    }
                }

                GUILayout.Label("lower-case id, e.g. music", EditorStyles.miniLabel);
            }
        }

        private void DrawSoundList()
        {
            using var column = new EditorGUILayout.VerticalScope(GUILayout.Width(260));
            _showNewSound = EditorGUILayout.Foldout(_showNewSound, "New sound", true);
            if (_showNewSound)
                DrawNewSound();

            using var scroll = new EditorGUILayout.ScrollViewScope(_listScroll);
            _listScroll = scroll.scrollPosition;

            string category = null;
            foreach (var sound in _list.Ordered())
            {
                if (sound.category != category)
                {
                    category = sound.category;
                    EditorGUILayout.Space(4);
                    var label = _list.Category(category) == null ? $"{category.ToUpperInvariant()} (missing category)" : category.ToUpperInvariant();
                    EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                }

                var processed = Processed(sound.id) != null;
                var kept = KeptCount(sound);
                var exported = _exportedCount.TryGetValue(sound.id, out var n) && n > 0;
                var rect = GUILayoutUtility.GetRect(10, 20, GUILayout.ExpandWidth(true));
                if (sound.id == _selectedId)
                    EditorGUI.DrawRect(rect, SelectedRowColor);

                var mark = exported ? "● " : "   ";
                var stale = _stale.Contains(sound.id) ? " *" : "";
                GUI.Label(new Rect(rect.x + 4, rect.y + 2, rect.width - 50, rect.height), mark + sound.id + stale, EditorStyles.label);

                var countStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
                if (!processed || kept == 0)
                    countStyle.normal.textColor = ShortColor;
                GUI.Label(new Rect(rect.xMax - 46, rect.y, 42, rect.height), processed ? $"{kept} kept" : "new", countStyle);

                if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                {
                    _selectedId = sound.id;
                    _detailScroll = Vector2.zero;
                    Event.current.Use();
                    Repaint();
                }
            }
        }

        private void DrawNewSound()
        {
            using var _ = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
            _newId = EditorGUILayout.TextField(new GUIContent("Id", "lower-case with dashes, e.g. pig-sneeze"), _newId);
            var categoryIds = _list.categories.Select(c => c.id).ToArray();
            _newCategory = EditorGUILayout.Popup("Category", _newCategory, categoryIds);
            _newType = EditorGUILayout.Popup("Type", _newType, Types);

            var error = !IdPattern.IsMatch(_newId) ? "Id: lower-case letters, digits and dashes."
                : _list.Sound(_newId) != null ? "That id is taken."
                : categoryIds.Length == 0 ? "Add a category first."
                : null;
            if (error != null && _newId.Length > 0)
                EditorGUILayout.LabelField(error, EditorStyles.miniLabel);
            using (new EditorGUI.DisabledScope(error != null))
            {
                if (GUILayout.Button("Add sound"))
                    AddSound();
            }
        }

        private void DrawSoundDetails()
        {
            var sound = Selected;
            if (sound == null)
            {
                EditorGUILayout.HelpBox("No sounds yet. Add one with New sound.", MessageType.Info);
                return;
            }

            using var column = new EditorGUILayout.VerticalScope();
            using var scroll = new EditorGUILayout.ScrollViewScope(_detailScroll);
            _detailScroll = scroll.scrollPosition;

            EditorGUILayout.LabelField(sound.id, EditorStyles.largeLabel);
            DrawSoundFields(sound);

            var processed = Processed(sound.id);
            var inProject = _exportedCount.TryGetValue(sound.id, out var n) ? n : 0;
            EditorGUILayout.LabelField(inProject > 0
                ? $"In project: {inProject} clip(s) in {ExportDir(sound)}"
                : $"Not exported yet → {ExportDir(sound)}", EditorStyles.miniLabel);

            if (_stale.Contains(sound.id))
                EditorGUILayout.HelpBox("Settings changed: Reprocess to apply them to the clips.", MessageType.Info);
            if (processed != null && processed.missing.Length > 0)
                EditorGUILayout.HelpBox($"{processed.missing.Length} pick(s) not found:\n" + string.Join("\n", processed.missing),
                    MessageType.Warning);
            if (processed != null && processed.skipped.Length > 0)
                EditorGUILayout.HelpBox($"{processed.skipped.Length} pick(s) couldn't be read:\n" + string.Join("\n", processed.skipped),
                    MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(processed == null))
                {
                    if (GUILayout.Button("Export this sound"))
                    {
                        Export(new[] { sound });
                        GUIUtility.ExitGUI();
                    }
                }

                if (GUILayout.Button(processed == null ? "Process" : "Reprocess"))
                    Process(sound);
                using (new EditorGUI.DisabledScope(processed == null))
                {
                    if (GUILayout.Button("Reset ticks"))
                    {
                        _selection.ResetToDefaults(processed);
                        _selection.Save(SelectionFile);
                    }
                }

                if (GUILayout.Button("Remove sound"))
                {
                    RemoveSound(sound);
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.LabelField("Found something better? Drop audio files (from anywhere) on this window to add them to this sound.",
                EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space(2);

            if (processed == null)
            {
                EditorGUILayout.HelpBox(sound.candidates.Count == 0
                    ? "No picks yet. Drop audio files here, or ask Claude to run /sfx-scout for this sound."
                    : $"{sound.candidates.Count} pick(s), not processed yet. Press Process.", MessageType.Info);
                return;
            }

            if (processed.clips.Length == 0)
            {
                EditorGUILayout.HelpBox("No clips came out of the picks for this sound.", MessageType.Warning);
                return;
            }

            string previousSource = null;
            foreach (var clip in processed.clips)
            {
                DrawClipRow(sound, processed, clip, firstOfSource: clip.path != previousSource);
                previousSource = clip.path;
            }
        }

        private void DrawSoundFields(SfxSoundDef sound)
        {
            var category = _list.Category(sound.category);
            var categoryIds = _list.categories.Select(c => c.id).ToList();
            if (category == null)
                categoryIds.Add(sound.category);

            DrawIdField(sound);

            EditorGUI.BeginChangeCheck();
            var newCategory = categoryIds[EditorGUILayout.Popup("Category", categoryIds.IndexOf(sound.category), categoryIds.ToArray())];
            var newType = Pick("Type", sound.type, Types);
            sound.description = EditorGUILayout.TextField(
                new GUIContent("Description", "What it is and where the game plays it. sfx-scout reads it when picking candidates."),
                sound.description);

            var processingChanged = newCategory != sound.category || newType != sound.type;
            sound.category = newCategory;
            sound.type = newType;

            _showOverrides = EditorGUILayout.Foldout(_showOverrides, "Processing overrides (0 = category default)", true);
            if (_showOverrides)
            {
                using var _ = new EditorGUI.IndentLevelScope();
                EditorGUI.BeginChangeCheck();
                var channelOptions = new[] { $"Category default ({(category?.channels == 2 ? "stereo" : "mono")})", "Mono", "Stereo" };
                sound.channels = EditorGUILayout.Popup("Channels", Mathf.Clamp(sound.channels, 0, 2), channelOptions);
                sound.lufs = EditorGUILayout.FloatField(new GUIContent("LUFS", $"category: {category?.lufs}"), sound.lufs);
                if (sound.IsLoop)
                {
                    sound.loop_seconds = EditorGUILayout.FloatField(
                        new GUIContent("Loop length s", Inherited(category?.loop_seconds ?? 0, SfxPrepSettings.LoopSeconds)), sound.loop_seconds);
                    sound.crossfade = EditorGUILayout.FloatField(
                        new GUIContent("Loop crossfade s", Inherited(category?.crossfade ?? 0, SfxPrepSettings.LoopCrossfade)), sound.crossfade);
                }
                else
                {
                    sound.max_len = EditorGUILayout.FloatField(new GUIContent("Max one-shot s", $"category: {category?.max_len}"), sound.max_len);
                }

                processingChanged |= EditorGUI.EndChangeCheck();
            }

            if (EditorGUI.EndChangeCheck())
            {
                SaveList();
                if (processingChanged)
                    _stale.Add(sound.id);
                CountExported();
            }
        }

        /// <summary>
        /// The id names the export folder and files, so it can change only while nothing is exported. The sound's clips
        /// are processed again under the new id.
        /// </summary>
        private void DrawIdField(SfxSoundDef sound)
        {
            var exported = _exportedCount.TryGetValue(sound.id, out var n) && n > 0;
            using (new EditorGUI.DisabledScope(exported))
            {
                var tip = exported
                    ? "Exported clips are named after it; remove them from the project to rename."
                    : "lower-case with dashes, e.g. pig-sneeze. Names the export folder and files.";
                var newId = EditorGUILayout.DelayedTextField(new GUIContent("Id", tip), sound.id);
                if (newId == sound.id)
                    return;

                if (!IdPattern.IsMatch(newId) || _list.Sound(newId) != null)
                {
                    EditorUtility.DisplayDialog("SFX Prep", IdPattern.IsMatch(newId)
                        ? $"'{newId}' is taken."
                        : "Ids are lower-case letters, digits and dashes, e.g. pig-sneeze.", "OK");
                    return;
                }

                StopPreview();
                _selection?.sounds.RemoveAll(e => e.id == sound.id);
                _selection?.Save(SelectionFile);
                _stale.Remove(sound.id);
                sound.id = newId;
                _selectedId = newId;
                SaveList();
                CountExported();
            }
        }

        /// <summary>Tooltip for a loop override: what 0 falls back to, the category's value or else the config's.</summary>
        private static string Inherited(float categoryValue, float configValue) =>
            categoryValue > 0 ? $"category: {categoryValue}" : $"config: {configValue}";

        /// <summary>A popup over fixed options that keeps a value it doesn't know (from older lists).</summary>
        private static string Pick(string label, string value, string[] options)
        {
            var all = options.Contains(value) || string.IsNullOrEmpty(value) ? options : options.Append(value).ToArray();
            return all[Mathf.Max(0, EditorGUILayout.Popup(label, Array.IndexOf(all, value), all))];
        }

        private void DrawClipRow(SfxSoundDef sound, SfxPrepSound processed, SfxPrepClip clip, bool firstOfSource)
        {
            var kept = _selection.IsKept(processed, clip);
            var rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(36));
            if (kept)
                EditorGUI.DrawRect(rect, KeptRowColor);

            var newKept = EditorGUILayout.Toggle(kept, GUILayout.Width(18));
            if (newKept != kept)
            {
                _selection.SetKept(processed, clip, newKept);
                _selection.Save(SelectionFile);
            }

            var playing = _playingFile == clip.file;
            if (GUILayout.Button(playing ? "■" : "▶", GUILayout.Width(28), GUILayout.Height(28)))
                TogglePreview(processed, clip);

            using (new EditorGUILayout.VerticalScope())
            {
                var mark = clip.user ? "★ " : "";
                EditorGUILayout.LabelField(new GUIContent($"{mark}{clip.source}  @ {clip.at:0.##}s", clip.path),
                    EditorStyles.label);
                var flags = (clip.peak_limited ? " · peak-limited" : "") + (clip.clipped_src > 0.0005f ? " · clipping in source" : "");
                EditorGUILayout.LabelField(
                    $"{clip.seconds:0.00} s · {(clip.channels == 1 ? "mono" : "stereo")} · SNR {clip.snr_db:0} dB · " +
                    $"gain {clip.gain_db:+0.0;-0.0} dB · score {clip.score:0.0}{flags}", EditorStyles.miniLabel);
            }

            if (firstOfSource)
            {
                if (GUILayout.Button(new GUIContent("×", "Remove this file from the sound's candidates"),
                        GUILayout.Width(22), GUILayout.Height(22)))
                {
                    RemovePick(sound, clip);
                    GUIUtility.ExitGUI();
                }
            }
            else
            {
                GUILayout.Space(26);
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
