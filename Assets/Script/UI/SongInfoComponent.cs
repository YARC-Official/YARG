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
			text.text = $"<b>{songInfo.SongName}</b>";

			if (songInfo.songLength == null) {
				rightText.text = "N/A";
			} else {
				int time = (int) songInfo.songLength.Value;
				int minutes = time / 60;
				int seconds = time % 60;

				rightText.text = $"{minutes}:{seconds:00}";
			}
		}

		public void PlaySong() {
			MainMenu.Instance.chosenSong = songInfo;
			MainMenu.Instance.ShowPreSong();
		}
	}
}