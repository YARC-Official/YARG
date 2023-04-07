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

		private void OnEnable() {
			// Bind events
			if (GameManager.client != null) {
				GameManager.client.SignalEvent += SignalRecieved;
			}
		}

		private void OnDisable() {
			// Unbind events
			if (GameManager.client != null) {
				GameManager.client.SignalEvent -= SignalRecieved;
			}
		}

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
				var diffView = Instantiate(difficultyView, difficultyContainer);
				diffView.GetComponent<DifficultyView>().SetInfo(diff.Key, diff.Value);
			}
		}

		private void Update() {
			// Wait a little bit to load the album cover 
			// to prevent lag when scrolling through.
			if (songInfo != null && !albumCoverLoaded) {
				float waitTime = GameManager.client != null ? 0.5f : 0.06f;
				if (timeSinceUpdate >= waitTime) {
					albumCoverLoaded = true;
					LoadAlbumCover();
				} else {
					timeSinceUpdate += Time.deltaTime;
				}
			}
		}

		private void LoadAlbumCover() {
			// If remote, request album cover
			if (GameManager.client != null) {
				GameManager.client.RequestAlbumCover(songInfo.folder.FullName);
			} else {
				string pngPath = Path.Combine(songInfo.folder.FullName, "album.png");
				string jpgPath = Path.Combine(songInfo.folder.FullName, "album.jpg");

				// Load PNG or JPG
				if (File.Exists(pngPath)) {
					StartCoroutine(LoadAlbumCoverCoroutine(pngPath));
				} else if (File.Exists(jpgPath)) {
					StartCoroutine(LoadAlbumCoverCoroutine(jpgPath));
				}
			}
		}

		private void SignalRecieved(string signal) {
			if (signal.StartsWith("AlbumCoverDone,")) {
				string hash = signal[15..];

				// Skip if the hashes are not equal.
				// That means that this request was for a different song.
				if (hash != Utils.Hash(songInfo.folder.FullName)) {
					return;
				}

				string path = Path.Combine(GameManager.client.AlbumCoversPath, hash);
				StartCoroutine(LoadAlbumCoverCoroutine(path));
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