using System.Collections.Generic;
using System.Linq;
using Gisha.Effects.Audio;
using gishadev.tools.Audio;
using gishadev.tools.Core;
using gishadev.tools.Pooling;
using UnityEngine;

namespace gishadev.tools.Effects
{
    public class SFXEmitter : PoolManager<SFXPoolObject>, ISFXEmitter
    {
        private readonly IAudioManager _audioManager;
        private readonly List<AudioSource> _emittedSources = new();

        public SFXEmitter(IAudioManager audioManager)
        {
            _audioManager = audioManager;
        }

        protected override Transform Parent { get; set; }
        protected override List<SFXPoolObject> PoolObjectsCollection => PoolDataSO.SFXPoolObjects.ToList();

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
            _emittedSources.RemoveAll(x => x == null);
            foreach (var source in _emittedSources)
            {
                if (!source.gameObject.activeInHierarchy)
                    continue;

                var baseVolume = source.GetComponent<SFXBaseVolume>().GetBaseVolume(source);
                source.volume = baseVolume * _audioManager.SFXVolumePercentage * _audioManager.MasterVolumePercentage;
            }
        }

        public GameObject EmitAt(int index, Vector3 position, Quaternion rotation)
        {
            if (!TryInstantiate(index, out var obj))
                return null;

            obj.transform.position = position;
            obj.transform.rotation = rotation;

            var audioSource = obj.GetOrAddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            var poolObject = PoolObjectsCollection[index];
            if (poolObject.AudioClips.Length > 0)
                audioSource.clip = poolObject.AudioClips[Random.Range(0, poolObject.AudioClips.Length)];

            var baseVolume = obj.GetOrAddComponent<SFXBaseVolume>().GetBaseVolume(audioSource);
            audioSource.volume = baseVolume * _audioManager.SFXVolumePercentage * _audioManager.MasterVolumePercentage;

            if (!_emittedSources.Contains(audioSource))
                _emittedSources.Add(audioSource);

            audioSource.Play();
            obj.GetOrAddComponent<DisableSFXOnComplete>().StartTimer();

            return obj;
        }
    }
}