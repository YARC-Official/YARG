using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using YARG.Data;
using YARG.Serialization;
using YARG.Song;
using YARG.UI.MusicLibrary.ViewTypes;

namespace YARG.UI.MusicLibrary {
	public class Sidebar : MonoBehaviour {
		[SerializeField]
		private Transform _difficultyRingsContainer;
		[SerializeField]
		private TextMeshProUGUI _album;
		[SerializeField]
		private TextMeshProUGUI _source;
		[SerializeField]
		private TextMeshProUGUI _charter;
		[SerializeField]
		private TextMeshProUGUI _genre;
		[SerializeField]
		private TextMeshProUGUI _year;
		[SerializeField]
		private TextMeshProUGUI _length;
		[SerializeField]
		private RawImage _albumCover;
		[SerializeField]
		private TextMeshProUGUI _infoText;

		[Space]
		[SerializeField]
		private GameObject difficultyRingPrefab;

		private List<DifficultyRing> difficultyRings = new();

		private CancellationTokenSource _cancellationToken;

		public void Init() {
			// Spawn 10 difficulty rings
			for (int i = 0; i < 10; i++) {
				var go = Instantiate(difficultyRingPrefab, _difficultyRingsContainer);
				difficultyRings.Add(go.GetComponent<DifficultyRing>());
			}
		}

		public async UniTask UpdateSidebar() {
			// Cancel album art
			if (_cancellationToken != null) {
				_cancellationToken.Cancel();
				_cancellationToken.Dispose();
				_cancellationToken = null;
			}

			if (SongSelection.Instance.Songs.Count <= 0) {
				return;
			}

			var viewType = SongSelection.Instance.Songs[SongSelection.Instance.SelectedIndex];
			if (viewType is not SongViewType songViewType) {
				// setting the sidebar info when we are on a header
				// It would be nice to have this display more data but I didn't want to monkey around with song scanning to get get number of charters, genres, etc
				_albumCover.texture = null;
				_albumCover.color = Color.clear;
				_album.text = "";
				_source.text = SongSources.GetSourceCount().ToString()+ " sources";
				_charter.text = "";
				_genre.text = 	"";
				_year.text = "";
				_length.text = "";
				_infoText.text = "";
				//We might want to add a dummy song so we can call UpdateDifficulties() and set all the rings to -1
				difficultyRings[0].SetInfo(true, Instrument.GUITAR, -1);
				difficultyRings[1].SetInfo(true, Instrument.BASS, -1);
				difficultyRings[2].SetInfo(true, Instrument.DRUMS, -1);
				difficultyRings[3].SetInfo(true, Instrument.KEYS, -1);
				difficultyRings[4].SetInfo(true, Instrument.VOCALS, -1);
				difficultyRings[5].SetInfo(true, Instrument.REAL_GUITAR, -1);
				difficultyRings[6].SetInfo(true, Instrument.REAL_BASS, -1);
				difficultyRings[7].SetInfo(false, "trueDrums", -1);
				difficultyRings[8].SetInfo(true, Instrument.REAL_KEYS, -1);
				difficultyRings[9].SetInfo(false, "band", -1);
				return;
			}

			var songEntry = songViewType.SongEntry;

			_album.text = songEntry.Album;
			_source.text = SongSources.SourceToGameName(songEntry.Source);
			_charter.text = songEntry.Charter;
			_genre.text = songEntry.Genre;
			_year.text = songEntry.Year;
			_infoText.text = songEntry.LoadingPhrase;
			

			// Format and show length
			if (songEntry.SongLengthTimeSpan.Hours > 0) {
				_length.text = songEntry.SongLengthTimeSpan.ToString(@"h\:mm\:ss");
			} else {
				_length.text = songEntry.SongLengthTimeSpan.ToString(@"m\:ss");
			}

			UpdateDifficulties(songEntry);

			// Finally, update album cover
			await LoadAlbumCover();
		}

		private void UpdateDifficulties(SongEntry songEntry) {
			/*
			
				Guitar               ; Bass               ; 4 or 5 lane ; Keys     ; Mic (dependent on mic count) 
				Pro Guitar or Co-op  ; Pro Bass or Rhythm ; True Drums  ; Pro Keys ; Band
			
			*/

			difficultyRings[0].SetInfo(songEntry, Instrument.GUITAR);
			difficultyRings[1].SetInfo(songEntry, Instrument.BASS);

			// 5-lane or 4-lane
			if (songEntry.DrumType == DrumType.FiveLane) {
				difficultyRings[2].SetInfo(songEntry, Instrument.GH_DRUMS);
			} else {
				difficultyRings[2].SetInfo(songEntry, Instrument.DRUMS);
			}

			difficultyRings[3].SetInfo(songEntry, Instrument.KEYS);

			// Mic (with mic count)
			if (songEntry.PartDifficulties.GetValueOrDefault(Instrument.HARMONY, -1) == -1) {
				difficultyRings[4].SetInfo(songEntry, Instrument.VOCALS);
			} else {
				difficultyRings[4].SetInfo(songEntry, Instrument.HARMONY);
			}

			// Protar or Co-op
			int realGuitarDiff = songEntry.PartDifficulties.GetValueOrDefault(Instrument.REAL_GUITAR, -1);
			if (songEntry.DrumType == DrumType.FourLane && realGuitarDiff == -1) {
				difficultyRings[5].SetInfo(songEntry, Instrument.GUITAR_COOP);
			} else {
				difficultyRings[5].SetInfo(songEntry, Instrument.REAL_GUITAR);
			}

			// Pro bass or Rhythm
			int realBassDiff = songEntry.PartDifficulties.GetValueOrDefault(Instrument.REAL_BASS, -1);
			if (songEntry.DrumType == DrumType.FiveLane && realBassDiff == -1) {
				difficultyRings[6].SetInfo(songEntry, Instrument.RHYTHM);
			} else {
				difficultyRings[6].SetInfo(songEntry, Instrument.REAL_BASS);
			}

			difficultyRings[7].SetInfo(false, "trueDrums", -1);
			difficultyRings[8].SetInfo(songEntry, Instrument.REAL_KEYS);
			difficultyRings[9].SetInfo(false, "band", -1);
		}

		public async UniTask LoadAlbumCover() {
			// Dispose of the old texture (prevent memory leaks)
			if (_albumCover.texture != null) {
				// This might seem weird, but we are destroying the *texture*, not the UI image.
				Destroy(_albumCover.texture);
			}

			// Hide album art until loaded
			_albumCover.texture = null;
			_albumCover.color = Color.clear;

			_cancellationToken = new();

			var viewType = SongSelection.Instance.Songs[SongSelection.Instance.SelectedIndex];
			if (viewType is not SongViewType songViewType) {
				return;
			}

			var songEntry = songViewType.SongEntry;

			if (songEntry.SongType == SongType.SongIni) {
				string[] possiblePaths = {
					"album.png",
					"album.jpg",
					"album.jpeg",
				};

				// Load album art from one of the paths
				foreach (string path in possiblePaths) {
					string fullPath = Path.Combine(songEntry.Location, path);
					if (File.Exists(fullPath)) {
						await LoadSongIniCover(fullPath);
						break;
					}
				}
			} else if (songEntry.SongType == SongType.RbCon) {
				await LoadRbConCover((ConSongEntry) songEntry);
			} else if (songEntry.SongType == SongType.ExtractedRbCon) {
				await LoadExtractedRbConCover((ExtractedConSongEntry) songEntry);
			}
		}

		private async UniTask LoadSongIniCover(string filePath) {
			// Load file
#if UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
			using UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(new System.Uri(filePath));
#else
			using UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(filePath);
#endif

			try {
				await uwr.SendWebRequest().WithCancellation(_cancellationToken.Token);
				var texture = DownloadHandlerTexture.GetContent(uwr);

				// Set album cover
				_albumCover.texture = texture;
				_albumCover.color = Color.white;
				_albumCover.uvRect = new Rect(0f, 0f, 1f, 1f);
			} catch (OperationCanceledException) { }
		}

		private async UniTask LoadRbConCover(ConSongEntry conSongEntry) {
			if (string.IsNullOrEmpty(conSongEntry.ImagePath)) {
				return;
			}

			Texture2D texture = null;

			try {
				byte[] bytes;
				if(conSongEntry.AlternatePath)
					bytes = File.ReadAllBytes(conSongEntry.ImagePath);
				else bytes = await XboxCONInnerFileRetriever.RetrieveFile(conSongEntry.Location,
					conSongEntry.ImageFileSize, conSongEntry.ImageFileMemBlockOffsets, _cancellationToken.Token);
				
				texture = await XboxImageTextureGenerator.GetTexture(bytes, _cancellationToken.Token);

				_albumCover.texture = texture;
				_albumCover.color = Color.white;
				_albumCover.uvRect = new Rect(0f, 0f, 1f, -1f);
			} catch (OperationCanceledException) {
				// Dispose of the texture (prevent memory leaks)
				if (texture != null) {
					// This might seem weird, but we are destroying the *texture*, not the UI image.
					Destroy(texture);
				}
			}
		}

		private async UniTask LoadExtractedRbConCover(ExtractedConSongEntry conSongEntry) {
			if (string.IsNullOrEmpty(conSongEntry.ImagePath)) {
				return;
			}

			Texture2D texture = null;

			try {
				var bytes = await File.ReadAllBytesAsync(conSongEntry.ImagePath, _cancellationToken.Token);
				texture = await XboxImageTextureGenerator.GetTexture(bytes, _cancellationToken.Token);

				_albumCover.texture = texture;
				_albumCover.color = Color.white;
				_albumCover.uvRect = new Rect(0f, 0f, 1f, -1f);
			} catch (OperationCanceledException) {
				// Dispose of the texture (prevent memory leaks)
				if (texture != null) {
					// This might seem weird, but we are destroying the *texture*, not the UI image.
					Destroy(texture);
				}
			}
		}

		public void PrimaryButtonClick() {
			var viewType = SongSelection.Instance.Songs[SongSelection.Instance.SelectedIndex];
			viewType.PrimaryButtonClick();
		}

		public void SearchFilter(string type) {
			var viewType = SongSelection.Instance.Songs[SongSelection.Instance.SelectedIndex];
			if (viewType is not SongViewType songViewType) {
				return;
			}

			var songEntry = songViewType.SongEntry;

			string value = type switch {
				"source" => songEntry.Source,
				"album" => songEntry.Album,
				"year" => songEntry.Year,
				"charter" => songEntry.Charter,
				"genre" => songEntry.Genre,
				_ => throw new Exception("Unreachable")
			};
			SongSelection.Instance.searchField.text = $"{type}:{value}";
		}
	}
}