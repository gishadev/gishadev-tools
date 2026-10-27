using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace gishadev.tools.Editor.SfxPrep
{
    /// <summary>
    /// Where SFX Prep keeps a project's data, where it exports clips, and the per-machine bits. Plain JSON, so
    /// sfx_prep.py and the sfx-scout skill read the same files:
    /// <list type="bullet">
    /// <item><c>ProjectSettings/Packages/com.gishadev.tools/SfxPrepSettings.json</c> (commit it): the SFX Prep folder
    /// that holds <c>sfx_config.json</c>, <c>sound_list.json</c> and sfx-scout's files, and the audio root.</item>
    /// <item><c>UserSettings/Packages/com.gishadev.tools/SfxPrep.json</c> (per machine, not committed): the Python
    /// executable and the sound library folder.</item>
    /// </list>
    /// The processing cache is always <c>Library/SfxPrep</c>.
    /// </summary>
    public static class SfxPrepSettings
    {
        public const string ProjectFile = "ProjectSettings/Packages/com.gishadev.tools/SfxPrepSettings.json";
        public const string UserFile = "UserSettings/Packages/com.gishadev.tools/SfxPrep.json";
        public const string CacheDir = "Library/SfxPrep";

        [Serializable]
        private class ProjectData
        {
            public string folder = "Tools/SfxPrep";
            public string audio_root = "Assets/_Project/Audio";
        }

        [Serializable]
        private class UserData
        {
            public string python = "python";
            public string library_dir = "";
        }

        // Only what the window shows; sfx_prep.py reads the rest.
        [Serializable]
        private class Config
        {
            public LoopConfig loop = new();
        }

        [Serializable]
        private class LoopConfig
        {
            public float seconds = 12f;
            public float crossfade = 0.5f;
        }

        private static ProjectData _project;
        private static UserData _user;
        private static Config _config;

        public static string ProjectRoot => Directory.GetParent(Application.dataPath)!.FullName;

        /// <summary>Holds sfx_config.json, sound_list.json and sfx-scout's rules and reports. Relative to the project root.</summary>
        public static string Folder
        {
            get => Project.folder;
            set
            {
                if (value == Project.folder)
                    return;
                Project.folder = value;
                _config = null;
                Write(ProjectFile, Project);
            }
        }

        /// <summary>Exported clips go to &lt;this&gt;/&lt;Category folder&gt;/&lt;sound id&gt;/.</summary>
        public static string AudioRoot
        {
            get => Project.audio_root;
            set
            {
                if (value == Project.audio_root)
                    return;
                Project.audio_root = value;
                Write(ProjectFile, Project);
            }
        }

        /// <summary>Executable with numpy installed. Per machine.</summary>
        public static string Python
        {
            get => User.python;
            set
            {
                if (value == User.python)
                    return;
                User.python = value;
                Write(UserFile, User);
            }
        }

        /// <summary>Folder the candidates' paths are relative to (the downloaded sound libraries). Per machine.</summary>
        public static string LibraryDir
        {
            get => User.library_dir;
            set
            {
                if (value == User.library_dir)
                    return;
                User.library_dir = value;
                Write(UserFile, User);
            }
        }

        public static string ConfigPath => Folder + "/sfx_config.json";
        public static string SoundListPath => Folder + "/sound_list.json";
        public static bool ConfigExists => File.Exists(ConfigPath);
        public static string ManifestFile => CacheDir + "/prep_manifest.json";
        public static string SelectionFile => CacheDir + "/selection.json";

        /// <summary>The config's loop length, used where a category and sound don't set their own.</summary>
        public static float LoopSeconds => LoadConfig().loop.seconds;

        public static float LoopCrossfade => LoadConfig().loop.crossfade;

        /// <summary>sfx_prep.py inside this package, wherever the package is installed (Packages/ or Assets/).</summary>
        public static string ScriptPath => ToolsFile("sfx_prep.py");

        /// <summary>Read every file again, e.g. after they were edited outside Unity.</summary>
        public static void Reload()
        {
            _project = null;
            _user = null;
            _config = null;
        }

        /// <summary>
        /// Writes the config and the sound list from the package's examples, where they don't exist yet.
        /// Returns the files written (empty when both were already there).
        /// </summary>
        public static List<string> CreateMissingFiles()
        {
            var written = new List<string>();
            if (!ConfigExists && CopyExample("sfx_config.example.json", ConfigPath))
                written.Add(ConfigPath);
            if (!File.Exists(SoundListPath) && CopyExample("sound_list.example.json", SoundListPath))
                written.Add(SoundListPath);

            _config = null;
            return written;
        }

        private static ProjectData Project
        {
            get
            {
                if (_project != null)
                    return _project;
                _project = Read<ProjectData>(ProjectFile);
                if (_project == null)
                {
                    _project = new ProjectData();
                    if (MigrateProjectAsset(_project))
                        Write(ProjectFile, _project);
                }

                return _project;
            }
        }

        private static UserData User
        {
            get
            {
                if (_user != null)
                    return _user;
                _user = Read<UserData>(UserFile);
                if (_user == null)
                {
                    _user = new UserData();
                    if (MigrateEditorPrefs(_user))
                        Write(UserFile, _user);
                }

                return _user;
            }
        }

        private static Config LoadConfig() => _config ??= Read<Config>(ConfigPath) ?? new Config();

        // 1.10.0 kept these in ProjectSettings/SfxPrepSettings.asset (configPath, audioRoot) and EditorPrefs.
        private static bool MigrateProjectAsset(ProjectData data)
        {
            const string oldFile = "ProjectSettings/SfxPrepSettings.asset";
            if (!File.Exists(oldFile))
                return false;

            var text = File.ReadAllText(oldFile);
            var config = Regex.Match(text, @"^\s*configPath:\s*(.+?)\s*$", RegexOptions.Multiline);
            var audioRoot = Regex.Match(text, @"^\s*audioRoot:\s*(.+?)\s*$", RegexOptions.Multiline);
            if (config.Success)
                data.folder = Path.GetDirectoryName(config.Groups[1].Value)!.Replace('\\', '/');
            if (audioRoot.Success)
                data.audio_root = audioRoot.Groups[1].Value;
            return true;
        }

        private static bool MigrateEditorPrefs(UserData data)
        {
            const string pythonKey = "gishadev.SfxPrep.Python";
            const string libraryKey = "gishadev.SfxPrep.LibraryDir";
            if (!EditorPrefs.HasKey(pythonKey) && !EditorPrefs.HasKey(libraryKey))
                return false;

            data.python = EditorPrefs.GetString(pythonKey, data.python);
            data.library_dir = EditorPrefs.GetString(libraryKey, data.library_dir);
            return true;
        }

        private static T Read<T>(string path) where T : class
        {
            if (!File.Exists(path))
                return null;

            try
            {
                return JsonUtility.FromJson<T>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogError($"SFX Prep: couldn't read {path}: {e.Message}");
                return null;
            }
        }

        private static void Write(string path, object data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonUtility.ToJson(data, true) + "\n");
        }

        private static bool CopyExample(string example, string destination)
        {
            var source = ToolsFile(example);
            if (source == null || !File.Exists(source))
            {
                Debug.LogError($"SFX Prep: {example} not found in the com.gishadev.tools package (Tools~/SfxPrep).");
                return false;
            }

            var dir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.Copy(source, destination);
            return true;
        }

        private static string ToolsFile(string name)
        {
            var root = PackageRoot();
            return root != null ? Path.Combine(root, "Tools~", "SfxPrep", name) : null;
        }

        private static string PackageRoot()
        {
            var info = PackageInfo.FindForAssembly(typeof(SfxPrepSettings).Assembly);
            if (info != null)
                return info.resolvedPath;

            // Embedded under Assets/ (e.g. as a git submodule): walk up from this script to the package.json.
            var guids = AssetDatabase.FindAssets($"{nameof(SfxPrepSettings)} t:MonoScript");
            foreach (var guid in guids)
            {
                var dir = Path.GetDirectoryName(Path.GetFullPath(AssetDatabase.GUIDToAssetPath(guid)));
                while (dir != null && !File.Exists(Path.Combine(dir, "package.json")))
                    dir = Path.GetDirectoryName(dir);
                if (dir != null)
                    return dir;
            }

            return null;
        }
    }
}
