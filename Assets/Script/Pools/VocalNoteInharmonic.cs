using UnityEngine;
using YARG.PlayMode;

namespace YARG.Pools {
	public class VocalNoteInharmonic : Poolable {
		private float lengthCache;

		public void SetLength(float length) {
			length *= MicPlayer.TRACK_SPEED / Play.speed;
			lengthCache = length;

			transform.localScale = transform.localScale.WithX(lengthCache);
			transform.localPosition = transform.localPosition.AddX(lengthCache / 2f);
		}

		private void Update() {
			transform.localPosition -= new Vector3(Time.deltaTime * MicPlayer.TRACK_SPEED, 0f, 0f);

			if (transform.localPosition.x < -12f - (lengthCache / 2f)) {
				MoveToPool();
			}
		}
	}
}