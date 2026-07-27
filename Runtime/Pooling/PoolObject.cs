using System.Linq;
using gishadev.tools.Core;
using gishadev.tools.Extensions;
using UnityEngine;

namespace gishadev.tools.Pooling
{
    [System.Serializable]
    public abstract class PoolObject : EnumEntryTarget, IPoolObject, IDropdownTargetData
    {
        [field: SerializeField] public string Name { get; private set; }
        [field: SerializeField] public GameObject[] Prefabs { get; private set; }
        public int[] InstanceIds => Prefabs.Select(x => x.GetInstanceID()).ToArray();

        public PoolObject(GameObject prefab)
        {
            Prefabs = new GameObject[1];
            Prefabs[0] = prefab;

            Name = Prefabs[0].name;
        }

        protected PoolObject()
        {
        }

        public GameObject GetPrefab()
        {
            return Prefabs.GetRandomElement();
        }
    }

    [System.Serializable]
    public class SFXPoolObject : PoolObject
    {
        [field: SerializeField, Tooltip("Variations of Audio")] public AudioClip[] AudioClips { get; private set; }
        
        public SFXPoolObject(GameObject prefab) : base(prefab)
        {
        }

        public SFXPoolObject()
        {
        }
    }

    [System.Serializable]
    public class VFXPoolObject : PoolObject
    {
        public VFXPoolObject(GameObject prefab) : base(prefab)
        {
        }
        
        public VFXPoolObject()
        {
        }
    }
    
    [System.Serializable]
    public class OtherPoolObject : PoolObject
    {
        public OtherPoolObject(GameObject prefab) : base(prefab)
        {
        }
        
        public OtherPoolObject()
        {
        }
    }
}