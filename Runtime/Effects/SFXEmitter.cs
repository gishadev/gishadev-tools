using System.Collections.Generic;
using System.Linq;
using Gisha.Effects.Audio;
using gishadev.tools.Core;
using gishadev.tools.Pooling;
using UnityEngine;

namespace gishadev.tools.Effects
{
    public class SFXEmitter : PoolManager<SFXPoolObject>, ISFXEmitter
    {
        protected override Transform Parent { get; set; }
        protected override List<SFXPoolObject> PoolObjectsCollection => PoolDataSO.SFXPoolObjects.ToList();

        public override void Initialize()
        {
            Parent = new GameObject("[SFXEmitter]").transform;
            base.Initialize();
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
            
            audioSource.Play();
            obj.GetOrAddComponent<DisableSFXOnComplete>().StartTimer();

            return obj;
        }
    }
}