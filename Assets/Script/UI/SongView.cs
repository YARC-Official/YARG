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

		public void UpdateSongView(SongInfo songInfo) {
			songName.text = songInfo.SongName;
			artist.text = $"<i>{songInfo.ArtistName}</i>";

			var score = ScoreManager.GetScore(songInfo);
			if (score == null) {
				scoreText.text = "<alpha=#44>No Score";
			} else {
				scoreText.text = $"{score.TotalHighestPercent * 100f:N0}%";
			}
		}
	}
}