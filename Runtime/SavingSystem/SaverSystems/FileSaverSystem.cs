using System;

namespace gishadev.tools.SavingSystem
{
    /// <summary>Saves to a file under <c>Application.persistentDataPath</c>.
    /// <para>Each instance owns its own <see cref="LocalPrefsStore"/>, so the file name you construct it
    /// with is the one it both writes to <em>and</em> reads from. Two savers with different names are
    /// independent save slots.</para>
    /// <example><code>
    /// var slot = new FileSaverSystem("slot1");
    /// slot.Save("player", JsonUtility.ToJson(playerData));
    /// </code></example></summary>
    public sealed class FileSaverSystem : ISaverSystem, IDisposable
    {
        readonly bool _autoFlush;

        /// <summary>The underlying store, for the typed accessors and the save/load events.</summary>
        public LocalPrefsStore Store { get; }

        /// <param name="fileName">File name without extension, under <c>Application.persistentDataPath</c>.</param>
        /// <param name="encryptor">Pass one to encrypt the file. See <see cref="AesEncryptor"/>.</param>
        /// <param name="autoFlush">Write to disk on every change. Turn off if you're saving many keys at
        /// once — each flush reserializes the whole file — and call <see cref="Flush"/> when you're done.</param>
        public FileSaverSystem(string fileName = LocalPrefsStore.DEFAULT_FILE_NAME,
            ISaveEncryptor encryptor = null, bool autoFlush = true)
        {
            Store = new LocalPrefsStore(fileName, encryptor);
            _autoFlush = autoFlush;
        }

        public void Save(string key, string data)
        {
            Store.Set(key, data);
            FlushIfAuto();
        }

        public string Load(string key)
        {
            return Store.Get<string>(key, null);
        }

        public bool TryLoad(string key, out string result)
        {
            return Store.TryGet(key, out result);
        }

        public bool Exists(string key)
        {
            return Store.HasKey<string>(key);
        }

        public void Delete(string key)
        {
            if (Store.RemoveKey<string>(key))
                FlushIfAuto();
        }

        /// <summary>Clears this file's contents. Other savers and other files are untouched.</summary>
        public void ClearAll()
        {
            Store.ClearAll();
            FlushIfAuto();
        }

        /// <summary>Writes pending changes to disk. Only needed when <c>autoFlush</c> is off.</summary>
        public bool Flush()
        {
            return Store.SaveIfDirty();
        }

        public void Dispose()
        {
            Store.Dispose();
        }

        void FlushIfAuto()
        {
            if (_autoFlush)
                Store.Save();
        }
    }
}
