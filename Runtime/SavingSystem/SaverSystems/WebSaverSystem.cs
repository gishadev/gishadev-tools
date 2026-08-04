using System;

namespace gishadev.tools.SavingSystem
{
    /// <summary>Saves to the browser's localStorage on WebGL, and delegates to another saver everywhere
    /// else (including the Editor, so you can test the same code path in Play Mode).
    /// <para>Keys are namespaced, so <see cref="ClearAll"/> only removes this game's entries even when
    /// several games share an origin. localStorage holds roughly 5 MB per origin — for larger saves use
    /// <see cref="FileSaverSystem"/>, whose files live in IndexedDB on WebGL.</para>
    /// <para>The JavaScript side ships with the package as <c>Runtime/Plugins/WebGL/
    /// GishadevToolsStorage.jslib</c> — no per-project setup needed.</para></summary>
    public class WebSaverSystem : ISaverSystem
    {
        readonly ISaverSystem _altSaverSystem;

        /// <param name="altSaverSystem">Used on every non-WebGL platform. Cannot be a
        /// <see cref="WebSaverSystem"/>.</param>
        public WebSaverSystem(ISaverSystem altSaverSystem)
        {
            if (altSaverSystem == null)
                throw new ArgumentNullException(nameof(altSaverSystem));

            if (altSaverSystem is WebSaverSystem)
                throw new ArgumentException("altSaverSystem cannot be a WebSaverSystem.",
                    nameof(altSaverSystem));

            _altSaverSystem = altSaverSystem;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // Emscripten symbols are global across the whole build, so these are prefixed to avoid
        // colliding with another package's .jslib at link time.
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void GishadevTools_Save(string key, string value);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern string GishadevTools_Load(string key);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern bool GishadevTools_HasKey(string key);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void GishadevTools_Delete(string key);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void GishadevTools_ClearAll();

        void SaveToLocalStorage(string key, string value) => GishadevTools_Save(key, value);
        string LoadFromLocalStorage(string key) => GishadevTools_Load(key);
        bool HasKeyInLocalStorage(string key) => GishadevTools_HasKey(key);
        void DeleteFromLocalStorage(string key) => GishadevTools_Delete(key);
        void ClearAllLocalStorage() => GishadevTools_ClearAll();
#else
        void SaveToLocalStorage(string key, string value) => _altSaverSystem.Save(key, value);
        string LoadFromLocalStorage(string key) => _altSaverSystem.Load(key);
        bool HasKeyInLocalStorage(string key) => _altSaverSystem.Exists(key);
        void DeleteFromLocalStorage(string key) => _altSaverSystem.Delete(key);
        void ClearAllLocalStorage() => _altSaverSystem.ClearAll();
#endif

        public void Save(string key, string data)
        {
            SaveToLocalStorage(key, data);
        }

        /// <summary>The stored blob, or <c>null</c> if the key isn't set.</summary>
        public string Load(string key)
        {
            return LoadFromLocalStorage(key);
        }

        public bool TryLoad(string key, out string result)
        {
            // One round trip: the JS side already returns null for a missing key.
            result = LoadFromLocalStorage(key);
            return result != null;
        }

        public bool Exists(string key)
        {
            return HasKeyInLocalStorage(key);
        }

        public void Delete(string key)
        {
            DeleteFromLocalStorage(key);
        }

        public void ClearAll()
        {
            ClearAllLocalStorage();
        }
    }
}
