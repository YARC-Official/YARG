using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using YARG.Data;
using YARG.Song;

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

		private SongEntry _songEntry;

		public void UpdateSongView(SongEntry songEntry) {
			// Force stop album cover loading if new song
			StopAllCoroutines();

			timeSinceUpdate = 0f;
			albumCoverLoaded = false;
			_songEntry = songEntry;

			// Source Icon
			if (songEntry.Source != null) {
				string folderPath = $"Sources/{songEntry.Source}";
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
			songName.text = songEntry.Name;
			artist.text = songEntry.Artist;

			// Song score
			var score = ScoreManager.GetScore(songEntry);
			if (score == null || score.highestPercent.Count <= 0) {
				scoreText.text = "";
			} else {
				var (instrument, highest) = score.GetHighestPercent();
				scoreText.text = $"<sprite name=\"{instrument}\"> <b>{highest.difficulty.ToChar()}</b> {Mathf.Floor(highest.percent * 100f):N0}%";
			}

			// Song length
			lengthText.text = songEntry.SongLengthTimeSpan.ToString(@"hh\:mm\:ss");

			// Source
			supportText.text = SongSources.SourceToGameName(songEntry.Source);

			// Album
			albumText.text = songEntry.Album;

			// Year
			yearText.text = songEntry.Year;

			// Genre
			genreText.text = songEntry.Genre;

			// Charter
			charterText.text = songEntry.Charter;

			// Album cover
			albumCover.texture = null;
			albumCover.color = new Color(0f, 0f, 0f, 0.4f);
		}

		private void Update() {
			// Wait a little bit to load the album cover to prevent lag when scrolling through.
			if (_songEntry != null && !albumCoverLoaded) {
				if (timeSinceUpdate >= 0.06f) {
					albumCoverLoaded = true;
					LoadAlbumCover();
				} else {
					timeSinceUpdate += Time.deltaTime;
				}
			}
		}

		private void LoadAlbumCover() {
			if (_songEntry.SongType == SongType.SongIni) {
				string[] albumPaths = {
					"album.png",
					"album.jpg",
					"album.jpeg",
				};

				foreach (string path in albumPaths) {
					string fullPath = Path.Combine(_songEntry.Location, path);
					if (File.Exists(fullPath)) {
						StartCoroutine(LoadAlbumCoverCoroutine(fullPath));
						break;
					}
				}
			} else {
				// Check if an EXCon or if there is no album image for this song
				if (_songEntry is not ExtractedConSongEntry conEntry || conEntry.ImageInfo is null) {
					return;
				}

				// Set album cover
				albumCover.texture = conEntry.ImageInfo.GetAsTexture();
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
			MainMenu.Instance.chosenSong = _songEntry;
			MainMenu.Instance.ShowPreSong();
		}

		public void SearchFilter(string type) {
			string value = type switch {
				"artist" => _songEntry.Artist,
				"source" => _songEntry.Source,
				"album" => _songEntry.Album,
				"year" => _songEntry.Year,
				"charter" => _songEntry.Charter,
				"genre" => _songEntry.Genre,
				_ => throw new System.Exception("Unreachable")
			};

			SongSelection.Instance.searchField.text = $"{type}:{value}";
		}
	}
}
