using System;

namespace gishadev.tools.Audio
{
    public interface IAudioManager
    {
        event Action<AudioData> AudioStarted;
        event Action VolumeChanged;

        float MasterVolumePercentage { get; set; }
        float MusicVolumePercentage { get; set; }
        float SFXVolumePercentage { get; set; }

        float GetEffectiveVolume(AudioData audioData);

        void PlaySFX(int index);
        void PlayMusic(int index);
    }
}