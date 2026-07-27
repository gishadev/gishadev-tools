using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using gishadev.tools.Extensions;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace gishadev.tools.Audio
{
    public class AudioManager : IAudioManager, IInitializable, IDisposable
    {
        [Inject] private AudioMasterSO _audioMasterData;

        public delegate void DelayedDelegate();

        public event Action<AudioData> AudioStarted;
        public event Action VolumeChanged;

        private static GameObject _audioParent;

        private float _masterVolumePercentage = 1f;
        private float _musicVolumePercentage = 1f;
        private float _sfxVolumePercentage = 1f;

        private CancellationTokenSource _delayFuncCts;
        private CancellationTokenSource _cts;

        public AudioMasterSO AudioMasterData => _audioMasterData;
        public float MasterVolumePercentage => _masterVolumePercentage;
        public float MusicVolumePercentage => _musicVolumePercentage;
        public float SFXVolumePercentage => _sfxVolumePercentage;

        public void Initialize()
        {
            if (_audioParent != null)
                return;
            Init();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public void Dispose()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _cts?.Cancel();
        }

        public void SetMasterVolume(float volumePercent)
        {
            _masterVolumePercentage = Mathf.Clamp01(volumePercent);

            ApplyVolume(GetAudioCollection<SFXData>());
            ApplyVolume(GetAudioCollection<MusicData>());
            VolumeChanged?.Invoke();
        }

        public void SetSFXVolume(float volumePercent)
        {
            _sfxVolumePercentage = Mathf.Clamp01(volumePercent);
            ApplyVolume(GetAudioCollection<SFXData>());
            VolumeChanged?.Invoke();
        }

        public void SetMusicVolume(float volumePercent)
        {
            _musicVolumePercentage = Mathf.Clamp01(volumePercent);
            ApplyVolume(GetAudioCollection<MusicData>());
            VolumeChanged?.Invoke();
        }

        public float GetEffectiveVolume(AudioData audioData)
        {
            var typeVolume = audioData is MusicData ? _musicVolumePercentage : _sfxVolumePercentage;
            return audioData.InitialVolume * typeVolume * _masterVolumePercentage;
        }

        private void ApplyVolume(IEnumerable<AudioData> collection)
        {
            foreach (var audio in collection)
                audio.AudioSource.volume = GetEffectiveVolume(audio);
        }

        public void PlaySFX(int index) => PlayAudio<SFXData>(index);
        public void PlayMusic(int index) => PlayAudio<MusicData>(index);

        public void PlayAudio<T>(int index) where T : AudioData, new()
        {
            var audioCollection = GetAudioCollection<T>();

            if (index < 0 || index > audioCollection.Length - 1)
            {
                Debug.LogError("There is no sfx with index " + index);
                return;
            }

            var data = audioCollection.ToArray()[index];
            data.Play();

            AudioStarted?.Invoke(data);

            Debug.Log($"I'm playing: {data.Name} of type {typeof(T)}");
        }

        #region Initialization

        private void Init()
        {
            _audioParent = new GameObject("[Audio Parent]");
            
            _delayFuncCts = new CancellationTokenSource();
            _cts = new CancellationTokenSource();
            _cts.RegisterRaiseCancelOnDestroy(_audioParent);
            
            Object.DontDestroyOnLoad(_audioParent);

            InitCollection(AudioMasterData.SFXCollection);
            InitCollection(AudioMasterData.MusicCollection);
        }

        private void InitCollection<T>(IEnumerable<T> collection) where T : AudioData, new()
        {
            // Init audio player.
            BaseAudioPlayer audioPlayer =
                typeof(T) == typeof(MusicData) ? new MusicPlayer(this) : new SFXPlayer(this);

            foreach (var audio in collection)
            {
                var child = new GameObject(audio.Name);
                child.transform.SetParent(_audioParent.transform);

                var audioSource = child.AddComponent<AudioSource>();
                audio.InitAudioSource(audioSource);
                audio.InitAudioPlayer(audioPlayer);
            }
        }

        #endregion

        public async UniTask FadeIn(AudioData audioData, CancellationTokenSource fadeCTS)
        {
            audioData.AudioSource.volume = 0f;
            var linkedCTS = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, fadeCTS.Token);

            var targetVolume = GetEffectiveVolume(audioData);
            while (!linkedCTS.IsCancellationRequested && audioData.AudioSource.volume < targetVolume)
            {
                var volume = audioData.AudioSource.volume +
                             targetVolume * Time.deltaTime / AudioMasterData.FadeTransitionTime;
                audioData.AudioSource.volume = Mathf.Min(volume, targetVolume);
                await UniTask.Yield(cancellationToken: linkedCTS.Token).SuppressCancellationThrow();
                targetVolume = GetEffectiveVolume(audioData);
            }
        }

        public async UniTask FadeOut(AudioData audioData, CancellationTokenSource fadeCTS)
        {
            var linkedCTS = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, fadeCTS.Token);

            while (!linkedCTS.IsCancellationRequested && audioData.AudioSource.volume > 0f)
            {
                var startVolume = GetEffectiveVolume(audioData);
                var volume = audioData.AudioSource.volume -
                             startVolume * Time.deltaTime / AudioMasterData.FadeTransitionTime;
                audioData.AudioSource.volume = Mathf.Max(volume, 0f);
                await UniTask.Yield(cancellationToken: linkedCTS.Token).SuppressCancellationThrow();
            }

            if (linkedCTS.IsCancellationRequested)
                return;

            audioData.AudioSource.Stop();
            audioData.AudioSource.volume = GetEffectiveVolume(audioData);
        }

        public async void DelayFunc(DelayedDelegate delayedDelegate, float delay)
        {
            var linkedCTS = CancellationTokenSource.CreateLinkedTokenSource(_delayFuncCts.Token, _cts.Token);
            await UniTask.WaitForSeconds(delay, cancellationToken: linkedCTS.Token).SuppressCancellationThrow();

            if (!linkedCTS.IsCancellationRequested)
                delayedDelegate();
        }

        public void CancelDelayFunc()
        {
            _delayFuncCts = _delayFuncCts.Renew();
        }

        private T[] GetAudioCollection<T>() where T : AudioData, new()
        {
            return typeof(T) == typeof(MusicData)
                ? AudioMasterData.MusicCollection.Cast<T>().ToArray()
                : AudioMasterData.SFXCollection.Cast<T>().ToArray();
        }

        private void OnSceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
        }
    }
}