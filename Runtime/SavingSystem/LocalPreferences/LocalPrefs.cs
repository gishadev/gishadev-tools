using System.Collections.Generic;
using UnityEngine;

namespace gishadev.tools.SavingSystem
{
    /// <summary>Static shortcut to a single shared <see cref="LocalPrefsStore"/> — a drop-in replacement
    /// for <c>PlayerPrefs</c> that also handles vectors and saves to a file you can move or delete.
    /// <para>For save slots, multiple profiles or anything you'd want to inject, create
    /// <see cref="LocalPrefsStore"/> instances directly instead.</para>
    /// <example><code>
    /// LocalPrefs.SetInt("highscore", 42);
    /// LocalPrefs.Save();
    /// </code></example></summary>
    public static class LocalPrefs
    {
        static LocalPrefsStore s_store;

        /// <summary>The shared store, created on first use. Reading it never fails: a broken save file
        /// leaves it empty and usable rather than unusable.</summary>
        public static LocalPrefsStore Store
        {
            get
            {
                if (s_store == null)
                    s_store = new LocalPrefsStore(LocalPrefsStore.DEFAULT_FILE_NAME);
                return s_store;
            }
        }

        /// <summary>Points the shared store at a different file. Pass an encryptor to encrypt it.</summary>
        public static LocalPrefsStore Use(string fileName, ISaveEncryptor encryptor = null)
        {
            s_store?.Dispose();
            s_store = new LocalPrefsStore(fileName, encryptor);
            return s_store;
        }

        // Statics survive Play Mode when domain reload is disabled, so a stale store (and its
        // wantsToQuit subscription) would otherwise leak into the next session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_store?.Dispose();
            s_store = null;
        }

        #region Persistence

        public static bool Save()
        {
            return Store.Save();
        }

        public static bool Load()
        {
            return Store.Load();
        }

        /// <summary>Deletes the shared store's file from disk.</summary>
        public static void DeleteFile()
        {
            Store.DeleteFile();
        }

        #endregion

        #region Typed accessors

        public static bool GetBool(string key, bool defaultValue = default) => Store.Get(key, defaultValue);
        public static bool SetBool(string key, bool value) => Store.Set(key, value);

        public static int GetInt(string key, int defaultValue = default) => Store.Get(key, defaultValue);
        public static int SetInt(string key, int value) => Store.Set(key, value);

        public static float GetFloat(string key, float defaultValue = default) => Store.Get(key, defaultValue);
        public static float SetFloat(string key, float value) => Store.Set(key, value);

        public static Vector2 GetVector2(string key, Vector2 defaultValue = default) => Store.Get(key, defaultValue);
        public static Vector2 SetVector2(string key, Vector2 value) => Store.Set(key, value);

        public static Vector3 GetVector3(string key, Vector3 defaultValue = default) => Store.Get(key, defaultValue);
        public static Vector3 SetVector3(string key, Vector3 value) => Store.Set(key, value);

        public static Vector4 GetVector4(string key, Vector4 defaultValue = default) => Store.Get(key, defaultValue);
        public static Vector4 SetVector4(string key, Vector4 value) => Store.Set(key, value);

        public static string GetString(string key, string defaultValue = default) => Store.Get(key, defaultValue);
        public static string SetString(string key, string value = default) => Store.Set(key, value);

        #endregion

        #region Generic accessors

        /// <summary>Returns the stored value, or <paramref name="defaultValue"/> if the key isn't set.
        /// <para>Reading never creates the key.</para></summary>
        public static T Get<T>(string key, T defaultValue = default) => Store.Get(key, defaultValue);

        /// <summary>Reads a value, distinguishing "not set" from "set to the default".</summary>
        public static bool TryGet<T>(string key, out T value) => Store.TryGet(key, out value);

        public static T Set<T>(string key, T value) => Store.Set(key, value);

        /// <summary>True if the key is set for any supported type.</summary>
        public static bool HasKey(string key) => Store.HasKey(key);

        public static bool HasKey<T>(string key) => Store.HasKey<T>(key);

        /// <summary>Removes the key from every type it's set for.</summary>
        public static bool RemoveKey(string key) => Store.RemoveKey(key);

        public static bool RemoveKey<T>(string key) => Store.RemoveKey<T>(key);

        /// <summary>Clears every key of every type. Use with caution.</summary>
        public static void ClearAll() => Store.ClearAll();

        public static void ClearAll<T>() => Store.ClearAll<T>();

        /// <summary>Renames the key wherever it's set. Returns the name it ended up with.</summary>
        public static string ChangeKey(string oldKey, string newKey) => Store.ChangeKey(oldKey, newKey);

        public static string ChangeKey<T>(string oldKey, string newKey) => Store.ChangeKey<T>(oldKey, newKey);

        /// <summary>Renames the first key holding <paramref name="value"/>.
        /// <para>Slow — it scans every entry of that type.</para></summary>
        public static string ChangeKeyByValue<T>(T value, string newKey) => Store.ChangeKeyByValue(value, newKey);

        /// <summary>Removes the first key holding <paramref name="value"/>. Slow.</summary>
        public static bool RemoveKeyByValue<T>(T value) => Store.RemoveKeyByValue(value);

        /// <summary>Removes every key holding <paramref name="value"/>. Slow.</summary>
        public static bool RemoveKeysByValue<T>(T value) => Store.RemoveKeysByValue(value);

        public static int KeysCount<T>() => Store.KeysCount<T>();

        /// <summary>Every key set for this type. Allocates — don't call it per frame.</summary>
        public static string[] AllKeys<T>() => Store.AllKeys<T>();

        /// <summary>Every value stored for this type. Allocates — don't call it per frame.</summary>
        public static List<T> AllValues<T>() => Store.AllValues<T>();

        #endregion
    }
}
