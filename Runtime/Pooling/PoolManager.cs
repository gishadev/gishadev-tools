using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace gishadev.tools.Pooling
{
    public abstract class PoolManager<T> : IInitializable, IDisposable where T : PoolObject, new()
    {
        [Inject] protected PoolDataSO PoolDataSO { get; set; }

        private Dictionary<IPoolObject, List<GameObject>> _objectsByPoolObject = new();
        private Dictionary<IPoolObject, Transform> _parentByPoolObject = new();

        protected abstract Transform Parent { get; set; }
        protected abstract List<T> PoolObjectsCollection { get; }

        public virtual void Initialize()
        {
            Object.DontDestroyOnLoad(Parent);
            _objectsByPoolObject = new Dictionary<IPoolObject, List<GameObject>>();
            _parentByPoolObject = new Dictionary<IPoolObject, Transform>();

            InitializePools(PoolObjectsCollection);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public virtual void Dispose()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // Unload pools.
        private void OnSceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            foreach (var parent in _parentByPoolObject.Values)
                Object.Destroy(parent.gameObject);

            _objectsByPoolObject = new Dictionary<IPoolObject, List<GameObject>>();
            _parentByPoolObject = new Dictionary<IPoolObject, Transform>();

            InitializePools(PoolObjectsCollection);
        }

        protected bool TryInstantiate(int index, out GameObject emittedObj)
        {
            T[] collection;
            if (typeof(T) == typeof(SFXPoolObject))
                collection = PoolDataSO.SFXPoolObjects.Cast<T>().ToArray();
            else if (typeof(T) == typeof(VFXPoolObject))
                collection = PoolDataSO.VFXPoolObjects.Cast<T>().ToArray();
            else
                collection = PoolDataSO.OtherPoolObjects.Cast<T>().ToArray();

            var poolObj = collection[index];
            var prefab = poolObj.GetPrefab();
            emittedObj = null;

            if (poolObj.Equals(null))
                return false;

            if (_objectsByPoolObject.TryGetValue(poolObj, out var sceneObjectsList))
            {
                if (sceneObjectsList.Any(x => !x.activeInHierarchy))
                {
                    emittedObj = ActivateAvailableObject(sceneObjectsList);
                    return true;
                }
            }
            else
            {
                _objectsByPoolObject.Add(poolObj, new List<GameObject>());
                CreateObjectParent(poolObj);
            }

            emittedObj = InstantiateNewObject(prefab, poolObj);
            return true;
        }

        private void InitializePools(List<T> poolObjects)
        {
            foreach (var po in poolObjects)
            {
                _objectsByPoolObject.Add(po, new List<GameObject>());
                CreateObjectParent(po);
            }
        }

        #region Object Instantiating

        private GameObject InstantiateNewObject(GameObject prefab,
            IPoolObject po)
        {
            Transform parent = _parentByPoolObject[po];

            GameObject createdObject = Object.Instantiate(prefab, parent);
            _objectsByPoolObject[po].Add(createdObject);

            return createdObject;
        }

        private GameObject ActivateAvailableObject(List<GameObject> sceneObjectsList)
        {
            GameObject objectToActivate;
            if (sceneObjectsList.Count > 1)
            {
                List<GameObject> unactiveObjects = sceneObjectsList.Where(x => !x.activeInHierarchy).ToList();
                objectToActivate = unactiveObjects[UnityEngine.Random.Range(0, unactiveObjects.Count)];
            }
            else
                objectToActivate = sceneObjectsList.FirstOrDefault(x => !x.activeInHierarchy);

            objectToActivate.SetActive(true);

            return objectToActivate;
        }

        #endregion

        private void CreateObjectParent(IPoolObject poKey)
        {
            var name = $"[{poKey.GetType().Name}_{poKey.Name}]";
            var parent = new GameObject(name);
            parent.transform.SetParent(Parent);

            _parentByPoolObject.Add(poKey, parent.transform);
        }
    }
}