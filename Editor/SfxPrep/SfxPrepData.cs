using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace gishadev.tools.Editor.SfxPrep
{
    // Field names match the JSON written by sfx_prep.py (read with JsonUtility).

    /// <summary><c>Library/SfxPrep/prep_manifest.json</c>: every processed sound and its candidate clips.</summary>
    [Serializable]
    public class SfxPrepManifest
    {
        public string created;
        public string library_dir;
        public SfxPrepSound[] sounds = Array.Empty<SfxPrepSound>();

        public static SfxPrepManifest Load(string path)
        {
            if (!File.Exists(path))
                return null;

            try
            {
                return JsonUtility.FromJson<SfxPrepManifest>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogError($"SFX Prep: couldn't read {path}: {e.Message}");
                return null;
            }
        }
    }

    [Serializable]
    public class SfxPrepSound
    {
        public string id;
        public int count;
        public bool loop;
        public string processed;
        public string[] missing = Array.Empty<string>();
        public string[] skipped = Array.Empty<string>();
        public SfxPrepClip[] clips = Array.Empty<SfxPrepClip>();
    }

    [Serializable]
    public class SfxPrepClip
    {
        public string id;
        public string file;
        public string source;
        public string library;
        public string path;
        public bool user;
        public float at;
        public float seconds;
        public int channels;
        public float gain_db;
        public bool peak_limited;
        public float snr_db;
        public float clipped_src;
        public string load_type;
        public float quality;
        public float score;
        public bool keep;
    }

    /// <summary>
    /// Which clips you keep per sound, saved next to the cache. When a sound is processed again your ticks stay on
    /// the clips that still exist, and clips that are new (a file you just added) get the script's default tick.
    /// </summary>
    [Serializable]
    public class SfxPrepSelection
    {
        public List<Entry> sounds = new();

        [Serializable]
        public class Entry
        {
            public string id;
            public string processed;
            public List<string> keep = new();
            public List<string> seen = new();
        }

        public static SfxPrepSelection LoadAndReconcile(string path, SfxPrepManifest manifest)
        {
            SfxPrepSelection selection = null;
            if (File.Exists(path))
            {
                try
                {
                    selection = JsonUtility.FromJson<SfxPrepSelection>(File.ReadAllText(path));
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"SFX Prep: ignoring unreadable {path}: {e.Message}");
                }
            }

            selection ??= new SfxPrepSelection();
            foreach (var sound in manifest.sounds)
            {
                var entry = selection.Get(sound.id);
                if (entry == null)
                {
                    entry = new Entry { id = sound.id };
                    selection.sounds.Add(entry);
                }

                if (entry.processed != sound.processed)
                {
                    var seen = new HashSet<string>(entry.seen);
                    var kept = new HashSet<string>(entry.keep);
                    entry.keep = sound.clips
                        .Where(c => seen.Contains(c.id) ? kept.Contains(c.id) : c.keep)
                        .Select(c => c.id).ToList();
                    entry.seen = sound.clips.Select(c => c.id).ToList();
                    entry.processed = sound.processed;
                }
            }

            return selection;
        }

        public Entry Get(string soundId) => sounds.FirstOrDefault(e => e.id == soundId);

        public bool IsKept(SfxPrepSound sound, SfxPrepClip clip) => Get(sound.id)?.keep.Contains(clip.id) ?? false;

        public void SetKept(SfxPrepSound sound, SfxPrepClip clip, bool kept)
        {
            var entry = Get(sound.id);
            if (entry == null)
                return;

            var set = new HashSet<string>(entry.keep);
            if (kept)
                set.Add(clip.id);
            else
                set.Remove(clip.id);
            entry.keep = sound.clips.Where(c => set.Contains(c.id)).Select(c => c.id).ToList();
        }

        public void ResetToDefaults(SfxPrepSound sound)
        {
            var entry = Get(sound.id);
            if (entry != null)
                entry.keep = sound.clips.Where(c => c.keep).Select(c => c.id).ToList();
        }

        public void Save(string path) => File.WriteAllText(path, JsonUtility.ToJson(this, true));
    }
}
