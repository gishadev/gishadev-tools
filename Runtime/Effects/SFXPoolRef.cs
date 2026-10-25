using System;
using UnityEngine;

namespace gishadev.tools.Effects
{
    /// <summary>
    /// A pooled SFX (an entry of <c>PoolDataSO.SFXPoolObjects</c>) saved by its name, for serialized fields.
    /// Unlike a saved <c>SFXPoolEnum</c> it keeps pointing at the same sound when entries are added, removed or
    /// reordered. The inspector shows a drop-down of the pool's names.
    /// </summary>
    [Serializable]
    public struct SFXPoolRef
    {
        [SerializeField] private string name;

        public SFXPoolRef(string name) => this.name = name;

        public string Name => name;
        public bool IsSet => !string.IsNullOrEmpty(name);

        public override string ToString() => IsSet ? name : "(none)";
    }
}
