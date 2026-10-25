using System;
using UnityEngine;

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

        float GetEffectiveVolume(AudioData audioData);

        void PlaySFX(int index);
        void PlayMusic(int index);

        /// <summary>Index of the SFX bank entry with this name; -1 when there is none.</summary>
        int IndexOfSFX(string name);

        /// <summary>
        /// A 2D looping sound from the SFX bank under <paramref name="parent"/> (null = scene root), silent until
        /// <see cref="SFXLoop.SetPlaying"/>. For 3D loops use <c>ISFXEmitter.CreateLoop</c>.
        /// </summary>
        SFXLoop CreateSFXLoop(int index, Transform parent = null);

        /// <summary>While true, every <see cref="SFXLoop"/> holds where it is (e.g. the game is paused).</summary>
        bool IsSFXPaused { get; }
        void SetSFXPaused(bool paused);
    }
}