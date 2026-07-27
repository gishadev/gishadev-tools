using gishadev.tools.Extensions;

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
            var clip = data.AudioClips.GetRandomElement();
            if (clip != null)
                data.AudioSource.clip = clip;

            data.AudioSource.volume = _audioManager.GetEffectiveVolume(data);
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