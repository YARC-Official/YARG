using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using YARG.Data;

#if UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX

using System;

#endif

namespace YARG.UI {
	public class SelectedSongView : MonoBehaviour {
		[SerializeField]
		private TextMeshProUGUI songName;
		[SerializeField]
		private TextMeshProUGUI artist;
		[SerializeField]
		private TextMeshProUGUI scoreText;
		[SerializeField]
		private TextMeshProUGUI albumText;
		[SerializeField]
		private TextMeshProUGUI lengthText;
		[SerializeField]
		private TextMeshProUGUI supportText;
		[SerializeField]
		private TextMeshProUGUI genreText;
		[SerializeField]
		private TextMeshProUGUI yearText;
		[SerializeField]
		private TextMeshProUGUI charterText;
		[SerializeField]
		private Image sourceIcon;

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

			// Source Icon
			if (songInfo.source != null) {
				string folderPath = $"Sources/{songInfo.source}";
				Sprite loadedSprite = Resources.Load<Sprite>(folderPath);

				if (loadedSprite != null) {
					sourceIcon.sprite = loadedSprite;
					sourceIcon.enabled = true;
				} else {
					Debug.LogError($"Failed to load source icon at path: Resources/{folderPath}");
					sourceIcon.enabled = false;
				}
			} else {
				sourceIcon.enabled = false;
			}

			// Basic info
			songName.text = $"<b>{songInfo.SongName}</b>" + $"<space=10px><#00DBFD><i>{songInfo.artistName}</i>";
			artist.text = $"<i>{songInfo.artistName}</i>";

			// Song score
			var score = ScoreManager.GetScore(songInfo);
			if (score == null || score.highestPercent.Count <= 0) {
				scoreText.text = "";
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

			// Album
			albumText.text = songInfo.album;

			// Year
			yearText.text = songInfo.year;

			// Genre
			genreText.text = songInfo.genre;

			// Charter
			charterText.text = songInfo.charter;

			// Album cover
			albumCover.texture = null;
			albumCover.color = new Color(0f, 0f, 0f, 0.4f);
			albumCoverAlt.SetActive(true);

			// Difficulties

			foreach (Transform t in difficultyContainer) {
				Destroy(t.gameObject);
			}

			Instrument?[] difficultyOrder = {
				Instrument.GUITAR,
				Instrument.BASS,
				Instrument.DRUMS,
				Instrument.KEYS,
				Instrument.VOCALS,
				null,
				Instrument.REAL_GUITAR,
				Instrument.REAL_BASS,
				Instrument.REAL_DRUMS,
				Instrument.REAL_KEYS,
				Instrument.HARMONY,
				null,
				Instrument.GUITAR_COOP,
				Instrument.RHYTHM,
				Instrument.GH_DRUMS
			};

			foreach (var inst in difficultyOrder) {
				if (inst == null) {
					// Divider
					Instantiate(difficultyDivider, difficultyContainer);

					continue;
				}

				var instrument = inst.Value;

				// GH Drums == Drums difficulty
				var searchInstrument = instrument;
				if (instrument == Instrument.GH_DRUMS) {
					searchInstrument = Instrument.DRUMS;
				}

				if (!songInfo.partDifficulties.ContainsKey(searchInstrument)) {
					continue;
				}

				int difficulty = songInfo.partDifficulties[searchInstrument];

				// If not five-lane mode, hide GH Drums difficulty 
				if (instrument == Instrument.GH_DRUMS && songInfo.drumType != SongInfo.DrumType.FIVE_LANE) {
					difficulty = -1;
				}

				// If not four-lane mode, hide drums difficulty
				if (instrument == Instrument.DRUMS && songInfo.drumType == SongInfo.DrumType.FIVE_LANE) {
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
			if (songInfo.songType == SongInfo.SongType.SONG_INI) {
				string[] albumPaths = {
					"album.png",
					"album.jpg",
					"album.jpeg",
				};

				foreach (string path in albumPaths) {
					string fullPath = Path.Combine(songInfo.RootFolder, path);
					if (File.Exists(fullPath)) {
						StartCoroutine(LoadAlbumCoverCoroutine(fullPath));
						break;
					}
				}
			} else {
				if (songInfo.imageInfo == null) {
					return;
				}

				// Set album cover
				albumCover.texture = songInfo.imageInfo.GetAsTexture();
				albumCover.color = Color.white;
				albumCover.uvRect = new Rect(0f, 0f, 1f, -1f);
				albumCoverAlt.SetActive(false);
			}
		}

		private IEnumerator LoadAlbumCoverCoroutine(string filePath) {
			if (!File.Exists(filePath)) {
				yield break;
			}

			// Load file
#if UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
			
			Uri pathUri = new Uri(filePath);
			using UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(pathUri);

#else

			using UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(filePath);

#endif
			yield return uwr.SendWebRequest();
			var texture = DownloadHandlerTexture.GetContent(uwr);

			// Set album cover
			albumCover.texture = texture;
			albumCover.color = Color.white;
			albumCover.uvRect = new Rect(0f, 0f, 1f, 1f);
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
