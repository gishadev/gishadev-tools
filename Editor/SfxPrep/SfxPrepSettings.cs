using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace gishadev.tools.Editor.SfxPrep
{
    /// <summary>
    /// Where SFX Prep finds a project's data and where it exports clips. Saved per project in
    /// <c>ProjectSettings/SfxPrepSettings.asset</c> (commit it); the Python executable and the sound library folder
    /// are per machine and live in EditorPrefs.
    /// </summary>
    [FilePath("ProjectSettings/SfxPrepSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class SfxPrepSettings : ScriptableSingleton<SfxPrepSettings>
    {
        private const string PythonPrefKey = "gishadev.SfxPrep.Python";
        private const string LibraryPrefKey = "gishadev.SfxPrep.LibraryDir";
        private const string DefaultCacheDir = "Library/SfxPrep";

        [Tooltip("Processing settings (split, loop, loudness), the sound list path and the cache folder. Relative to the project root.")]
        [SerializeField] private string configPath = "Tools/SfxPrep/sfx_config.json";
        [Tooltip("Exported clips go to <this>/<Category folder>/<sound id>/.")]
        [SerializeField] private string audioRoot = "Assets/_Project/Audio";

        [Serializable]
        private class Config
        {
            public string sound_list = "Tools/SfxPrep/sound_list.json";
            public string cache_dir = DefaultCacheDir;
            public string library_dir = "";
        }

        private Config _config;

        public static string ProjectRoot => Directory.GetParent(Application.dataPath)!.FullName;

        public string ConfigPath
        {
            get => configPath;
            set
            {
                if (value == configPath)
                    return;
                configPath = value;
                _config = null;
                Save(true);
            }
        }

        public string AudioRoot
        {
            get => audioRoot;
            set
            {
                if (value == audioRoot)
                    return;
                audioRoot = value;
                Save(true);
            }
        }

        public bool ConfigExists => File.Exists(ConfigPath);
        public string SoundListPath => LoadConfig().sound_list;
        public string CacheDir => string.IsNullOrEmpty(LoadConfig().cache_dir) ? DefaultCacheDir : LoadConfig().cache_dir;
        public string ManifestFile => CacheDir + "/prep_manifest.json";
        public string SelectionFile => CacheDir + "/selection.json";

        /// <summary>Executable with numpy installed. Per machine.</summary>
        public static string Python
        {
            get => EditorPrefs.GetString(PythonPrefKey, "python");
            set => EditorPrefs.SetString(PythonPrefKey, value);
        }

        /// <summary>
        /// Folder the candidates' paths are relative to (the downloaded sound libraries). Per machine; empty = the
        /// <c>library_dir</c> in the config, if any.
        /// </summary>
        public static string LibraryDirOverride
        {
            get => EditorPrefs.GetString(LibraryPrefKey, "");
            set => EditorPrefs.SetString(LibraryPrefKey, value);
        }

        public string LibraryDir => string.IsNullOrEmpty(LibraryDirOverride) ? LoadConfig().library_dir : LibraryDirOverride;

        /// <summary>sfx_prep.py inside this package, wherever the package is installed (Packages/ or Assets/).</summary>
        public static string ScriptPath
        {
            get
            {
                var root = PackageRoot();
                return root != null ? Path.Combine(root, "Tools~", "SfxPrep", "sfx_prep.py") : null;
            }
        }

        /// <summary>Forget the cached config, e.g. after it was edited outside Unity.</summary>
        public void ReloadConfig() => _config = null;

        private Config LoadConfig()
        {
            if (_config != null)
                return _config;

            _config = new Config();
            if (!ConfigExists)
                return _config;

            try
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(ConfigPath), _config);
            }
            catch (Exception e)
            {
                Debug.LogError($"SFX Prep: couldn't read {ConfigPath}: {e.Message}");
            }

            return _config;
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
