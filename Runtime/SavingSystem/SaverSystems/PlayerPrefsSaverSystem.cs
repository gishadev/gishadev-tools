using UnityEngine;

namespace gishadev.tools.SavingSystem
{
    /// <summary>Saves through Unity's <see cref="PlayerPrefs"/> (registry on Windows, plist on macOS,
    /// IndexedDB on WebGL).
    /// <para><see cref="ClearAll"/> calls <c>PlayerPrefs.DeleteAll</c>, which wipes every PlayerPrefs key
    /// in the project — not just the ones written here.</para></summary>
    public class PlayerPrefsSaverSystem : ISaverSystem
    {
        public void Save(string key, string data)
        {
            PlayerPrefs.SetString(key, data);
            PlayerPrefs.Save();
        }

        public string Load(string key)
        {
            // PlayerPrefs.GetString returns "" for a missing key; the ISaverSystem contract is null.
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
        }

        public bool TryLoad(string key, out string result)
        {
            if (!PlayerPrefs.HasKey(key))
            {
                result = null;
                return false;
            }

            result = PlayerPrefs.GetString(key);
            return true;
        }

        public bool Exists(string key)
        {
            return PlayerPrefs.HasKey(key);
        }

        public void Delete(string key)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        public void ClearAll()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }
    }
}
