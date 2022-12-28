using TMPro;
using UnityEngine;
using YARG.Data;

namespace YARG.UI {
	public class SongInfoComponent : MonoBehaviour {
		[SerializeField]
		private TextMeshProUGUI songName;
		[SerializeField]
		private TextMeshProUGUI artist;
		[SerializeField]
		private TextMeshProUGUI lengthText;

		public SongInfo songInfo;

		public void UpdateText() {
			songName.text = $"<b>{songInfo.SongName}</b>";
			artist.text = songInfo.artistName;

			if (songInfo.songLength == null) {
				lengthText.text = "N/A";
			} else {
				int time = (int) songInfo.songLength.Value;
				int minutes = time / 60;
				int seconds = time % 60;

				lengthText.text = $"{minutes}:{seconds:00}";
			}
		}

		public void PlaySong() {
			if (songInfo.songLength == null) {
				return;
			}

			MainMenu.Instance.chosenSong = songInfo;
			MainMenu.Instance.ShowPreSong();
		}
	}
}