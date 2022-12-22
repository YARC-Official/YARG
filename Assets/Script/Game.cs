using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using YARG.Serialization;
using YARG.UI;
using YARG.Utils;

namespace YARG {
	public class Game : MonoBehaviour {
		public const float HIT_MARGIN = 0.075f;
		public const bool ANCHORING = true;

		public static readonly DirectoryInfo SONG_FOLDER = new(@"B:\Clone Hero Alpha\Songs");
		public static readonly FileInfo CACHE_FILE = new(Path.Combine(SONG_FOLDER.ToString(), "yarg_cache.json"));

		public static SongInfo song = null;

		[SerializeField]
		private GameObject soundAudioPrefab;
		[SerializeField]
		private GameObject trackPrefab;

		public static Game Instance {
			get;
			private set;
		} = null;

		private bool songStarted = false;

		private float realSongTime = 0f;
		public float SongTime {
			get => realSongTime + PlayerManager.globalCalibration;
		}

		public Chart chart;

		private void Awake() {
			Instance = this;

			chart = null;
			realSongTime = 0f;

			// Song

			StartCoroutine(StartSong());
		}

		private IEnumerator StartSong() {
			// Load audio
			List<AudioSource> audioSources = new();
			foreach (var file in song.folder.GetFiles("*.ogg")) {
				if (file.Name == "preview.ogg") {
					continue;
				}

				// Load file
				using UnityWebRequest uwr = UnityWebRequestMultimedia.GetAudioClip(file.FullName, AudioType.OGGVORBIS);
				((DownloadHandlerAudioClip) uwr.downloadHandler).streamAudio = true;
				yield return uwr.SendWebRequest();
				var clip = DownloadHandlerAudioClip.GetContent(uwr);

				// Create audio source
				var songAudio = Instantiate(soundAudioPrefab, transform);
				var audioSource = songAudio.GetComponent<AudioSource>();
				audioSource.clip = clip;
				audioSources.Add(audioSource);
			}

			// Load midi
			var parser = new MidiParser(Path.Combine(song.folder.FullName, "notes.mid"),
				song.delay + SourceDelays.GetSourceDelay(song.source, song.delay));
			chart = new Chart();
			parser.Parse(chart);

			// Spawn tracks
			for (int i = 0; i < PlayerManager.players.Count; i++) {
				var track = Instantiate(trackPrefab,
					new Vector3(i * 25f, 0f, 0f), trackPrefab.transform.rotation);
				track.GetComponent<Track>().player = PlayerManager.players[i];
			}

			songStarted = true;

			// Start all audio at the same time
			foreach (var audioSource in audioSources) {
				audioSource.Play();
			}
		}

		private void Update() {
			if (songStarted) {
				realSongTime += Time.deltaTime;

				if (Keyboard.current.upArrowKey.wasPressedThisFrame) {
					PlayerManager.globalCalibration += 0.01f;
				}

				if (Keyboard.current.downArrowKey.wasPressedThisFrame) {
					PlayerManager.globalCalibration -= 0.01f;
				}
			}
		}

		public void Exit() {
			SceneManager.LoadScene(0);
		}
	}
}