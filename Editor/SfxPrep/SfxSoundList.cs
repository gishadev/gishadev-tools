using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
                return JsonUtility.FromJson<SfxSoundList>(File.ReadAllText(path));
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
    }

    [Serializable]
    public class SfxSoundDef
    {
        public string id;
        public string name;
        public string category;
        public string type = "One-shot";
        public string variations = "1";
        public string priority = "P2";
        public string hook = "";
        public string status = "todo";
        public string source = "";
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

        /// <summary>How many variations it wants: '4–6' → 6, '1, pitch varies' → 1; loops keep one.</summary>
        public int TargetCount
        {
            get
            {
                if (IsLoop)
                    return 1;
                var numbers = Regex.Matches((variations ?? "1").Split(',')[0], @"\d+").Select(m => int.Parse(m.Value)).ToList();
                return numbers.Count > 0 ? numbers.Max() : 1;
            }
        }
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
