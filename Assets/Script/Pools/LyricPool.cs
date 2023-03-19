using System.Collections.Generic;
using UnityEngine;
using YARG.Data;
using YARG.PlayMode;

namespace YARG.Pools {
	public sealed class LyricPool : Pool {
		private List<GameObject> starpowerActivates = new();

		private float lastLyricLocation = float.NegativeInfinity;

		private void Update() {
			// Update lyric location
			lastLyricLocation -= Time.deltaTime * MicPlayer.TRACK_SPEED;
		}

		public Transform AddLyric(LyricInfo lyric, bool starpower, float x) {
			// Don't let lyrics collide
			if (x < lastLyricLocation) {
				x = lastLyricLocation + 0.125f;
			}

			var poolable = (VocalLyric) Add("lyric", new Vector3(x, 0f, -0.68f));
			poolable.SetLyric(lyric, starpower);

			// Calculate the location of the end of the lyric
			lastLyricLocation = x + poolable.text.preferredWidth;

			return poolable.transform;
		}

		public Transform AddStarpowerActivate(float x, float length) {
			// Don't let lyrics collide with this
			float newX = x;
			if (newX < lastLyricLocation) {
				newX = lastLyricLocation + 0.125f;
				length -= newX - x;
			}

			if (length < MicPlayer.STARPOWER_ACTIVATE_MIN) {
				return null;
			}

			var poolable = (VocalStarpowerActivate) Add("starpowerActivate", new Vector3(newX, -0.04f, -0.737f));
			poolable.SetLength(length);

			starpowerActivates.Add(poolable.gameObject);
			return poolable.transform;
		}

		public void RemoveAllStarpowerActivates() {
			foreach (var go in starpowerActivates) {
				Destroy(go);
			}
		}
	}
}