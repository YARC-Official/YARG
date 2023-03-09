using TMPro;
using UnityEngine;
using YARG.PlayMode;

namespace YARG.Pools {
	public sealed class LyricPool : Pool {
		private float lastLyricLocation = float.NegativeInfinity;

		private void Update() {
			// Update lyric location
			lastLyricLocation -= Time.deltaTime * MicPlayer.TRACK_SPEED;
		}

		public Transform AddLyric(string text, float x) {
			// Don't let lyrics collide
			if (x < lastLyricLocation) {
				x = lastLyricLocation + 0.125f;
			}

			var poolable = Add("lyric", new Vector3(x, 0f, -0.68f));

			var tmp = poolable.GetComponent<TextMeshPro>();
			tmp.text = text;

			// Calculate the location of the end of the lyric
			lastLyricLocation = x + tmp.preferredWidth;

			return poolable.transform;
		}
	}
}