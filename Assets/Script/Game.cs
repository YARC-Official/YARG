using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using YARG.Serialization;

namespace YARG {
	public class Game : MonoBehaviour {
		public const float HIT_MARGIN = 0.075f;
		public const bool ANCHORING = true;

		public static readonly DirectoryInfo SONG_FOLDER = new(@"B:\YARG_Songs");
		public static readonly FileInfo CACHE_FILE = new(Path.Combine(SONG_FOLDER.ToString(), "yarg_cache.json"));

		public static DirectoryInfo song = new(@"B:\Clone Hero Alpha\Songs\Jane's Addiction - Been Caught Stealing");

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
		private float calibration = 0f;
		public float SongTime {
			get => realSongTime + calibration;
		}

		public float SongSpeed {
			get;
			private set;
		}
		public List<NoteInfo> chart;
		public List<EventInfo> chartEvents;

		private void Awake() {
			Instance = this;

			chart = null;
			chartEvents = null;
			SongSpeed = 7f;
			calibration = -0.23f;
			realSongTime = 0f;

			// Song

			StartCoroutine(StartSong(song));
		}

		private IEnumerator StartSong(DirectoryInfo songFolder) {
			// Load audio
			List<AudioSource> audioSources = new();
			foreach (var file in songFolder.GetFiles("*.ogg")) {
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
			var parser = new MidiParser(Path.Combine(songFolder.FullName, "notes.mid"));
			parser.Parse(out chart, out chartEvents);

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
					calibration += 0.01f;
					Debug.Log(calibration);
				}

				if (Keyboard.current.downArrowKey.wasPressedThisFrame) {
					calibration -= 0.01f;
					Debug.Log(calibration);
				}
			}
		}
	}
}