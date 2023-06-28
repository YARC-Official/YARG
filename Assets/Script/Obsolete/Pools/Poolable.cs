using UnityEngine;

namespace YARG.Pools
{
    public abstract class Poolable : MonoBehaviour
    {
        public string poolId;

        [HideInInspector]
        public Pool pool;

        [HideInInspector]
        public object data;

        public void MoveToPool()
        {
            pool.Remove(this);
        }
    }
}