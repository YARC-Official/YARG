using TMPro;
using UnityEngine;
using YARG.PlayMode;

namespace YARG.Pools {
	public class LyricComponent : Poolable {
		public TextMeshPro text;

		private void Update() {
			transform.localPosition -= new Vector3(Time.deltaTime * MicPlayer.TRACK_SPEED, 0f, 0f);

			if (transform.localPosition.x < -12f) {
				MoveToPool();
			}
		}
	}
}