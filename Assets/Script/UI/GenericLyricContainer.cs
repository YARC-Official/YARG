using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YARG.Data;
using YARG.PlayMode;

namespace YARG.UI {
	public class GenericLyricContainer : MonoBehaviour {
		private static List<GenericLyricInfo> LyricInfos => Play.Instance.chart.genericLyrics;

		[SerializeField]
		private TextMeshProUGUI _lyricText;

		private int _lyricIndex;
		private int _lyricPhraseIndex;

		private void Start() {
			_lyricText.text = string.Empty;

			// Temporary
			bool playingVocals = false;
			foreach (var player in PlayerManager.players) {
				if (player.chosenInstrument is "vocals" or "harmVocals") {
					playingVocals = true;
				}
			}

			// Disable if there are no lyrics or someone is singing
			if (LyricInfos.Count <= 0 || playingVocals) {
				gameObject.SetActive(false);
			}
		}

		private void Update() {
			if (_lyricIndex >= LyricInfos.Count) {
				return;
			}

			var lyric = LyricInfos[_lyricIndex];
			if (_lyricPhraseIndex >= lyric.lyric.Count && lyric.EndTime < Play.Instance.SongTime) {
				// Clear phrase
				_lyricText.text = string.Empty;

				_lyricPhraseIndex = 0;
				_lyricIndex++;
			} else if (_lyricPhraseIndex < lyric.lyric.Count && lyric.lyric[_lyricPhraseIndex].time < Play.Instance.SongTime) {
				// Consolidate lyrics
				string o = "<color=#5CB9FF>";
				for (int i = 0; i < lyric.lyric.Count; i++) {
					(_, string str) = lyric.lyric[i];

					if (str.EndsWith("-")) {
						o += str[..^1].Replace("=", "-");
					} else {
						o += str.Replace("=", "-") + " ";
					}

					if (i + 1 > _lyricPhraseIndex) {
						o += "</color>";
					}
				}

				_lyricText.text = o;
				_lyricPhraseIndex++;
			}
		}
	}
}