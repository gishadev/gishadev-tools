using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace gishadev.tools.SavingSystem
{
    public struct Pref<T>
    {
        public string name;
        public T value;

        public Pref(string name, T value)
        {
            this.name = name;
            this.value = value;
        }
    }

    public interface IPrefs
    {
        Type type { get; }
        int Count { get; }
        object SetPref(string key, object value);
        object GetPref(string key, object defaultValue);
        bool ContainsKey(string key);
        bool ContainsValue(object value);
        string ChangeKey(string oldKey, string newKey);
        string KeyByValue(object value);
        List<string> KeysByValue(object value);
        bool RemoveKey(string key);
        void RemoveKeys(List<string> keys);
        string[] AllKeys(Type type);
        List<object> AllValues(Type type);
        void ClearAll();
    }

    /// <summary>A serializable string-keyed store for one value type.
    /// <para>The dictionary is the source of truth; the key/value arrays exist only to carry it through
    /// JsonUtility and are rebuilt on every serialization.</para></summary>
    [Serializable]
    public class Prefs<T> : ISerializationCallbackReceiver, IPrefs
    {
        static readonly Type s_type = typeof(T);

        [SerializeField] string[] keys = Array.Empty<string>();
        [SerializeField] T[] values = Array.Empty<T>();

        [NonSerialized] Dictionary<string, T> _dictionary = new Dictionary<string, T>();

        public T this[string name]
        {
            get { return _dictionary[name]; }
            set { _dictionary[name] = value; }
        }

        public int Count
        {
            get { return _dictionary.Count; }
        }

        public Type type
        {
            get { return s_type; }
        }

        /// <summary>Every key/value pair currently stored.</summary>
        public IEnumerable<Pref<T>> All()
        {
            foreach (var kvp in _dictionary)
                yield return new Pref<T>(kvp.Key, kvp.Value);
        }

        /// <summary>Returns the stored value, or <paramref name="defaultValue"/> if the key is absent.
        /// <para>Reading never creates the key — use <see cref="SetPref(string,T)"/> for that.</para></summary>
        public T GetPref(string prefName, T defaultValue)
        {
            if (_dictionary.TryGetValue(prefName, out T value))
                return value;
            return defaultValue;
        }

        public object GetPref(string prefName, object defaultValue)
        {
            if (_dictionary.TryGetValue(prefName, out T value))
                return value;
            return defaultValue;
        }

        public T SetPref(string prefName, T newValue)
        {
            _dictionary[prefName] = newValue;
            return newValue;
        }

        public object SetPref(string prefName, object newValue)
        {
            _dictionary[prefName] = (T)newValue;
            return newValue;
        }

        public string[] AllKeys(Type type)
        {
            if (s_type == type)
                return _dictionary.Keys.ToArray();
            return Array.Empty<string>();
        }

        public List<object> AllValues(Type type)
        {
            if (type != s_type)
                return new List<object>();

            List<object> allValues = new List<object>(_dictionary.Count);
            foreach (var val in _dictionary.Values)
                allValues.Add(val);
            return allValues;
        }

        public string ChangeKey(string oldKey, string newKey)
        {
            if (!_dictionary.TryGetValue(oldKey, out T value))
                return oldKey;

            if (oldKey == newKey)
                return newKey;

            if (_dictionary.ContainsKey(newKey))
            {
                Debug.LogWarning($"Prefs<{s_type.Name}>: cannot rename \"{oldKey}\" to \"{newKey}\", " +
                                 "that key is already taken.");
                return oldKey;
            }

            _dictionary.Remove(oldKey);
            _dictionary.Add(newKey, value);
            return newKey;
        }

        public string KeyByValue(object value)
        {
            string key = default;
            T Value = (T)value;
            foreach (var pair in _dictionary)
                if (EqualityComparer<T>.Default.Equals(pair.Value, Value))
                {
                    key = pair.Key;
                    break;
                }

            return key;
        }

        public List<string> KeysByValue(object value)
        {
            List<string> keys = new List<string>();
            T Value = (T)value;
            foreach (var pair in _dictionary)
                if (EqualityComparer<T>.Default.Equals(pair.Value, Value))
                {
                    keys.Add(pair.Key);
                }

            return keys;
        }

        public bool RemoveKey(string key)
        {
            return _dictionary.Remove(key);
        }

        public void RemoveKeys(List<string> keysToRemove)
        {
            if (keysToRemove == null)
                return;

            for (int k = 0; k < keysToRemove.Count; k++)
                if (keysToRemove[k] != default)
                    _dictionary.Remove(keysToRemove[k]);
        }

        public void Add(string name, T value)
        {
            _dictionary.Add(name, value);
        }

        public void Remove(string key)
        {
            _dictionary.Remove(key);
        }

        public bool TryGetValue(string key, out T value)
        {
            return _dictionary.TryGetValue(key, out value);
        }

        public bool ContainsKey(string key)
        {
            return _dictionary.ContainsKey(key);
        }

        public bool ContainsValue(T value)
        {
            return _dictionary.ContainsValue(value);
        }

        public bool ContainsValue(object value)
        {
            if (value == null)
                return !s_type.IsValueType && _dictionary.ContainsValue(default);

            if (value.GetType() != s_type)
                return false;

            return _dictionary.ContainsValue((T)value);
        }

        public void ClearAll()
        {
            _dictionary.Clear();
            keys = Array.Empty<string>();
            values = Array.Empty<T>();
        }

        // Unload from dictionary to arrays. Always rebuilds: skipping an empty dictionary would
        // leave the previous contents in the arrays and write deleted entries back out.
        public void OnBeforeSerialize()
        {
            keys = new string[_dictionary.Count];
            values = new T[_dictionary.Count];
            int i = 0;
            foreach (var kvp in _dictionary)
            {
                keys[i] = kvp.Key;
                values[i] = kvp.Value;
                i++;
            }
        }

        // Load items into the dictionary. Tolerates hand-edited or truncated files: mismatched array
        // lengths and duplicate keys must not throw out of Unity's deserialization callback.
        public void OnAfterDeserialize()
        {
            _dictionary = new Dictionary<string, T>();

            if (keys == null || values == null)
                return;

            int count = Mathf.Min(keys.Length, values.Length);
            for (int i = 0; i < count; i++)
            {
                if (keys[i] == null)
                    continue;
                _dictionary[keys[i]] = values[i];
            }
        }
    }
}
