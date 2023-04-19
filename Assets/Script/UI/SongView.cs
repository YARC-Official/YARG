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
			Color songNameColor = new Color(107 / 255f, 227 / 255f, 243 / 255f, 1);
			songName.color = songNameColor;
			artist.text = $"<i>{songInfo.artistName}</i>";

			var score = ScoreManager.GetScore(songInfo);
			if (score == null || score.highestPercent.Count <= 0) {
				Color scoreColor = new Color(107 / 255f, 227 / 255f, 243 / 255f, 1);
				scoreText.color = scoreColor;
				scoreText.text = "<alpha=#44>No Score";
			} else {
				var (instrument, highest) = score.GetHighestPercent();
				scoreText.color = Color.white;
				scoreText.text = $"<sprite name=\"{instrument}\"> <b>{highest.difficulty.ToChar()}</b> {Mathf.Floor(highest.percent * 100f):N0}%";
			}
		}

		public void UpdateSongViewAsHeader(string header, string subheader) {
			background.SetActive(true);

			songName.text = $"<uppercase><b>{header}</b></uppercase>";
			songName.color = Color.white;
			artist.text = "";
			scoreText.text = $"<uppercase><b>{subheader}</b></uppercase>";
			Color categorySongs = new Color(97 / 255f, 180 / 255f, 252 / 255f, 1);
			scoreText.color = categorySongs;
		}
	}
}