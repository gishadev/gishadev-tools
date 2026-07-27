using System.Collections.Generic;
using gishadev.tools.Pooling;
using UnityEngine;

namespace gishadev.tools.Effects
{
    public class VFXEmitter : PoolManager<VFXPoolObject>, IVFXEmitter
    {
        protected override Transform Parent { get; set; }
        protected override IReadOnlyList<VFXPoolObject> PoolObjectsCollection => PoolDataSO.VFXPoolObjects;

        public override void Initialize()
        {
            Parent = new GameObject("[VFXEmitter]").transform;
            base.Initialize();
        }

        public GameObject EmitAt(int index, Vector3 position, Quaternion? rotation = null)
        {
            if (!TryInstantiate(index, out var obj))
                return null;

            obj.transform.position = position;
            obj.transform.rotation = rotation ?? Quaternion.identity;

            return obj;
        }
    }
}