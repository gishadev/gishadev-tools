using gishadev.tools.Audio;
using UnityEngine;

namespace gishadev.tools.Effects
{
    /// <summary>
    /// Plays sounds saved by name (<see cref="SFXPoolRef"/>, <see cref="SFXAudioRef"/>). An unset reference plays
    /// nothing; a name the pool or bank no longer has logs a warning and plays nothing.
    /// </summary>
    public static class SoundRefExtensions
    {
        public static GameObject EmitAt(this ISFXEmitter emitter, SFXPoolRef sound, Vector3 position,
            Quaternion? rotation = null, float volume = 1f, float pitchJitter = 0f)
        {
            int index = Resolve(sound.IsSet ? emitter.IndexOf(sound.Name) : -1, sound.Name, "SFX pool");
            return index < 0 ? null : emitter.EmitAt(index, position, rotation, volume, pitchJitter);
        }

        public static SFXLoop CreateLoop(this ISFXEmitter emitter, SFXPoolRef sound, Transform parent = null)
        {
            int index = Resolve(sound.IsSet ? emitter.IndexOf(sound.Name) : -1, sound.Name, "SFX pool");
            return index < 0 ? null : emitter.CreateLoop(index, parent);
        }

        public static void PlaySFX(this IAudioManager audio, SFXAudioRef sound)
        {
            int index = Resolve(sound.IsSet ? audio.IndexOfSFX(sound.Name) : -1, sound.Name, "SFX bank");
            if (index >= 0)
                audio.PlaySFX(index);
        }

        public static SFXLoop CreateSFXLoop(this IAudioManager audio, SFXAudioRef sound, Transform parent = null)
        {
            int index = Resolve(sound.IsSet ? audio.IndexOfSFX(sound.Name) : -1, sound.Name, "SFX bank");
            return index < 0 ? null : audio.CreateSFXLoop(index, parent);
        }

        private static int Resolve(int index, string name, string listName)
        {
            if (index < 0 && !string.IsNullOrEmpty(name))
                Debug.LogWarning($"gishadev.tools: no '{name}' in the {listName} (renamed or removed?).");
            return index;
        }
    }
}
