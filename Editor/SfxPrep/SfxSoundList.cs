using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace gishadev.tools.Editor.SfxPrep
{
    // Field names match the project's sound_list.json, which sfx_prep.py and the sfx-scout skill read too.

    /// <summary>
    /// The game's sound list: categories (with their processing and export settings) and every sound with its
    /// candidate files. Edited in the SFX Prep window, filled with candidates by sfx-scout and by dropped files.
    /// </summary>
    [Serializable]
    public class SfxSoundList
    {
        public string _doc;
        public List<SfxCategory> categories = new();
        public List<SfxSoundDef> sounds = new();

        public static SfxSoundList Load(string path)
        {
            if (!File.Exists(path))
                return null;

            try
            {
                // Older lists called the description "hook"; the other dropped fields (name, variations, priority,
                // status, source) are ignored and go on the next save.
                var json = File.ReadAllText(path).Replace("\"hook\":", "\"description\":");
                return JsonUtility.FromJson<SfxSoundList>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"SFX Prep: couldn't read {path}: {e.Message}");
                return null;
            }
        }

        public void Save(string path) => File.WriteAllText(path, JsonUtility.ToJson(this, true) + "\n");

        public SfxCategory Category(string id) => categories.FirstOrDefault(c => c.id == id);

        public SfxSoundDef Sound(string id) => sounds.FirstOrDefault(s => s.id == id);

        /// <summary>Sounds in display order: by category order, then their own order.</summary>
        public IEnumerable<SfxSoundDef> Ordered() =>
            sounds.OrderBy(s => categories.FindIndex(c => c.id == s.category) is var i && i >= 0 ? i : int.MaxValue)
                .ThenBy(s => s.order).ThenBy(s => s.id);
    }

    [Serializable]
    public class SfxCategory
    {
        public string id;
        public string folder;
        public string prefix = "sfx";
        public int channels = 1;
        public float lufs = -20f;
        public float max_len = 2.5f;

        // Loop sounds; 0 = the config's loop settings.
        public float loop_seconds;
        public float crossfade;

        // Import: clips longer than stream_after seconds stream (0 = never); Vorbis quality 0..1.
        public float stream_after;
        public float quality = 0.7f;
    }

    [Serializable]
    public class SfxSoundDef
    {
        public string id;
        public string category;
        public string type = "One-shot";
        public string description = "";
        public int order;

        // Processing overrides; 0 = the category's (or the global) default.
        public int channels;
        public float lufs;
        public float max_len;
        public float loop_seconds;
        public float crossfade;

        public List<SfxCandidate> candidates = new();
        public List<string> excluded = new();

        public bool IsLoop => type != null && type.StartsWith("Loop", StringComparison.OrdinalIgnoreCase);
    }

    [Serializable]
    public class SfxCandidate
    {
        public string file;
        public string path;
        public string library;
        public string year;
        public float seconds;
        public int channels;
        public string note;
        public bool user;
    }
}
