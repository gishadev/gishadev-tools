using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using gishadev.tools.Extensions;
using UnityEngine.Events;

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
            var clip = data.AudioClips.GetRandomElement();
            if (clip != null)
                data.AudioSource.clip = clip;

            InitPlay(data).Forget();
        }

        // If we have auto-sequencing - start next audio clip when delay is over.
        private void HandleAutoSequencing(MusicData data)
        {
            _audioManager.CancelDelayFunc();

            if (data.AudioSource == null || !_audioManager.AudioMasterData.MusicAutoSequencing)
                return;

            // Nothing to sequence off of - the track length is what schedules the next one.
            if (data.AudioSource.clip == null || data.AudioClips.IsNullOrEmpty())
                return;

            _audioManager.DelayFunc(() =>
            {
                // FindIndex returns -1 when the playing clip isn't part of the collection
                // (e.g. it came from the prefab) - fall back to starting the list over.
                var oldIndex = Array.FindIndex(data.AudioClips, x => x == data.AudioSource.clip);
                data.AudioSource.clip = oldIndex < 0
                    ? data.AudioClips[0]
                    : data.AudioClips.GetNextValue(oldIndex);
                InitPlay(data).Forget();
            }, data.AudioSource.clip.length / data.AudioSource.pitch).Forget();
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

        private async UniTaskVoid InitPlay(MusicData newMusic)
        {
            _fadeCTS = _fadeCTS.Renew();

            if (_currentMusic != null)
            {
                if (_currentMusic.IsFade && !_fadeCTS.IsCancellationRequested)
                    await _audioManager.FadeOut(_currentMusic, _fadeCTS);
                else
                    _currentMusic.Stop();
            }

            _fadeCTS = _fadeCTS.Renew();

            if (newMusic.AudioSource != null)
            {
                newMusic.AudioSource.Play();

                if (newMusic.IsFade)
                    await _audioManager.FadeIn(newMusic, _fadeCTS);
                else
                    newMusic.AudioSource.volume = _audioManager.GetEffectiveVolume(newMusic);
            }

            _currentMusic = newMusic;

            _musicInitiated?.Invoke(_currentMusic);
        }
    }
}