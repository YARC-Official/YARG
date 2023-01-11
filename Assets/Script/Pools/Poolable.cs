using UnityEngine;

namespace YARG.Pools {
	public abstract class Poolable : MonoBehaviour {
		public string poolId;

		[HideInInspector]
		public Pool pool;

		public void MoveToPool() {
			pool.Remove(this);
		}
	}
}