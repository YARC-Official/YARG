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
		[SerializeField]
		private GameObject difficultyDivider;

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
			supportText.text = songInfo.SourceFriendlyName;

			// Album cover
			albumCover.texture = null;
			albumCover.color = new Color(0f, 0f, 0f, 0.4f);
			albumCoverAlt.SetActive(true);

			// Difficulties

			foreach (Transform t in difficultyContainer) {
				Destroy(t.gameObject);
			}

			string[] difficultyOrder = {
				"guitar",
				"bass",
				"drums",
				"keys",
				"vocals",
				null,
				"realGuitar",
				"realBass",
				"realDrums",
				"realKeys",
				"harmVocals",
				null,
				"ghDrums"
			};

			foreach (var instrument in difficultyOrder) {
				if (instrument == null) {
					// Divider
					Instantiate(difficultyDivider, difficultyContainer);

					continue;
				}

				// GH Drums == Drums difficulty
				var searchInstrument = instrument;
				if (instrument == "ghDrums") {
					searchInstrument = "drums";
				}

				if (!songInfo.partDifficulties.ContainsKey(searchInstrument)) {
					continue;
				}

				int difficulty = songInfo.partDifficulties[searchInstrument];

				// If not five-lane mode, hide GH Drums difficulty 
				if (instrument == "ghDrums" && songInfo.drumType != SongInfo.DrumType.FIVE_LANE) {
					difficulty = -1;
				}

				// If not four-lane mode, hide drums difficulty
				if (instrument == "drums" && songInfo.drumType == SongInfo.DrumType.FIVE_LANE) {
					difficulty = -1;
				}

				// Difficulty
				var diffView = Instantiate(difficultyView, difficultyContainer);
				diffView.GetComponent<DifficultyView>().SetInfo(instrument, difficulty);
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
			string[] albumPaths = {
				"album.png",
				"album.jpg",
				"album.jpeg",
			};

			foreach (string path in albumPaths) {
				string fullPath = Path.Combine(songInfo.folder.FullName, path);
				if (File.Exists(fullPath)) {
					StartCoroutine(LoadAlbumCoverCoroutine(fullPath));
					break;
				}
			}
		}

		private IEnumerator LoadAlbumCoverCoroutine(string filePath) {
			if (!File.Exists(filePath)) {
				yield break;
			}

			// Load file
#if UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
			
			using UnityWebRequest uwr = UnityWebRequestTexture.GetTexture($"file://{filePath}");

#else

			using UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(filePath);

#endif
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