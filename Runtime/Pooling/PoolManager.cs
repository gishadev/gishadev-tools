using System;
using System.Collections.Generic;
using System.Linq;
using gishadev.tools.Extensions;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace gishadev.tools.Pooling
{
    public abstract class PoolManager<T> : IInitializable, IDisposable where T : PoolObject
    {
        [Inject] protected PoolDataSO PoolDataSO { get; set; }

        private readonly Dictionary<IPoolObject, List<GameObject>> _objectsByPoolObject = new();
        private readonly Dictionary<IPoolObject, Transform> _parentByPoolObject = new();

        protected abstract Transform Parent { get; set; }
        protected abstract List<T> PoolObjectsCollection { get; }

        public virtual void Initialize()
        {
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

            foreach (var poolObject in PoolObjectsCollection)
            {
                _objectsByPoolObject.Add(poolObject, new List<GameObject>());
                CreateObjectParent(poolObject);
            }
        }

        protected bool TryInstantiate(int index, out GameObject emittedObj)
        {
            emittedObj = null;

            var poolObject = PoolObjectsCollection[index];
            if (poolObject == null)
                return false;

            // Objects are normally pre-registered by ResetPools, but register lazily too
            // in case the pool data changed after Initialize (e.g. edited mid-session).
            if (!_objectsByPoolObject.TryGetValue(poolObject, out var sceneObjects))
            {
                sceneObjects = new List<GameObject>();
                _objectsByPoolObject.Add(poolObject, sceneObjects);
                CreateObjectParent(poolObject);
            }
            else if (sceneObjects.Any(x => !x.activeInHierarchy))
            {
                emittedObj = ActivateAvailableObject(sceneObjects);
                return true;
            }

            emittedObj = InstantiateNewObject(poolObject.GetPrefab(), poolObject);
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

        private static GameObject ActivateAvailableObject(List<GameObject> sceneObjects)
        {
            var objectToActivate = sceneObjects.Where(x => !x.activeInHierarchy).ToList().GetRandomElement();
            objectToActivate.SetActive(true);

            return objectToActivate;
        }

        #endregion

        private void CreateObjectParent(IPoolObject poolObject)
        {
            var name = $"[{poolObject.GetType().Name}_{poolObject.Name}]";
            var parent = new GameObject(name);
            parent.transform.SetParent(Parent);

            _parentByPoolObject.Add(poolObject, parent.transform);
        }
    }
}
