using UnityEngine;

namespace gishadev.tools.Effects
{
    public interface IVFXEmitter
    {
        GameObject EmitAt(int index, Vector3 position, Quaternion? rotation = null);
    }
}