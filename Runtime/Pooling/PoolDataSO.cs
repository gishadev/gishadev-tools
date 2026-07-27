using System.Collections.Generic;
using System.Linq;
using gishadev.tools.Core;
using UnityEngine;

namespace gishadev.tools.Pooling
{
    [CreateAssetMenu(fileName = "PoolData", menuName = "ScriptableObjects/PoolData")]
    public class PoolDataSO : ScriptableObjectEnumsGenerator, IDropdownHolder
    {
        [field: SerializeField] public SFXPoolObject[] SFXPoolObjects { get; private set; }
        [field: SerializeField] public VFXPoolObject[] VFXPoolObjects { get; private set; }
        [field: SerializeField] public OtherPoolObject[] OtherPoolObjects { get; private set; }

#if UNITY_EDITOR
        // Enum auto generation method.
        public override void OnCollectionChanged()
        {
            InitEnumForCollection(SFXPoolObjects, SFXPoolObjects.Select(x => x.Name), Constants.POOL_SFX_ENUM_NAME);
            InitEnumForCollection(VFXPoolObjects, VFXPoolObjects.Select(x => x.Name), Constants.POOL_VFX_ENUM_NAME);
            InitEnumForCollection(OtherPoolObjects, OtherPoolObjects.Select(x => x.Name), Constants.POOL_OTHER_ENUM_NAME);
        }
#endif

        public void OnDragNDropped<T, U>(U importKeyObject, IEnumerable<T> targetCollection)
            where T : IDropdownTargetData, new()
            where U : class
        {
            var tempCollection = targetCollection.ToList();
            var newData = InstanceCreator.CreateInstanceWithArgs<T>(importKeyObject);
            tempCollection.Add(newData);

            if (typeof(T) == typeof(SFXPoolObject))
                SFXPoolObjects = tempCollection.Cast<SFXPoolObject>().ToArray();
            if (typeof(T) == typeof(VFXPoolObject))
                VFXPoolObjects = tempCollection.Cast<VFXPoolObject>().ToArray();
            if (typeof(T) == typeof(OtherPoolObject))
                OtherPoolObjects = tempCollection.Cast<OtherPoolObject>().ToArray();
        }
    }
}