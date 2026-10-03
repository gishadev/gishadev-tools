using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using gishadev.tools.Extensions;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace gishadev.tools.Audio
{
    public class AudioManager : IAudioManager, IInitializable, IDisposable
    {
        private readonly AudioMasterSO _audioMasterData;

        public delegate void DelayedDelegate();

        public event Action<AudioData> AudioStarted;
        public event Action VolumeChanged;

        // Only used to warn about a second instance - see Initialize. Reset on SubsystemRegistration so
        // it starts at zero every Play session, even with domain reload disabled.
        private static int _initializedInstances;

        private GameObject _audioParent;
        private bool _isInitialized;

        private float _masterVolumePercentage = 1f;
        private float _musicVolumePercentage = 1f;
        private float _sfxVolumePercentage = 1f;

        private CancellationTokenSource _delayFuncCts;
        private CancellationTokenSource _cts;

        public AudioMasterSO AudioMasterData => _audioMasterData;
        public float MasterVolumePercentage => _masterVolumePercentage;
        public float MusicVolumePercentage => _musicVolumePercentage;
        public float SFXVolumePercentage => _sfxVolumePercentage;

        public AudioManager(AudioMasterSO audioMasterSO)
        {
            _audioMasterData = audioMasterSO;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _initializedInstances = 0;

        public void Initialize()
        {
            if (_audioMasterData == null)
            {
                Debug.LogError("AudioManager: no AudioMasterSO given. Pass one to GishadevToolsInstaller " +
                               "(or assign it on GishadevToolsLifetimeScope).");
                return;
            }

            if (_isInitialized)
                return;

            // Two managers each build their own players and play their own music on top of each other.
            // Still initialize this one - skipping it would leave it silently without players.
            if (_initializedInstances > 0)
                Debug.LogWarning("AudioManager: another AudioManager is already running. Register audio once, " +
                                 "in your root/project scope, or both will play.");

            Init();
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _delayFuncCts?.Cancel();

            if (!_isInitialized)
                return;

            _isInitialized = false;
            _initializedInstances--;

            if (_audioParent != null)
                Object.Destroy(_audioParent);
            _audioParent = null;
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

        private void ApplyVolume(AudioData[] collection)
        {
            for (int i = 0; i < collection.Length; i++)
                collection[i].AudioSource.volume = GetEffectiveVolume(collection[i]);
        }

        public void PlaySFX(int index) => PlayAudio<SFXData>(index);
        public void PlayMusic(int index) => PlayAudio<MusicData>(index);

        public void PlayAudio<T>(int index) where T : AudioData
        {
            var audioCollection = GetAudioCollection<T>();

            if (index < 0 || index >= audioCollection.Length)
            {
                Debug.LogError($"AudioManager: no {typeof(T).Name} at index {index} " +
                               $"({audioCollection.Length} entries configured on {_audioMasterData.name}).");
                return;
            }

            var data = audioCollection[index];
            data.Play();

            AudioStarted?.Invoke(data);
        }

        #region Initialization

        private void Init()
        {
            _isInitialized = true;
            _initializedInstances++;

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

        public async UniTaskVoid DelayFunc(DelayedDelegate delayedDelegate, float delay)
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

        // Returns the backing array as-is - this is read on every play/volume change,
        // so it must not allocate. MusicData[]/SFXData[] convert to AudioData[] covariantly.
        private AudioData[] GetAudioCollection<T>() where T : AudioData
        {
            return typeof(T) == typeof(MusicData)
                ? AudioMasterData.MusicCollection
                : AudioMasterData.SFXCollection;
        }
    }
}