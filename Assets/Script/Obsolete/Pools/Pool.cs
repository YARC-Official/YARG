using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace YARG.Pools
{
    public class Pool : MonoBehaviour
    {
        public PlayerManager.Player player;

        /// <summary>
        /// Unity inspector class ('cause we can't use dictionaries for some reason).
        /// </summary>
        [Serializable]
        public class KVP
        {
            public string id;
            public GameObject prefab;
        }

        [SerializeField]
        protected List<KVP> poolablePrefabsInspector = new();

        protected Dictionary<string, GameObject> poolablePrefabs = new();

        protected Dictionary<string, List<Poolable>> pooled = new();
        protected Dictionary<string, List<Poolable>> active = new();

        private void Awake()
        {
            // Convert inspector dictionary to real dictionary
            foreach (var kvp in poolablePrefabsInspector)
            {
                poolablePrefabs[kvp.id] = kvp.prefab;
            }

            // Create pools for each object
            foreach (var kvp in poolablePrefabs)
            {
                pooled.Add(kvp.Key, new List<Poolable>());
                active.Add(kvp.Key, new List<Poolable>());
            }
        }

        public void Remove(Poolable obj)
        {
            // Remove from "active" list (guarenteed to exist)
            active[obj.poolId].Remove(obj);

            // Add to pooled list (guarenteed to exist)
            pooled[obj.poolId].Add(obj);
            OnPooled(obj);

            // Disable
            obj.gameObject.SetActive(false);
        }

        public Poolable Add(string id, Vector3 position)
        {
            if (!poolablePrefabs.TryGetValue(id, out var prefab))
            {
                throw new InvalidOperationException($"Poolable prefab with ID `{id}` doesn't exist.");
            }

            var poolList = pooled[id];
            var activeList = active[id];

            // Get a disabled poolable
            if (poolList.Count >= 1)
            {
                var obj = poolList.First();

                // Remove from pool and add to active
                poolList.Remove(obj);
                activeList.Add(obj);
                OnActive(obj);

                obj.transform.localPosition = position;
                obj.gameObject.SetActive(true);

                return obj;
            }

            // Create new poolable if not in pool
            var newObj = Instantiate(prefab, transform).GetComponent<Poolable>();
            newObj.transform.localPosition = position;
            newObj.transform.localRotation = prefab.transform.rotation;
            newObj.pool = this;

            // Add to active
            activeList.Add(newObj);
            OnActive(newObj);

            return newObj;
        }

        protected virtual void OnPooled(Poolable poolable)
        {
        }

        protected virtual void OnActive(Poolable poolable)
        {
        }
    }
}