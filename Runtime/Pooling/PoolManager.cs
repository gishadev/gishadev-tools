using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace gishadev.tools.Pooling
{
    public abstract class PoolManager<T> : IInitializable, IDisposable where T : PoolObject
    {
        protected PoolDataSO PoolDataSO { get; }

        private readonly Dictionary<IPoolObject, List<GameObject>> _objectsByPoolObject = new();
        private readonly Dictionary<IPoolObject, Transform> _parentByPoolObject = new();

        protected abstract Transform Parent { get; set; }

        // Must not allocate - this is read on every emit. Implementations should return the
        // backing array from PoolDataSO directly rather than building a new collection.
        protected abstract IReadOnlyList<T> PoolObjectsCollection { get; }

        protected PoolManager(PoolDataSO poolDataSO)
        {
            PoolDataSO = poolDataSO;
        }

        public virtual void Initialize()
        {
            if (PoolDataSO == null)
            {
                Debug.LogError($"{GetType().Name}: no PoolDataSO given. Pass one to GishadevToolsInstaller " +
                               "(or assign it on GishadevToolsLifetimeScope).");
                return;
            }

            Object.DontDestroyOnLoad(Parent);
            ResetPools();

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public virtual void Dispose()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ResetPools();

        private void ResetPools()
        {
            foreach (var parent in _parentByPoolObject.Values)
                Object.Destroy(parent.gameObject);

            _objectsByPoolObject.Clear();
            _parentByPoolObject.Clear();

            var poolObjects = PoolObjectsCollection;
            for (int i = 0; i < poolObjects.Count; i++)
                RegisterPoolObject(poolObjects[i]);
        }

        protected bool TryInstantiate(int index, out GameObject emittedObj)
        {
            emittedObj = null;

            var poolObjects = PoolObjectsCollection;
            if (index < 0 || index >= poolObjects.Count)
            {
                Debug.LogError($"{GetType().Name}: no pool entry at index {index} " +
                               $"({poolObjects.Count} entries configured on {PoolDataSO?.name}).");
                return false;
            }

            var poolObject = poolObjects[index];
            if (poolObject == null)
                return false;

            // Objects are normally pre-registered by ResetPools, but register lazily too
            // in case the pool data changed after Initialize (e.g. edited mid-session).
            if (!_objectsByPoolObject.TryGetValue(poolObject, out var sceneObjects))
                sceneObjects = RegisterPoolObject(poolObject);
            else if (TryActivateAvailableObject(sceneObjects, out emittedObj))
                return true;

            var prefab = poolObject.GetPrefab();
            if (prefab == null)
            {
                Debug.LogError($"{GetType().Name}: pool entry '{poolObject.Name}' has no prefab assigned.");
                return false;
            }

            emittedObj = InstantiateNewObject(prefab, poolObject);
            return true;
        }

        #region Object Instantiating

        private GameObject InstantiateNewObject(GameObject prefab, IPoolObject poolObject)
        {
            Transform parent = _parentByPoolObject[poolObject];

            GameObject createdObject = Object.Instantiate(prefab, parent);
            _objectsByPoolObject[poolObject].Add(createdObject);

            return createdObject;
        }

        // Picks a random inactive instance - the pool can hold instances of different prefabs
        // when a pool entry has several, so which one gets reused is meaningful.
        private static bool TryActivateAvailableObject(List<GameObject> sceneObjects, out GameObject activated)
        {
            activated = null;

            int inactiveCount = 0;
            for (int i = 0; i < sceneObjects.Count; i++)
                if (!sceneObjects[i].activeInHierarchy)
                    inactiveCount++;

            if (inactiveCount == 0)
                return false;

            int target = Random.Range(0, inactiveCount);
            for (int i = 0; i < sceneObjects.Count; i++)
            {
                if (sceneObjects[i].activeInHierarchy || target-- > 0)
                    continue;

                activated = sceneObjects[i];
                activated.SetActive(true);
                return true;
            }

            return false;
        }

        #endregion

        private List<GameObject> RegisterPoolObject(IPoolObject poolObject)
        {
            var sceneObjects = new List<GameObject>();
            _objectsByPoolObject.Add(poolObject, sceneObjects);

            var name = $"[{poolObject.GetType().Name}_{poolObject.Name}]";
            var parent = new GameObject(name);
            parent.transform.SetParent(Parent);
            _parentByPoolObject.Add(poolObject, parent.transform);

            return sceneObjects;
        }
    }
}
