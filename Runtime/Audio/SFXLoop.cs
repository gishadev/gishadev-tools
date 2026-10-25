using UnityEngine;

namespace gishadev.tools.Audio
{
    /// <summary>
    /// A looping sound: a hose hiss, a rolling cart, wind. Made by <c>ISFXEmitter.CreateLoop</c> (3D, with the pool
    /// entry's prefab settings) or <c>IAudioManager.CreateSFXLoop</c> (2D, from the bank). Silent until
    /// <see cref="SetPlaying"/>; fades to the level you ask for, follows the master/SFX volume and holds while
    /// <see cref="IAudioManager.IsSFXPaused"/>. Lives on its own GameObject: destroyed with its parent or by
    /// <see cref="Dispose"/>.
    /// </summary>
    public class SFXLoop : MonoBehaviour
    {
        // Volume (0..1) per second: fast enough to follow a trigger, slow enough not to click.
        private const float FadeSpeed = 6f;

        private AudioSource _source;
        private IAudioManager _audio;
        private float _baseVolume;

        private bool _playing;
        private float _level = 1f;
        private float _shown;
        private bool _paused;

        public AudioSource Source => _source;
        public bool IsPlaying => _playing;

        /// <param name="level">0..1 loudness while playing; ignored when stopping.</param>
        public void SetPlaying(bool playing, float level = 1f)
        {
            _playing = playing;
            if (playing)
                _level = Mathf.Clamp01(level);
        }

        public void SetPitch(float pitch)
        {
            if (_source != null)
                _source.pitch = pitch;
        }

        public void Dispose()
        {
            if (this != null)
                Destroy(gameObject);
        }

        internal void Init(AudioSource source, float baseVolume, IAudioManager audio)
        {
            _source = source;
            _baseVolume = baseVolume;
            _audio = audio;

            _source.playOnAwake = false;
            _source.loop = true;
            _source.volume = 0f;
            _source.Stop();
        }

        private void Update()
        {
            if (_source == null)
                return;

            bool paused = _audio != null && _audio.IsSFXPaused;
            if (paused != _paused)
            {
                _paused = paused;
                if (paused)
                    _source.Pause();
                else
                    _source.UnPause();
            }

            if (paused)
                return;

            // Unscaled: fades still finish while the game runs at timeScale 0.
            _shown = Mathf.MoveTowards(_shown, _playing ? _level : 0f, FadeSpeed * Time.unscaledDeltaTime);
            float volumeScale = _audio != null ? _audio.SFXVolumePercentage * _audio.MasterVolumePercentage : 1f;
            _source.volume = _shown * _baseVolume * volumeScale;

            if (_shown <= 0f)
            {
                if (_source.isPlaying)
                    _source.Stop();
                return;
            }

            // Also restarts after the object was hidden and shown again (a disabled source stops).
            if (!_source.isPlaying && _source.clip != null)
            {
                // Random start: two loops of the same clip never play in phase.
                _source.time = Random.Range(0f, _source.clip.length * 0.9f);
                _source.Play();
            }
        }
    }
}
