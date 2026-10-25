using UnityEngine;

namespace gishadev.tools.Effects
{
    /// <summary>
    /// The bump a surface makes when a <see cref="PhysicsSoundSource"/> (a cart, a crate) runs into it hard enough.
    /// Put it on the root of a fence, wall, gate...; it covers every collider below.
    /// </summary>
    public class SurfaceAudio : MonoBehaviour
    {
        [SerializeField] private SFXPoolRef bumpSound;

        public SFXPoolRef BumpSound => bumpSound;
    }
}
