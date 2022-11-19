using UnityEngine;
using YARG.Pools;

namespace YARG {
	public class BeatLine : Poolable {
		private void Update() {
			transform.localPosition -= new Vector3(0f, 0f, Time.deltaTime * Game.Instance.SongSpeed);

			if (transform.localPosition.z < -3f) {
				MoveToPool();
			}
		}
	}
}