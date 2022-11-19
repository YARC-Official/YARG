using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace YARG.Pools {
	public class Pool : MonoBehaviour {
		[Serializable]
		public class KVP {
			public string id;
			public GameObject prefab;
		}

		[SerializeField]
		private List<KVP> poolablePrefabsInspector = new();

		private Dictionary<string, GameObject> poolablePrefabs = new();
		private Dictionary<string, List<Poolable>> pooled = new();

		private void Awake() {
			foreach (var kvp in poolablePrefabsInspector) {
				poolablePrefabs[kvp.id] = kvp.prefab;
			}
		}

		public void Remove(Poolable obj) {
			// Remove from world and add to list
			if (pooled.TryGetValue(obj.poolId, out var poolList)) {
				poolList.Add(obj);
			} else {
				pooled.Add(obj.poolId, new List<Poolable> {
					obj
				});
			}

			obj.gameObject.SetActive(false);
		}

		public Poolable Add(string id, Vector3 position) {
			// Get a disabled poolable
			if (pooled.TryGetValue(id, out var pool) && pool.Count >= 1) {
				var obj = pool.First();
				pool.Remove(obj);

				obj.transform.localPosition = position;
				obj.gameObject.SetActive(true);

				return obj;
			}

			if (!poolablePrefabs.TryGetValue(id, out var prefab)) {
				throw new InvalidOperationException($"Poolable prefab with ID `{id}` doesn't exist.");
			}

			// Create new poolable if not in pool
			var newObj = Instantiate(prefab, transform).GetComponent<Poolable>();
			newObj.transform.localPosition = position;
			newObj.transform.localRotation = prefab.transform.rotation;
			newObj.pool = this;

			return newObj;
		}
	}
}