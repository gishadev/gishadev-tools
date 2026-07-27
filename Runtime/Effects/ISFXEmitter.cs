using UnityEngine;

namespace gishadev.tools.Effects
{
    public interface ISFXEmitter
    {
        GameObject EmitAt(int index, Vector3 position, Quaternion? rotation = null);
    }
}