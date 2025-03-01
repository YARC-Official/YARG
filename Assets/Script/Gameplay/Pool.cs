using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using YARG.Helpers.Extensions;

namespace YARG.Gameplay
{
    public interface IPoolable
    {
        public Pool ParentPool { get; set; }

        public void EnableFromPool();
        public void DisableIntoPool();
    }

    public class Pool : MonoBehaviour
    {
        // TODO: Reserialize everything and remove this
        [field: FormerlySerializedAs("_prefab")]
        [field: SerializeField]
        public GameObject Prefab { get; private set; }

        [SerializeField]
        private int _prewarmAmount = 300;
        [SerializeField]
        private int _objectCap = 500;

        private readonly Stack<IPoolable> _pooled = new();
        private readonly List<IPoolable> _spawnedObjects = new();

        private int TotalCount => _pooled.Count + _spawnedObjects.Count;

        public IReadOnlyList<IPoolable> AllSpawned => _spawnedObjects;

        protected virtual void Awake()
        {
            for (int i = 0; i < _prewarmAmount; i++)
            {
                _pooled.Push(CreateNew());
            }
        }

        public void SetPrefabAndReset(GameObject newPrefab)
        {
            Prefab = newPrefab;

            transform.DestroyChildren();
            _pooled.Clear();
            _spawnedObjects.Clear();

            // Re-prewarm
            for (int i = 0; i < _prewarmAmount; i++)
            {
                _pooled.Push(CreateNew());
            }
        }

        private IPoolable CreateNew()
        {
            if (TotalCount + 1 > _objectCap)
            {
                return null;
            }

            var gameObject = Instantiate(Prefab, transform);
            gameObject.SetActive(false);

            var poolable = gameObject.GetComponent<IPoolable>();
            poolable.ParentPool = this;

            return poolable;
        }

        public bool CanSpawnAmount(int count)
        {
            if (TotalCount + count <= _objectCap)
            {
                return true;
            }

            if (_pooled.Count >= count)
            {
                return true;
            }

            return false;
        }

        public IPoolable TakeWithoutEnabling()
        {
            if (_pooled.TryPop(out var poolable))
            {
                _spawnedObjects.Add(poolable);
                return poolable;
            }

            poolable = CreateNew();
            if (poolable != null)
            {
                _spawnedObjects.Add(poolable);
            }

            return poolable;
        }

        public IPoolable Take()
        {
            var poolable = TakeWithoutEnabling();
            if (poolable == null)
            {
                return null;
            }

            poolable.EnableFromPool();
            return poolable;
        }

        public void Return(IPoolable poolable)
        {
            // Skip if the stack already contains this poolable
            if (_pooled.Contains(poolable)) return;

            _spawnedObjects.Remove(poolable);

            poolable.DisableIntoPool();
            _pooled.Push(poolable);

            OnReturned(poolable);
        }

        public void ReturnAllObjects()
        {
            foreach (var poolable in _spawnedObjects.ToList())
            {
                Return(poolable);
            }
        }

        protected virtual void OnReturned(IPoolable poolable)
        {

        }
    }
}
