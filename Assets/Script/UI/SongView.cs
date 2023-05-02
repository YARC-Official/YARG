using TMPro;
using UnityEngine;
using YARG.Data;
using UnityEngine.UI;

namespace YARG.UI {
	public class SongView : MonoBehaviour {
		[SerializeField]
		private TextMeshProUGUI songName;
		[SerializeField]
		private TextMeshProUGUI artist;
		[SerializeField]
		private TextMeshProUGUI scoreText;
		[SerializeField]
		private GameObject songBackground;
		[SerializeField]
		private GameObject categoryBackground;
		[SerializeField]
		private Image sourceIcon;

		public void UpdateSongView(SongInfo songInfo) {
			songBackground.SetActive(true);
			categoryBackground.SetActive(false);
			sourceIcon.enabled = true;

			if (songInfo.source != null) {
				string folderPath = $"Sources/{songInfo.source}";
				Sprite loadedSprite = Resources.Load<Sprite>(folderPath);

				if (loadedSprite != null) {
					sourceIcon.sprite = loadedSprite;
					sourceIcon.enabled = true;
				} else {
					sourceIcon.sprite = Resources.Load<Sprite>("Sources/custom");
					sourceIcon.enabled = true;
				}
			} else {
				sourceIcon.enabled = true;
			}
			
			// 50% opacity
			sourceIcon.color = new Color(0.5f, 0.5f, 0.5f, 0.5f); // grey out icon


			songName.text = songInfo.SongName + $"     <i><alpha=#50>{songInfo.artistName}</i>";
			artist.text = $"<i>{songInfo.artistName}</i>";

			var score = ScoreManager.GetScore(songInfo);
			if (score == null || score.highestPercent.Count <= 0) {
				scoreText.text = "";
			} else {
				var (instrument, highest) = score.GetHighestPercent();
				scoreText.text = $"<sprite name=\"{instrument}\"> <b>{highest.difficulty.ToChar()}</b> {Mathf.Floor(highest.percent * 100f):N0}%";
			}
		}

		public void UpdateSongViewAsHeader(string header, string subheader) {
			songBackground.SetActive(false);
			categoryBackground.SetActive(true);
			sourceIcon.enabled = false;

			songName.text = $"<#FFFFFF><b>{header}</b>";
			artist.text = "";
			scoreText.text = subheader;
		}
	}
}