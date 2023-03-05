using UnityEngine;
using YARG.PlayMode;

namespace YARG.Pools {
	public class VocalNoteHarmonic : Poolable {
		[SerializeField]
		private LineRenderer lineRenderer;

		private float lengthCache;

		public void SetInfo(float note, int octave, float length) {
			// Get length
			length *= MicPlayer.TRACK_SPEED / Play.speed;
			lengthCache = length;

			// Set line position
			float z = MicPlayer.NoteAndOctaveToZ(note, octave);
			lineRenderer.SetPosition(0, new Vector3(1f / 15f, 0f, z));
			lineRenderer.SetPosition(1, new Vector3(length - 1f / 15f, 0f, z));
		}

		private void Update() {
			transform.localPosition -= new Vector3(Time.deltaTime * MicPlayer.TRACK_SPEED, 0f, 0f);

			if (transform.localPosition.x < -12f - lengthCache) {
				MoveToPool();
			}
		}
	}
}