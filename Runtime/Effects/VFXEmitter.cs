using System.Collections.Generic;
using System.Linq;
using gishadev.tools.Pooling;
using UnityEngine;

namespace gishadev.tools.Effects
{
    public class VFXEmitter : PoolManager<VFXPoolObject>, IVFXEmitter
    {
        protected override Transform Parent { get; set; }
        protected override List<VFXPoolObject> PoolObjectsCollection => PoolDataSO.VFXPoolObjects.ToList();

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