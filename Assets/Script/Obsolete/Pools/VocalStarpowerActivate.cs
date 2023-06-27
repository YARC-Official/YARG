using UnityEngine;
using YARG.PlayMode;

namespace YARG.Pools {
	public class VocalStarpowerActivate : Poolable {
		[SerializeField]
		private MeshRenderer meshRenderer;

		private float lengthCache;

		public void SetLength(float length) {
			length *= MicPlayer.trackSpeed / Play.speed;
			lengthCache = length;

			transform.localScale = transform.localScale.WithX(lengthCache);
			transform.localPosition = transform.localPosition.AddX(lengthCache / 2f);

			// Set material UV
			meshRenderer.material.SetFloat("Tiling", lengthCache);
		}

		private void Update() {
			meshRenderer.enabled = MicPlayer.Instance.StarpowerReady;

			transform.localPosition -= new Vector3(Time.deltaTime * MicPlayer.trackSpeed, 0f, 0f);

			if (transform.localPosition.x < -12f - (lengthCache / 2f)) {
				MoveToPool();
			}
		}
	}
}