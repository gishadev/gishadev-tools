using UnityEngine;

namespace gishadev.tools.Audio
{
    public class SFXPlayer : AudioPlayer<SFXData>
    {
        private readonly AudioManager _audioManager;

        public SFXPlayer(AudioManager audioManager)
        {
            _audioManager = audioManager;
        }

        public override void Play(SFXData data)
        {
            // Randomize clip.
            if (data.AudioClips.Length > 1)
                data.AudioSource.clip = data.AudioClips[Random.Range(0, data.AudioClips.Length)];

            data.AudioSource.volume = data.InitialVolume * _audioManager.SFXVolumePercentage;
            data.AudioSource.Play();
        }

        public override void Pause(SFXData data)
        {
            data.AudioSource.Pause();
        }

        public override void Stop(SFXData data)
        {
            data.AudioSource.Stop();
        }
    }
}