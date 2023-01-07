using TMPro;
using UnityEngine;
using YARG.Data;

namespace YARG.UI {
	public class SongView : MonoBehaviour {
		[SerializeField]
		private TextMeshProUGUI songName;
		[SerializeField]
		private TextMeshProUGUI artist;
		[SerializeField]
		private TextMeshProUGUI lengthText;

		public void UpdateSongView(SongInfo songInfo) {
			songName.text = songInfo.SongName;
			artist.text = $"<i>{songInfo.ArtistName}</i>";

			if (songInfo.songLength == null) {
				lengthText.text = "N/A";
			} else {
				int time = (int) songInfo.songLength.Value;
				int minutes = time / 60;
				int seconds = time % 60;

				lengthText.text = $"{minutes}:{seconds:00}";
			}
		}
	}
}