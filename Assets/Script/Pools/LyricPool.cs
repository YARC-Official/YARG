using UnityEngine;
using YARG.Data;
using YARG.PlayMode;

namespace YARG.Pools {
	public sealed class LyricPool : Pool {
		private float lastLyricLocation = float.NegativeInfinity;

		private void Update() {
			// Update lyric location
			lastLyricLocation -= Time.deltaTime * MicPlayer.TRACK_SPEED;
		}

		public Transform AddLyric(LyricInfo lyric, float x) {
			// Don't let lyrics collide
			if (x < lastLyricLocation) {
				x = lastLyricLocation + 0.125f;
			}

			var poolable = (VocalLyric) Add("lyric", new Vector3(x, 0f, -0.68f));
			poolable.SetLyric(lyric);

			// Calculate the location of the end of the lyric
			lastLyricLocation = x + poolable.text.preferredWidth;

			return poolable.transform;
		}
	}
}