using UnityEngine;
using YARG.PlayMode;

namespace YARG.Pools {
	public class VocalNoteInharmonic : Poolable {
		[SerializeField]
		private MeshRenderer meshRenderer;

		private float lengthCache;

		public void SetLength(float length) {
			length *= MicPlayer.TRACK_SPEED / Play.speed;
			lengthCache = length;

			transform.localScale = transform.localScale.WithX(lengthCache);
			transform.localPosition = transform.localPosition.AddX(lengthCache / 2f);
		}

		public void SetHarmony(bool isHarmony) {
			if (isHarmony) {
				transform.localPosition = transform.localPosition.WithZ(0.005f);
				transform.localScale = transform.localScale.WithY(1.19f);
			} else {
				transform.localPosition = transform.localPosition.WithZ(0.1825f);
				transform.localScale = transform.localScale.WithY(1.54f);
			}
		}

		public void SetColor(int harmIndex) {
			var lineColor = meshRenderer.material.color;
			var harmColor = MicPlayer.HarmonicColors[harmIndex];
			harmColor.a = lineColor.a;

			meshRenderer.material.color = harmColor;
		}

		private void Update() {
			transform.localPosition -= new Vector3(Time.deltaTime * MicPlayer.TRACK_SPEED, 0f, 0f);

			if (transform.localPosition.x < -12f - (lengthCache / 2f)) {
				MoveToPool();
			}
		}
	}
}