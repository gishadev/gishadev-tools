using UnityEngine;

namespace gishadev.tools.Effects
{
    // Caches the authored AudioSource volume of a pooled SFX instance,
    // so volume scaling on reuse is always relative to the original value.
    [RequireComponent(typeof(AudioSource))]
    public class SFXBaseVolume : MonoBehaviour
    {
        private float _baseVolume;
        private bool _initialized;

        public float GetBaseVolume(AudioSource source)
        {
            if (!_initialized)
            {
                _baseVolume = source.volume;
                _initialized = true;
            }

            return _baseVolume;
        }
    }
}