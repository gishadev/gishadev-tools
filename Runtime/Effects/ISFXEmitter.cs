using gishadev.tools.Audio;
using UnityEngine;

namespace gishadev.tools.Effects
{
    public interface ISFXEmitter
    {
        /// <param name="volume">0..1 on top of the pool prefab's volume (e.g. softer for a light bump).</param>
        /// <param name="pitchJitter">Random pitch offset each play (0.05 = 0.95..1.05), so repeats don't sound copy-pasted.</param>
        GameObject EmitAt(int index, Vector3 position, Quaternion? rotation = null, float volume = 1f,
            float pitchJitter = 0f);

        /// <summary>
        /// A looping sound from a pool entry under <paramref name="parent"/> (null = scene root): its clips, and the
        /// AudioSource settings of its prefab (3D range, mixer group...). Not pooled; silent until
        /// <see cref="SFXLoop.SetPlaying"/>.
        /// </summary>
        SFXLoop CreateLoop(int index, Transform parent = null);

        /// <summary>Index of the pool entry with this name; -1 when there is none.</summary>
        int IndexOf(string name);
    }
}