using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using gishadev.tools.Core;
using UnityEngine.Events;
using Random = UnityEngine.Random;

namespace gishadev.tools.Audio
{
    public class MusicPlayer : AudioPlayer<MusicData>
    {
        private readonly AudioManager _audioManager;
        private MusicData _currentMusic;

        private readonly UnityEvent<MusicData> _musicInitiated = new();
        private CancellationTokenSource _fadeCTS;

        public MusicPlayer(AudioManager audioManager)
        {
            _audioManager = audioManager;
            _musicInitiated.AddListener(HandleAutoSequencing);
        }

        public override void Play(MusicData data)
        {
            // Randomize clip.
            if (data.AudioClips.Length > 1)
                data.AudioSource.clip = data.AudioClips[Random.Range(0, data.AudioClips.Length)];

            InitPlay(data);
        }

        // If we have auto-sequencing - start next audio clip when delay is over. 
        private void HandleAutoSequencing(MusicData data)
        {
            _audioManager.CancelDelayFunc();
            if (data.AudioSource != null && _audioManager.AudioMasterData.MusicAutoSequencing)
                _audioManager.DelayFunc(() =>
                {
                    var oldIndex = Array.FindIndex(data.AudioClips, x => x == data.AudioSource.clip);
                    var nextValue = data.AudioClips.GetNextValue(oldIndex);
                    data.AudioSource.clip = nextValue;
                    InitPlay(data);
                }, data.AudioSource.clip.length / data.AudioSource.pitch);
        }

        public override void Pause(MusicData data)
        {
            data.AudioSource.Pause();
        }

        public override void Stop(MusicData data)
        {
            data.AudioSource.Stop();
            _audioManager.CancelDelayFunc();
        }

        private async void InitPlay(MusicData newMusic)
        {
            _fadeCTS?.Cancel();
            _fadeCTS = new CancellationTokenSource();

            if (_currentMusic != null)
            {
                if (_currentMusic.IsFade && !_fadeCTS.IsCancellationRequested)
                    await _audioManager.FadeOut(_currentMusic, _fadeCTS);
                else
                    _currentMusic.Stop();
            }

            _fadeCTS?.Cancel();
            _fadeCTS = new CancellationTokenSource();

            if (newMusic.AudioSource != null)
            {
                newMusic.AudioSource.Play();

                if (newMusic.IsFade)
                    await _audioManager.FadeIn(newMusic, _fadeCTS);
                else
                    newMusic.AudioSource.volume = newMusic.InitialVolume * _audioManager.MusicVolumePercentage;
            }

            _currentMusic = newMusic;

            _musicInitiated?.Invoke(_currentMusic);
        }
    }
}