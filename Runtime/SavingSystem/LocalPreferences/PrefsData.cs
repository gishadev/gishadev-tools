using System;
using System.Collections.Generic;
using UnityEngine;

namespace gishadev.tools.SavingSystem
{
    /// <summary>The serializable payload of a save file: one <see cref="Prefs{T}"/> container per supported type.
    /// <para>A plain class rather than a ScriptableObject — JsonUtility overwrites either, and this one
    /// doesn't need creating, parenting or protecting from domain reloads.</para></summary>
    [Serializable]
    public class PrefsData
    {
        public PrefsBool bools = new PrefsBool();
        public PrefsInt ints = new PrefsInt();
        public PrefsFloat floats = new PrefsFloat();
        public PrefsVector2 vector2 = new PrefsVector2();
        public PrefsVector3 vector3 = new PrefsVector3();
        public PrefsVector4 vector4 = new PrefsVector4();
        public PrefsString strings = new PrefsString();

        [NonSerialized] Dictionary<Type, IPrefs> _byType;

        public PrefsData()
        {
            RebuildMap();
        }

        public IEnumerable<IPrefs> All
        {
            get
            {
                EnsureMap();
                return _byType.Values;
            }
        }

        /// <summary>Rebuilds the type lookup. Must run after every deserialization: JsonUtility is free
        /// to hand back fresh container instances, and to skip constructors and field initializers
        /// entirely — so neither the map nor the containers themselves can be assumed to exist.</summary>
        public void RebuildMap()
        {
            if (_byType == null)
                _byType = new Dictionary<Type, IPrefs>();
            else
                _byType.Clear();

            Register(bools ??= new PrefsBool());
            Register(ints ??= new PrefsInt());
            Register(floats ??= new PrefsFloat());
            Register(vector2 ??= new PrefsVector2());
            Register(vector3 ??= new PrefsVector3());
            Register(vector4 ??= new PrefsVector4());
            Register(strings ??= new PrefsString());
        }

        void Register(IPrefs pref)
        {
            _byType[pref.type] = pref;
        }

        void EnsureMap()
        {
            if (_byType == null)
                RebuildMap();
        }

        public bool TryGet(Type type, out IPrefs pref)
        {
            EnsureMap();
            return _byType.TryGetValue(type, out pref);
        }

        /// <summary>Resolves the container for <typeparamref name="T"/> so callers can use its typed
        /// methods and skip the boxing that goes with the <see cref="IPrefs"/> object overloads.</summary>
        public bool TryGet<T>(out Prefs<T> pref)
        {
            EnsureMap();
            if (_byType.TryGetValue(typeof(T), out IPrefs untyped) && untyped is Prefs<T> typed)
            {
                pref = typed;
                return true;
            }

            pref = null;
            return false;
        }

        public bool ContainsKey(string key)
        {
            foreach (var pref in All)
                if (pref.ContainsKey(key))
                    return true;
            return false;
        }

        public bool RemoveKey(string key)
        {
            bool removed = false;
            foreach (var pref in All)
                if (pref.RemoveKey(key))
                    removed = true;
            return removed;
        }

        public void ClearAll()
        {
            foreach (var pref in All)
                pref.ClearAll();
        }
    }
}
