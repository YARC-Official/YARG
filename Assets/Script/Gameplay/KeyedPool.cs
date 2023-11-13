using System.Collections.Generic;

namespace YARG.Gameplay
{
    public sealed class KeyedPool : Pool
    {
        // We effectively have a two-way dictionary here
        private Dictionary<object, IPoolable> _poolablesByKey;
        private Dictionary<IPoolable, object> _keysByPoolable;

        protected override void Awake()
        {
            base.Awake();

            _poolablesByKey = new();
            _keysByPoolable = new();
        }

        protected override void OnReturned(IPoolable poolable)
        {
            // Remove from both dictionaries (if present)
            if (!_keysByPoolable.TryGetValue(poolable, out var key)) return;
            _keysByPoolable.Remove(poolable);
            _poolablesByKey.Remove(key);
        }

        public IPoolable KeyedTakeWithoutEnabling(object key)
        {
            var poolable = TakeWithoutEnabling();
            if (poolable == null)
            {
                return null;
            }

            KeyPoolable(key, poolable);
            return poolable;
        }

        public IPoolable KeyedTake(object key)
        {
            var poolable = Take();
            if (poolable == null)
            {
                return null;
            }

            KeyPoolable(key, poolable);
            return poolable;
        }

        private void KeyPoolable(object key, IPoolable p)
        {
            // Skip if it's already keyed
            if (_poolablesByKey.ContainsKey(key)) return;

            _poolablesByKey.Add(key, p);
            _keysByPoolable.Add(p, key);
        }

        public IPoolable GetByKey(object key)
        {
            if (_poolablesByKey.TryGetValue(key, out var p))
            {
                return p;
            }

            return null;
        }

        public object GetByPoolable(IPoolable p)
        {
            if (_keysByPoolable.TryGetValue(p, out var key))
            {
                return key;
            }

            return null;
        }
    }
}