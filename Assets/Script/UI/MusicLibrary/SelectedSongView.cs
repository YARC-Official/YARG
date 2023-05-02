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

namespace YARG.UI.MusicLibrary {
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
					// icon name = "custom"
					sourceIcon.sprite = Resources.Load<Sprite>("Sources/custom");
					sourceIcon.enabled = true;
				}
			} else {
				sourceIcon.enabled = true;
			}

			// Basic info
			songName.text = songInfo.SongName;
			artist.text = songInfo.artistName;

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
		}

		public void PlaySong() {
			MainMenu.Instance.chosenSong = songInfo;
			MainMenu.Instance.ShowPreSong();
		}

		public void SearchFilter(string type) {
			string value = type switch {
				"artist" => songInfo.artistName,
				"source" => songInfo.source,
				"album" => songInfo.album,
				"year" => songInfo.year,
				"charter" => songInfo.charter,
				"genre" => songInfo.genre,
				_ => throw new System.Exception("Unreachable")
			};

			SongSelection.Instance.searchField.text = $"{type}:{value}";
		}
	}
}
