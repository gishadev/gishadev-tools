using UnityEngine;

namespace gishadev.tools.Effects
{
    public interface IOtherEmitter
    {
        GameObject EmitAt(int index, Vector3 position, Quaternion? rotation = null);
        GameObject GetPrefab(int enumEntry);
    }
}