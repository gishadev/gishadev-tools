using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace gishadev.tools.Infrastructure
{
    public abstract class AutoInjectLifetimeScope : LifetimeScope
    {
        [SerializeField] private bool autoInjectScene = true;
        [SerializeField] private FindObjectsInactive includeInactive = FindObjectsInactive.Include;
        [SerializeField] private bool verboseLogging;

        private const BindingFlags MemberFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly Dictionary<Type, bool> InjectAttributeCache = new();

        protected override void Awake()
        {
            base.Awake();

            if (autoInjectScene)
                InjectAllSceneObjects();

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void InjectAllSceneObjects()
        {
            var injectables = FindObjectsByType(typeof(MonoBehaviour), includeInactive, FindObjectsSortMode.None)
                .Select(x => (MonoBehaviour)x)
                .Where(HasInjectAttribute);

            Inject(injectables);
        }

        private void InjectSceneObjects(Scene scene)
        {
            bool inactive = includeInactive == FindObjectsInactive.Include;

            var injectables = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(inactive))
                .Where(HasInjectAttribute);

            Inject(injectables);
        }

        private void Inject(IEnumerable<MonoBehaviour> injectables)
        {
            int count = 0;
            foreach (var injectable in injectables)
            {
                try
                {
                    Container.Inject(injectable);
                    count++;
                }
                catch (VContainerException ex)
                {
                    // Expected when nested: the dependency lives in a child container, which a parent
                    // can't see. The child scope's own sweep resolves it, so this one steps aside.
                    if (verboseLogging)
                        Debug.LogWarning($"Skipped {injectable.GetType().Name} on {injectable.gameObject.name}: " +
                                         $"{ex.InvalidType} is not registered in this scope.", injectable);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to inject {injectable.GetType().Name} on {injectable.gameObject.name}: {ex}",
                        injectable);
                }
            }

            if (verboseLogging)
                Debug.Log($"Auto-injected {count} scene components", this);
        }

        // Helper method to check if a MonoBehaviour has any [Inject] fields/properties/methods
        private static bool HasInjectAttribute(MonoBehaviour mb)
        {
            if (mb == null)
                return false;

            var type = mb.GetType();
            if (InjectAttributeCache.TryGetValue(type, out bool cached))
                return cached;

            bool hasInject = type.GetFields(MemberFlags).Any(HasInjectAttribute) ||
                             type.GetProperties(MemberFlags).Any(HasInjectAttribute) ||
                             type.GetMethods(MemberFlags).Any(HasInjectAttribute);

            InjectAttributeCache[type] = hasInject;
            return hasInject;
        }

        private static bool HasInjectAttribute(MemberInfo member) =>
            member.GetCustomAttributes(typeof(InjectAttribute), true).Length > 0;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (autoInjectScene)
                InjectSceneObjects(scene);
        }

        private void OnSceneUnloaded(Scene arg0)
        {
        }
    }
}
