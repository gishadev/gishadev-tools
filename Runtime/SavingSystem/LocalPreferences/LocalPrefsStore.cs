using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace gishadev.tools.SavingSystem
{
    /// <summary>A set of preferences backed by one file under <c>Application.persistentDataPath</c>.
    /// <para>Create one per save slot — two stores with different file names are fully independent.
    /// Main thread only.</para></summary>
    public sealed class LocalPrefsStore : IDisposable
    {
        public const string DEFAULT_FILE_NAME = "local";
        public const string FILE_EXTENSION = ".sg";

        // Every save starts with this header, so the loader can tell a plain file from an encrypted one
        // by looking at the file rather than at its name.
        static readonly byte[] MAGIC = { (byte)'G', (byte)'S', (byte)'A', (byte)'V' };
        const byte FORMAT_VERSION = 1;
        const byte FLAG_PLAIN = 0;
        const byte FLAG_ENCRYPTED = 1;
        const int HEADER_SIZE = 6; // magic(4) + version(1) + flags(1)

        public string FileName { get; }
        public string FilePath { get; }
        public string BackupPath { get; }
        public string TempPath { get; }

        /// <summary>Set to encrypt subsequent saves, null to write plain JSON. Changing this doesn't
        /// rewrite the file on disk — the next <see cref="Save"/> does.</summary>
        public ISaveEncryptor Encryptor { get; set; }

        /// <summary>True when there are changes that <see cref="Save"/> hasn't written yet.</summary>
        public bool IsDirty { get; private set; }

        public PrefsData Data { get; private set; }

        public event Action SaveCompleted;
        public event Action LoadCompleted;

        /// <summary>Raised when a file exists but couldn't be read. The store stays usable and empty.</summary>
        public event Action<string> LoadFailed;

        bool _autoSaveOnQuit;
        bool _disposed;

        public LocalPrefsStore(string fileName = DEFAULT_FILE_NAME, ISaveEncryptor encryptor = null,
            bool autoSaveOnQuit = true)
        {
            FileName = ValidateFileName(fileName);
            Encryptor = encryptor;
            Data = new PrefsData();

            string directory = Application.persistentDataPath;
            FilePath = Path.Combine(directory, FileName + FILE_EXTENSION);
            BackupPath = FilePath + ".bak";
            TempPath = FilePath + ".tmp";

            // Load last, and never let it throw past this point: a store that exists must be usable.
            // The old code assigned its singleton before loading, so one bad file left a permanently
            // half-built instance that logged "type is not supported" for the rest of the session.
            Load();

            AutoSaveOnQuit = autoSaveOnQuit;
        }

        /// <summary>Saves on application quit (in the Editor, on leaving Play Mode).</summary>
        public bool AutoSaveOnQuit
        {
            get { return _autoSaveOnQuit; }
            set
            {
                if (_autoSaveOnQuit == value)
                    return;

                _autoSaveOnQuit = value;

                if (!Application.isPlaying)
                    return;

                // Always unsubscribe first — re-subscribing a handler that's already attached would
                // save once per subscription.
                Application.wantsToQuit -= OnWantsToQuit;
                if (value)
                    Application.wantsToQuit += OnWantsToQuit;
            }
        }

        #region Persistence

        /// <summary>Reads the save file into memory, replacing everything currently held.
        /// <para>A missing file is not a failure — it just leaves the store empty.</para></summary>
        public bool Load()
        {
            string error = null;

            if (File.Exists(FilePath) && TryLoadFrom(FilePath, out error))
            {
                IsDirty = false;
                LoadCompleted?.Invoke();
                return true;
            }

            // The main file is missing or unreadable. A backup means the last save was interrupted
            // partway, so prefer stale data over no data.
            if (File.Exists(BackupPath) && TryLoadFrom(BackupPath, out _))
            {
                Debug.LogWarning($"LocalPrefsStore: \"{FileName}\" was unreadable " +
                                 $"({error ?? "file is missing"}), recovered the previous save from backup.");
                IsDirty = true; // in memory this is now newer than the main file
                LoadCompleted?.Invoke();
                return true;
            }

            Data = new PrefsData();
            IsDirty = false;

            if (error == null)
            {
                // Nothing on disk yet — a first run, not a problem.
                LoadCompleted?.Invoke();
                return true;
            }

            Debug.LogError($"LocalPrefsStore: could not load \"{FileName}\" ({error}). Starting empty.");
            LoadFailed?.Invoke(error);
            return false;
        }

        bool TryLoadFrom(string path, out string error)
        {
            error = null;

            byte[] raw;
            try
            {
                raw = File.ReadAllBytes(path);
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }

            if (!TryReadPayload(raw, out string json, out error))
                return false;

            try
            {
                // Parsing into a new instance rather than overwriting the live one keeps the store's
                // current contents intact if the JSON turns out to be malformed.
                PrefsData parsed = JsonUtility.FromJson<PrefsData>(json);
                if (parsed == null)
                {
                    error = "file did not contain a preferences object";
                    return false;
                }

                parsed.RebuildMap();
                Data = parsed;
                return true;
            }
            catch (Exception e)
            {
                error = "malformed JSON: " + e.Message;
                return false;
            }
        }

        bool TryReadPayload(byte[] raw, out string json, out string error)
        {
            json = null;
            error = null;

            if (raw == null || raw.Length < HEADER_SIZE)
            {
                error = "file is empty or truncated";
                return false;
            }

            for (int i = 0; i < MAGIC.Length; i++)
            {
                if (raw[i] != MAGIC[i])
                {
                    error = "not a save file (bad header)";
                    return false;
                }
            }

            byte version = raw[MAGIC.Length];
            if (version > FORMAT_VERSION)
            {
                error = $"file was written by a newer version of the save format (v{version})";
                return false;
            }

            byte flags = raw[MAGIC.Length + 1];
            byte[] body = new byte[raw.Length - HEADER_SIZE];
            Buffer.BlockCopy(raw, HEADER_SIZE, body, 0, body.Length);

            if (flags == FLAG_PLAIN)
            {
                json = Encoding.UTF8.GetString(body);
                return true;
            }

            if (flags != FLAG_ENCRYPTED)
            {
                error = $"unknown format flag ({flags})";
                return false;
            }

            if (Encryptor == null)
            {
                error = "file is encrypted but the store has no encryptor";
                return false;
            }

            if (!Encryptor.TryDecrypt(body, out json))
            {
                error = "decryption failed — wrong key, or the file was modified";
                return false;
            }

            return true;
        }

        /// <summary>Writes the current preferences to disk, replacing the file atomically.</summary>
        public bool Save()
        {
            try
            {
                byte[] payload = BuildPayload();

                string directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllBytes(TempPath, payload);
                CommitTempFile();

                IsDirty = false;
                SyncFileSystem();
                SaveCompleted?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                // The live file is only ever replaced by a fully written temp file, so a failure here
                // leaves the previous save intact.
                Debug.LogError($"LocalPrefsStore: failed to save \"{FileName}\": {e.Message}");
                TryDelete(TempPath);
                return false;
            }
        }

        /// <summary>Saves only when something has actually changed.</summary>
        public bool SaveIfDirty()
        {
            return !IsDirty || Save();
        }

        byte[] BuildPayload()
        {
            string json = JsonUtility.ToJson(Data);
            bool encrypt = Encryptor != null;

            byte[] body = encrypt
                ? Encryptor.Encrypt(json)
                : Encoding.UTF8.GetBytes(json);

            byte[] payload = new byte[HEADER_SIZE + body.Length];
            Buffer.BlockCopy(MAGIC, 0, payload, 0, MAGIC.Length);
            payload[MAGIC.Length] = FORMAT_VERSION;
            payload[MAGIC.Length + 1] = encrypt ? FLAG_ENCRYPTED : FLAG_PLAIN;
            Buffer.BlockCopy(body, 0, payload, HEADER_SIZE, body.Length);

            return payload;
        }

        void CommitTempFile()
        {
            if (!File.Exists(FilePath))
            {
                File.Move(TempPath, FilePath);
                return;
            }

            try
            {
                // Single atomic swap that also rotates the old save into the backup slot.
                File.Replace(TempPath, FilePath, BackupPath);
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceManually();
            }
            catch (IOException)
            {
                // Some filesystems (notably Emscripten's under WebGL) don't implement replace.
                ReplaceManually();
            }
        }

        void ReplaceManually()
        {
            TryDelete(BackupPath);
            File.Move(FilePath, BackupPath);
            File.Move(TempPath, FilePath);
        }

        /// <summary>Deletes the save file, its backup and any leftover temp file. In-memory values are
        /// untouched — call <see cref="ClearAll"/> too if you want a clean slate.</summary>
        public void DeleteFile()
        {
            TryDelete(FilePath);
            TryDelete(BackupPath);
            TryDelete(TempPath);
            SyncFileSystem();
        }

        static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"LocalPrefsStore: could not delete \"{path}\": {e.Message}");
            }
        }

        static void SyncFileSystem()
        {
            // No-op off WebGL; there, it's what makes the write outlive a page reload.
            WebGLFileSystem.Sync();
        }

        bool OnWantsToQuit()
        {
            try
            {
                SaveIfDirty();
            }
            catch (Exception e)
            {
                Debug.LogError($"LocalPrefsStore: save on quit failed for \"{FileName}\": {e.Message}");
            }

            return true; // a failed save must never trap the player in the game
        }

        #endregion

        #region Values

        /// <summary>Returns the stored value, or <paramref name="defaultValue"/> if the key isn't set.
        /// <para>Reading never creates the key.</para></summary>
        public T Get<T>(string key, T defaultValue = default)
        {
            if (Data.TryGet(out Prefs<T> prefs))
                return prefs.GetPref(key, defaultValue);

            Debug.LogError(TypeIsNotSupported("Get", typeof(T)));
            return defaultValue;
        }

        /// <summary>Reads a value, distinguishing "not set" from "set to the default".</summary>
        public bool TryGet<T>(string key, out T value)
        {
            if (Data.TryGet(out Prefs<T> prefs))
                return prefs.TryGetValue(key, out value);

            Debug.LogError(TypeIsNotSupported("TryGet", typeof(T)));
            value = default;
            return false;
        }

        public T Set<T>(string key, T value)
        {
            if (Data.TryGet(out Prefs<T> prefs))
            {
                prefs.SetPref(key, value);
                IsDirty = true;
                return value;
            }

            Debug.LogError(TypeIsNotSupported("Set", typeof(T)));
            return default;
        }

        /// <summary>True if the key is set for any supported type.</summary>
        public bool HasKey(string key)
        {
            return Data.ContainsKey(key);
        }

        public bool HasKey<T>(string key)
        {
            if (Data.TryGet(out Prefs<T> prefs))
                return prefs.ContainsKey(key);

            Debug.LogError(TypeIsNotSupported("HasKey", typeof(T)));
            return false;
        }

        /// <summary>Removes the key from every type it's set for.</summary>
        public bool RemoveKey(string key)
        {
            bool removed = Data.RemoveKey(key);
            if (removed)
                IsDirty = true;
            return removed;
        }

        public bool RemoveKey<T>(string key)
        {
            if (!Data.TryGet(out Prefs<T> prefs))
            {
                Debug.LogError(TypeIsNotSupported("RemoveKey", typeof(T)));
                return false;
            }

            bool removed = prefs.RemoveKey(key);
            if (removed)
                IsDirty = true;
            return removed;
        }

        /// <summary>Clears every key of every type. Use with caution.</summary>
        public void ClearAll()
        {
            Data.ClearAll();
            IsDirty = true;
        }

        public void ClearAll<T>()
        {
            if (!Data.TryGet(out Prefs<T> prefs))
            {
                Debug.LogError(TypeIsNotSupported("ClearAll", typeof(T)));
                return;
            }

            prefs.ClearAll();
            IsDirty = true;
        }

        /// <summary>Renames the key wherever it's set. Returns the name it ended up with.</summary>
        public string ChangeKey(string oldKey, string newKey)
        {
            bool changed = false;
            foreach (var pref in Data.All)
                if (pref.ChangeKey(oldKey, newKey) == newKey)
                    changed = true;

            if (changed)
                IsDirty = true;
            return changed ? newKey : oldKey;
        }

        public string ChangeKey<T>(string oldKey, string newKey)
        {
            if (!Data.TryGet(out Prefs<T> prefs))
            {
                Debug.LogError(TypeIsNotSupported("ChangeKey", typeof(T)));
                return oldKey;
            }

            string result = prefs.ChangeKey(oldKey, newKey);
            if (result == newKey)
                IsDirty = true;
            return result;
        }

        /// <summary>Renames the first key holding <paramref name="value"/>.
        /// <para>Slow — it scans every entry of that type.</para></summary>
        public string ChangeKeyByValue<T>(T value, string newKey)
        {
            if (!Data.TryGet(out Prefs<T> prefs))
            {
                Debug.LogError(TypeIsNotSupported("ChangeKeyByValue", typeof(T)));
                return null;
            }

            string key = prefs.KeyByValue(value);
            if (key == null)
                return null;

            string result = prefs.ChangeKey(key, newKey);
            if (result == newKey)
                IsDirty = true;
            return result;
        }

        /// <summary>Removes the first key holding <paramref name="value"/>. Slow.</summary>
        public bool RemoveKeyByValue<T>(T value)
        {
            if (!Data.TryGet(out Prefs<T> prefs))
            {
                Debug.LogError(TypeIsNotSupported("RemoveKeyByValue", typeof(T)));
                return false;
            }

            string key = prefs.KeyByValue(value);
            if (key == null)
                return false;

            prefs.RemoveKey(key);
            IsDirty = true;
            return true;
        }

        /// <summary>Removes every key holding <paramref name="value"/>. Slow.</summary>
        public bool RemoveKeysByValue<T>(T value)
        {
            if (!Data.TryGet(out Prefs<T> prefs))
            {
                Debug.LogError(TypeIsNotSupported("RemoveKeysByValue", typeof(T)));
                return false;
            }

            List<string> keys = prefs.KeysByValue(value);
            if (keys.Count == 0)
                return false;

            prefs.RemoveKeys(keys);
            IsDirty = true;
            return true;
        }

        public int KeysCount<T>()
        {
            if (Data.TryGet(out Prefs<T> prefs))
                return prefs.Count;

            Debug.LogError(TypeIsNotSupported("KeysCount", typeof(T)));
            return 0;
        }

        /// <summary>Every key set for this type. Allocates — don't call it per frame.</summary>
        public string[] AllKeys<T>()
        {
            if (Data.TryGet(out Prefs<T> prefs))
                return prefs.AllKeys(typeof(T));

            Debug.LogError(TypeIsNotSupported("AllKeys", typeof(T)));
            return Array.Empty<string>();
        }

        /// <summary>Every value stored for this type. Allocates — don't call it per frame.</summary>
        public List<T> AllValues<T>()
        {
            if (!Data.TryGet(out Prefs<T> prefs))
            {
                Debug.LogError(TypeIsNotSupported("AllValues", typeof(T)));
                return new List<T>();
            }

            List<T> allValues = new List<T>(prefs.Count);
            foreach (var pref in prefs.All())
                allValues.Add(pref.value);
            return allValues;
        }

        #endregion

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Application.wantsToQuit -= OnWantsToQuit;
            _autoSaveOnQuit = false;
        }

        static string ValidateFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Save file name must not be empty.", nameof(fileName));

            // Rejects directory separators too, so a file name can't escape persistentDataPath.
            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException($"Save file name \"{fileName}\" contains invalid characters.",
                    nameof(fileName));

            return fileName;
        }

        static string TypeIsNotSupported(string methodName, Type t)
        {
            return $"LocalPrefsStore {methodName}: type \"{t.Name}\" is not supported.";
        }
    }
}
