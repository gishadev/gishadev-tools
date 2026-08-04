namespace gishadev.tools.SavingSystem
{
    /// <summary>Flushes Unity's WebGL filesystem to IndexedDB.
    /// <para>On WebGL, writes under <c>Application.persistentDataPath</c> go to an in-memory filesystem
    /// and are only persisted when it's synced — without this, saves vanish on page reload. A no-op on
    /// every other platform, so callers don't need to guard it.</para></summary>
    public static class WebGLFileSystem
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void GishadevTools_SyncFs();
#endif

        public static void Sync()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            GishadevTools_SyncFs();
#endif
        }
    }
}
