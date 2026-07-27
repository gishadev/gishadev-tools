using System.Collections.Generic;
using gishadev.tools.Pooling;
using UnityEngine;

namespace gishadev.tools.Effects
{
    public class OtherEmitter : PoolManager<OtherPoolObject>, IOtherEmitter
    {
        protected override Transform Parent { get; set; }
        protected override IReadOnlyList<OtherPoolObject> PoolObjectsCollection => PoolDataSO.OtherPoolObjects;

        public override void Initialize()
        {
            Parent = new GameObject("[OtherEmitter]").transform;
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
        public GameObject GetPrefab(int enumEntry) => PoolDataSO.OtherPoolObjects[enumEntry].GetPrefab();
    }
}