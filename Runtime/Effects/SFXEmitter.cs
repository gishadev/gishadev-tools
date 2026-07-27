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

        public SFXEmitter(IAudioManager audioManager)
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

        public GameObject EmitAt(int index, Vector3 position, Quaternion? rotation = null)
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
            audioSource.volume = baseVolume * _audioManager.SFXVolumePercentage * _audioManager.MasterVolumePercentage;

            _emittedSources.Add(audioSource);

            audioSource.Play();
            obj.GetOrAddComponent<DisableSFXOnComplete>().StartTimer();

            return obj;
        }
    }
}
