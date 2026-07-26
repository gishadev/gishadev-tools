using System;

namespace gishadev.tools.Audio
{
    public interface IAudioManager
    {
        event Action<AudioData> AudioStarted;
        event Action VolumeChanged;

        float MasterVolumePercentage { get; }
        float MusicVolumePercentage { get; }
        float SFXVolumePercentage { get; }

        void SetMasterVolume(float volumePercent);
        void SetSFXVolume(float volumePercent);
        void SetMusicVolume(float volumePercent);

        void PlaySFX(int index);
        void PlayMusic(int index);
    }
}