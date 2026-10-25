using System;
using UnityEngine;

namespace gishadev.tools.Audio
{
    /// <summary>
    /// An SFX of the bank (an entry of <c>AudioMasterSO.SFXCollection</c>) saved by its name, for serialized fields.
    /// Unlike a saved <c>SFXAudioEnum</c> it keeps pointing at the same sound when entries are added, removed or
    /// reordered. The inspector shows a drop-down of the bank's names.
    /// </summary>
    [Serializable]
    public struct SFXAudioRef
    {
        [SerializeField] private string name;

        public SFXAudioRef(string name) => this.name = name;

        public string Name => name;
        public bool IsSet => !string.IsNullOrEmpty(name);

        public override string ToString() => IsSet ? name : "(none)";
    }
}
