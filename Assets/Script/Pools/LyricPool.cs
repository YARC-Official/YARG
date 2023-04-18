using System.Collections.Generic;
using UnityEngine;
using YARG.Data;
using YARG.PlayMode;

namespace YARG.Pools {
	public sealed class LyricPool : Pool {
		private const float TEXT_SPACING = 0.25f;

		private List<GameObject> starpowerActivates = new();

		private float lastLyricLocationBottom = float.NegativeInfinity;
		private float lastLyricLocationTop = float.NegativeInfinity;

		private void Update() {
			// Update lyric location
			lastLyricLocationBottom -= Time.deltaTime * MicPlayer.TRACK_SPEED;
			lastLyricLocationTop -= Time.deltaTime * MicPlayer.TRACK_SPEED;
		}

		public Transform AddLyric(LyricInfo lyric, bool starpower, float x, bool onTop) {
			// Don't let lyrics collide
			if (onTop) {
				if (x < lastLyricLocationTop) {
					x = lastLyricLocationTop + TEXT_SPACING;
				}
			} else {
				if (x < lastLyricLocationBottom) {
					x = lastLyricLocationBottom + TEXT_SPACING;
				}
			}

			var poolable = (VocalLyric) Add("lyric", new Vector3(x, 0f, onTop ? 0.81f : -0.758f));
			poolable.SetLyric(lyric, starpower);

			// Calculate the location of the end of the lyric
			if (onTop) {
				lastLyricLocationTop = x + poolable.text.preferredWidth;
			} else {
				lastLyricLocationBottom = x + poolable.text.preferredWidth;
			}

			return poolable.transform;
		}

		public Transform AddStarpowerActivate(float x, float length, bool onTop) {
			// Don't let lyrics collide with this
			float newX = x;
			if (onTop) {
				if (newX < lastLyricLocationTop) {
					newX = lastLyricLocationTop + 0.125f;
					length -= newX - x;
				}
			} else {
				if (newX < lastLyricLocationBottom) {
					newX = lastLyricLocationBottom + 0.125f;
					length -= newX - x;
				}
			}

			if (length < MicPlayer.STARPOWER_ACTIVATE_MIN) {
				return null;
			}

			var poolable = (VocalStarpowerActivate) Add("starpowerActivate", new Vector3(newX, -0.04f, onTop ? 0.775f : -0.815f));
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