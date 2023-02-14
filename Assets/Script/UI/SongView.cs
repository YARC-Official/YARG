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
		private TextMeshProUGUI scoreText;
		[SerializeField]
		private GameObject background;

		public void UpdateSongView(SongInfo songInfo) {
			background.SetActive(false);

			songName.text = songInfo.SongName;
			artist.text = $"<i>{songInfo.ArtistName}</i>";

			var score = ScoreManager.GetScore(songInfo);
			if (score == null) {
				scoreText.text = "<alpha=#44>No Score";
			} else {
				scoreText.text = $"{score.TotalHighestPercent * 100f:N0}%";
			}
		}

		public void UpdateSongViewAsHeader(string header, string subheader) {
			background.SetActive(true);

			songName.text = $"<b>{header}</b>";
			artist.text = subheader;
			scoreText.text = "";
		}
	}
}