using System.Collections.Generic;
using UnityEngine;

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
        private readonly Stack<IPoolable> _pooled = new();

        [SerializeField]
        private GameObject _prefab;
        [SerializeField]
        private int _prewarmAmount = 15;

        private void Awake()
        {
            for (int i = 0; i < _prewarmAmount; i++)
            {
                _pooled.Push(CreateNew());
            }
        }

        private IPoolable CreateNew()
        {
            var gameObject = Instantiate(_prefab, transform);
            gameObject.SetActive(false);

            var poolable = gameObject.GetComponent<IPoolable>();
            poolable.ParentPool = this;

            return poolable;
        }

        public IPoolable TakeWithoutEnabling()
        {
            if (_pooled.TryPop(out var poolable))
            {
                return poolable;
            }

            poolable = CreateNew();
            return poolable;
        }

        public IPoolable Take()
        {
            var poolable = TakeWithoutEnabling();
            poolable.EnableFromPool();
            return poolable;
        }

        public void Return(IPoolable poolable)
        {
            poolable.DisableIntoPool();
            _pooled.Push(poolable);
        }
    }
}