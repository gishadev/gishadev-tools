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
    /// <summary>
    /// A <see cref="LifetimeScope"/> that injects every scene component with an [Inject] member.
    /// Subclasses overriding <see cref="Configure"/> must call <c>base.Configure(builder)</c> —
    /// the sweep runs as a build callback registered there, because a scope whose parent hasn't
    /// spawned yet defers its build and has no container during Awake.
    /// </summary>
    [Obsolete("Use GishadevToolsInstaller in your own LifetimeScope and register MonoBehaviours explicitly.", false)]
    public abstract class AutoInjectLifetimeScope : LifetimeScope
    {
        [SerializeField] private bool autoInjectScene = true;
        [SerializeField] private FindObjectsInactive includeInactive = FindObjectsInactive.Include;
        [SerializeField] private bool verboseLogging;

        private const BindingFlags MemberFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly Dictionary<Type, bool> InjectAttributeCache = new();

        private bool _initialSweepDone;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            if (autoInjectScene)
                builder.RegisterBuildCallback(InjectAllSceneObjects);
        }

        protected override void Awake()
        {
            base.Awake();

            // The sweep normally runs from the build callback above. This only covers a subclass
            // that overrode Configure without calling base — and only when the container already
            // exists, since a deferred scope has none yet.
            if (autoInjectScene && !_initialSweepDone && Container != null)
                InjectAllSceneObjects(Container);

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void InjectAllSceneObjects(IObjectResolver resolver)
        {
            _initialSweepDone = true;

            var injectables = FindObjectsByType(typeof(MonoBehaviour), includeInactive, FindObjectsSortMode.None)
                .Select(x => (MonoBehaviour)x)
                .Where(HasInjectAttribute);

            Inject(injectables, resolver);
        }

        private void InjectSceneObjects(Scene scene, IObjectResolver resolver)
        {
            bool inactive = includeInactive == FindObjectsInactive.Include;

            var injectables = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(inactive))
                .Where(HasInjectAttribute);

            Inject(injectables, resolver);
        }

        private void Inject(IEnumerable<MonoBehaviour> injectables, IObjectResolver resolver)
        {
            int count = 0;
            foreach (var injectable in injectables)
            {
                try
                {
                    resolver.Inject(injectable);
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
            // Container is still null if this scope's build is queued behind a parent that
            // hasn't spawned; the build callback sweeps every loaded scene once it does.
            if (autoInjectScene && Container != null)
                InjectSceneObjects(scene, Container);
        }

        private void OnSceneUnloaded(Scene arg0)
        {
        }
    }
}
