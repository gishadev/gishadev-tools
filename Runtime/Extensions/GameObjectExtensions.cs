using UnityEngine;

namespace gishadev.tools.Extensions
{
    public static class GameObjectExtensions
    {
        public static T GetOrAddComponent<T>(this GameObject obj) where T : Component
        {
            return obj.TryGetComponent(out T component) ? component : obj.AddComponent<T>();
        }

        public static bool HasComponent<T>(this GameObject obj) where T : Component
        {
            return obj.TryGetComponent<T>(out _);
        }
    }
}
