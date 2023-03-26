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
			artist.text = $"<i>{songInfo.artistName}</i>";

			var score = ScoreManager.GetScore(songInfo);
			if (score == null || score.highestPercent.Count <= 0) {
				scoreText.text = "<alpha=#44>No Score";
			} else {
				var (instrument, highest) = score.GetHighestPercent();
				scoreText.text = $"<sprite name=\"{instrument}\"> <b>{highest.difficulty.ToChar()}</b> {Mathf.Floor(highest.percent * 100f):N0}%";
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