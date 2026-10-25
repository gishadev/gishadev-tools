using System.Collections.Generic;
using gishadev.tools.Audio;
using gishadev.tools.Extensions;
using gishadev.tools.Pooling;
using UnityEngine;

namespace gishadev.tools.Effects
{
    public class SFXEmitter : PoolManager<SFXPoolObject>, ISFXEmitter
    {
        private readonly IAudioManager _audioManager;
        private readonly HashSet<AudioSource> _emittedSources = new();

        public SFXEmitter(PoolDataSO poolDataSO, IAudioManager audioManager) : base(poolDataSO)
        {
            _audioManager = audioManager;
        }

        protected override Transform Parent { get; set; }
        protected override IReadOnlyList<SFXPoolObject> PoolObjectsCollection => PoolDataSO.SFXPoolObjects;

        public override void Initialize()
        {
            Parent = new GameObject("[SFXEmitter]").transform;
            base.Initialize();
            _audioManager.VolumeChanged += OnVolumeChanged;
        }

        public override void Dispose()
        {
            _audioManager.VolumeChanged -= OnVolumeChanged;
            base.Dispose();
        }

        private void OnVolumeChanged()
        {
            _emittedSources.RemoveWhere(x => x == null);
            foreach (var source in _emittedSources)
            {
                if (!source.gameObject.activeInHierarchy)
                    continue;

                var baseVolume = source.GetComponent<SFXBaseVolume>().GetBaseVolume(source);
                source.volume = baseVolume * _audioManager.SFXVolumePercentage * _audioManager.MasterVolumePercentage;
            }
        }

        public GameObject EmitAt(int index, Vector3 position, Quaternion? rotation = null, float volume = 1f,
            float pitchJitter = 0f)
        {
            if (!TryInstantiate(index, out var obj))
                return null;

            obj.transform.position = position;
            obj.transform.rotation = rotation ?? Quaternion.identity;

            var audioSource = obj.GetOrAddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            var poolObject = PoolObjectsCollection[index];
            var clip = poolObject.AudioClips.GetRandomElement();
            if (clip != null)
                audioSource.clip = clip;

            if (audioSource.clip == null)
            {
                Debug.LogWarning($"SFXEmitter: pool entry '{poolObject.Name}' has no AudioClips assigned and its " +
                                 "prefab's AudioSource has no clip - nothing will play.", obj);
                return obj;
            }

            var baseVolume = obj.GetOrAddComponent<SFXBaseVolume>().GetBaseVolume(audioSource);
            audioSource.volume = baseVolume * Mathf.Clamp01(volume) * _audioManager.SFXVolumePercentage *
                                 _audioManager.MasterVolumePercentage;
            audioSource.pitch = 1f + (pitchJitter > 0f ? Random.Range(-pitchJitter, pitchJitter) : 0f);

            _emittedSources.Add(audioSource);

            audioSource.Play();
            obj.GetOrAddComponent<DisableSFXOnComplete>().StartTimer();

            return obj;
        }

        public SFXLoop CreateLoop(int index, Transform parent = null)
        {
            var poolObjects = PoolObjectsCollection;
            if (index < 0 || index >= poolObjects.Count)
            {
                Debug.LogError($"SFXEmitter: no pool entry at index {index} " +
                               $"({poolObjects.Count} entries configured on {PoolDataSO?.name}).");
                return null;
            }

            var poolObject = poolObjects[index];
            var prefab = poolObject.GetPrefab();
            var go = prefab != null ? Object.Instantiate(prefab, parent, false) : new GameObject();
            if (prefab == null)
                go.transform.SetParent(parent, false);
            go.name = $"[Loop] {poolObject.Name}";

            // A loop isn't a pooled one-shot: nothing may switch it off when its clip ends.
            if (go.TryGetComponent(out DisableSFXOnComplete disableOnComplete))
                disableOnComplete.enabled = false;

            var source = go.GetOrAddComponent<AudioSource>();
            var clip = poolObject.AudioClips.GetRandomElement();
            if (clip != null)
                source.clip = clip;
            // Loops ride along with moving things (a tool in hand, a rolling cart): no doppler warble.
            source.dopplerLevel = 0f;

            var loop = go.AddComponent<SFXLoop>();
            loop.Init(source, source.volume, _audioManager);
            return loop;
        }
    }
}
