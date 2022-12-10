using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using YARG.Serialization;

namespace YARG {
	public class Game : MonoBehaviour {
		public const float HIT_MARGIN = 0.1f;
		public const bool ANCHORING = true;

		public static readonly DirectoryInfo SONG_FOLDER = new(@"B:\YARG_Songs");
		public static readonly FileInfo CACHE_FILE = new(Path.Combine(SONG_FOLDER.ToString(), "yarg_cache.json"));

		public static DirectoryInfo song = new(@"B:\Clone Hero Alpha\Songs\Jane's Addiction - Been Caught Stealing");
		public static bool botMode = false;

		[SerializeField]
		private GameObject soundAudioPrefab;
		[SerializeField]
		private GameObject trackPrefab;

		public delegate void FretPressAction(bool on, int fret);
		public event FretPressAction FretPressEvent;

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

		public bool StrumThisFrame {
			get;
			private set;
		} = false;

		private int botChartIndex = 0;

		private void Awake() {
			Instance = this;

			chart = null;
			chartEvents = null;
			SongSpeed = 7f;
			calibration = -0.23f;
			realSongTime = 0f;

			// Input

			// input = new YargInput();
			// input.Enable();

			// input._5Fret.Green.started += _ => FretPress(0);
			// input._5Fret.Red.started += _ => FretPress(1);
			// input._5Fret.Yellow.started += _ => FretPress(2);
			// input._5Fret.Blue.started += _ => FretPress(3);
			// input._5Fret.Orange.started += _ => FretPress(4);
			// input._5Fret.Strum.started += _ => Strum(true);

			// input._5Fret.Green.canceled += _ => FretRelease(0);
			// input._5Fret.Red.canceled += _ => FretRelease(1);
			// input._5Fret.Yellow.canceled += _ => FretRelease(2);
			// input._5Fret.Blue.canceled += _ => FretRelease(3);
			// input._5Fret.Orange.canceled += _ => FretRelease(4);
			// input._5Fret.Strum.canceled += _ => Strum(false);

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

			// Spawn track
			Instantiate(trackPrefab);

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

				// Update bot mode
				if (botMode) {
					bool resetForChord = false;
					while (chart.Count > botChartIndex && chart[botChartIndex].time <= Instance.SongTime) {
						// Release old frets
						if (!resetForChord) {
							for (int i = 0; i < 5; i++) {
								FretRelease(i);
							}
							resetForChord = true;
						}

						var noteInfo = chart[botChartIndex];

						// Press new fret
						FretPress(noteInfo.fret);
						StrumThisFrame = true;
						botChartIndex++;
					}
				}
			}
		}

		private void LateUpdate() {
			StrumThisFrame = false;
		}

		private void FretPress(int fret) {
			FretPressEvent?.Invoke(true, fret);
		}

		private void FretRelease(int fret) {
			FretPressEvent?.Invoke(false, fret);
		}

		private void Strum(bool on) {
			StrumThisFrame = on;
		}
	}
}