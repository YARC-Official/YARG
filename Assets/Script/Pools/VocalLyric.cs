using TMPro;
using UnityEngine;
using YARG.Data;
using YARG.PlayMode;

namespace YARG.Pools {
	public class VocalLyric : Poolable {
		public TextMeshPro text;

		private LyricInfo lyric;
		private bool starpower;

		public void SetLyric(LyricInfo lyricInfo, bool starpower) {
			lyric = lyricInfo;
			this.starpower = starpower;

			// I like this line of code
			text.text = lyric.lyric;
		}

		private void Update() {
			transform.localPosition -= new Vector3(Time.deltaTime * MicPlayer.TRACK_SPEED, 0f, 0f);

			if (transform.localPosition.x < -12f) {
				MoveToPool();
			}

			// Set color
			if (Play.Instance.SongTime < lyric.time) {
				if (starpower) {
					text.color = Color.yellow;
				} else {
					text.color = Color.white;
				}
			} else if (Play.Instance.SongTime > lyric.time && Play.Instance.SongTime < lyric.EndTime) {
				text.color = new Color(0.0549f, 0.6431f, 0.9765f);
			} else {
				text.color = new Color(0.349f, 0.349f, 0.349f);
			}
		}
	}
}