using TMPro;
using UnityEngine;

namespace YARG.UI {
	public class SongInfoComponent : MonoBehaviour {
		[SerializeField]
		private TextMeshProUGUI text;
		[SerializeField]
		private TextMeshProUGUI rightText;

		public SongInfo songInfo;

		public void UpdateText() {
			text.text = $"<b>{songInfo.songName}</b> <color=grey>{songInfo.artistName}</color>";

			if (songInfo.songLength == null) {
				rightText.text = "N/A";
			} else {
				rightText.text = $"{songInfo.songLength:N0}s";
			}
		}
	}
}