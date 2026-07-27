using System.Collections.Generic;
using System.Linq;
using gishadev.tools.Pooling;
using UnityEngine;

namespace gishadev.tools.Effects
{
    public class OtherEmitter : PoolManager<OtherPoolObject>, IOtherEmitter
    {
        protected override Transform Parent { get; set; }
        protected override List<OtherPoolObject> PoolObjectsCollection => PoolDataSO.OtherPoolObjects.ToList();

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