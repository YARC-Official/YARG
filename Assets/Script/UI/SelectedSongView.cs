using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using YARG.Data;
using YARG.Util;

namespace YARG.UI {
	public class SelectedSongView : MonoBehaviour {
		[SerializeField]
		private TextMeshProUGUI songName;
		[SerializeField]
		private TextMeshProUGUI artist;
		[SerializeField]
		private TextMeshProUGUI scoreText;
		[SerializeField]
		private TextMeshProUGUI lengthText;
		[SerializeField]
		private TextMeshProUGUI supportText;

		[Space]
		[SerializeField]
		private RawImage albumCover;
		[SerializeField]
		private GameObject albumCoverAlt;

		[Space]
		[SerializeField]
		private Transform difficultyContainer;
		[SerializeField]
		private GameObject difficultyView;

		private float timeSinceUpdate;
		private bool albumCoverLoaded;

		private SongInfo songInfo;

		public void UpdateSongView(SongInfo songInfo) {
			// Force stop album cover loading if new song
			StopAllCoroutines();

			timeSinceUpdate = 0f;
			albumCoverLoaded = false;
			this.songInfo = songInfo;

			// Basic info
			songName.text = songInfo.SongName;
			artist.text = $"<i>{songInfo.artistName}</i>";

			// Song score
			var score = ScoreManager.GetScore(songInfo);
			if (score == null || score.highestPercent.Count <= 0) {
				scoreText.text = "<alpha=#BB>No Score";
			} else {
				var (instrument, highest) = score.GetHighestPercent();
				scoreText.text = $"<sprite name=\"{instrument}\"> <b>{highest.difficulty.ToChar()}</b> {Mathf.Floor(highest.percent * 100f):N0}%";
			}

			// Song length
			int time = (int) songInfo.songLength;
			int minutes = time / 60;
			int seconds = time % 60;
			lengthText.text = $"{minutes}:{seconds:00}";

			// Source
			supportText.text = Utils.SourceToGameName(songInfo.source);

			// Album cover
			albumCover.texture = null;
			albumCover.color = new Color(0f, 0f, 0f, 0.4f);
			albumCoverAlt.SetActive(true);

			// Difficulties

			foreach (Transform t in difficultyContainer) {
				Destroy(t.gameObject);
			}

			foreach (var diff in songInfo.partDifficulties) {
				if (diff.Value == -1) {
					continue;
				}

				var diffView = Instantiate(difficultyView, difficultyContainer);

				// Get color
				string color = "white";
				if (diff.Value >= 6) {
					color = "#fc605d";
				}

				// Set text
				diffView.GetComponentInChildren<TextMeshProUGUI>().text =
					$"<sprite name=\"{diff.Key}\" color={color}> <color={color}>{(diff.Value == -2 ? "?" : diff.Value)}</color>";
			}
		}

		private void Update() {
			// Wait a little bit to load the album cover to prevent lag when scrolling through.
			if (songInfo != null && !albumCoverLoaded) {
				if (timeSinceUpdate >= 0.06f) {
					albumCoverLoaded = true;
					LoadAlbumCover();
				} else {
					timeSinceUpdate += Time.deltaTime;
				}
			}
		}

		private void LoadAlbumCover() {
			// If remote, request album cover
			string pngPath = Path.Combine(songInfo.folder.FullName, "album.png");
			string jpgPath = Path.Combine(songInfo.folder.FullName, "album.jpg");

			// Load PNG or JPG
			if (File.Exists(pngPath)) {
				StartCoroutine(LoadAlbumCoverCoroutine(pngPath));
			} else if (File.Exists(jpgPath)) {
				StartCoroutine(LoadAlbumCoverCoroutine(jpgPath));
			}
		}

		private IEnumerator LoadAlbumCoverCoroutine(string filePath) {
			if (!File.Exists(filePath)) {
				yield break;
			}

			// Load file
			using UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(filePath);
			yield return uwr.SendWebRequest();
			var texture = DownloadHandlerTexture.GetContent(uwr);

			// Set album cover
			albumCover.texture = texture;
			albumCover.color = Color.white;
			albumCoverAlt.SetActive(false);
		}

		public void PlaySong() {
			MainMenu.Instance.chosenSong = songInfo;
			MainMenu.Instance.ShowPreSong();
		}

		public void SearchArtist() {
			SongSelect.Instance.searchField.text = $"artist:{songInfo.artistName}";
		}

		public void SearchSource() {
			SongSelect.Instance.searchField.text = $"source:{songInfo.source}";
		}
	}
}