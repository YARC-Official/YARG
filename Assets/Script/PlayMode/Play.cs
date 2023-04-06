using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using YARG.Data;
using YARG.Serialization.Audio;
using YARG.Serialization.Parser;
using YARG.Settings;
using YARG.UI;

namespace YARG.PlayMode {
	public class Play : MonoBehaviour {
		public static Play Instance {
			get;
			private set;
		}

		public static float speed = 1f;

		public const float SONG_START_OFFSET = 1f;
		public const float HIT_MARGIN = 0.095f;
		public const bool ANCHORING = true;

		public static SongInfo song = null;

		public delegate void BeatAction();
		public static event BeatAction BeatEvent;

		public delegate void SongStateChangeAction(SongInfo songInfo);
		public static event SongStateChangeAction OnSongStart;
		public static event SongStateChangeAction OnSongEnd;

		[SerializeField]
		private GameObject soundAudioPrefab;

		public bool SongStarted {
			get;
			private set;
		} = false;

		private Dictionary<string, AudioSource> audioSources = new();
		private OccurrenceList<string> audioLowering = new();

		private List<AudioHandler> audioHandlers = new();

		private float realSongTime = 0f;
		public float SongTime {
			get => realSongTime + PlayerManager.GlobalCalibration * speed;
		}

		public Chart chart;

		private int beatIndex = 0;
		private int lyricIndex = 0;
		private int lyricPhraseIndex = 0;

		private bool _paused = false;
		public bool Paused {
			get => _paused;
			set {
				_paused = value;

				GameUI.Instance.pauseMenu.SetActive(value);

				if (value) {
					Time.timeScale = 0f;

					// Pause audio
					foreach (var (_, source) in audioSources) {
						source.Pause();
					}
				} else {
					Time.timeScale = 1f;

					// Unpause audio
					foreach (var (_, source) in audioSources) {
						source.UnPause();
					}
				}
			}
		}

		private void Awake() {
			Instance = this;

			// Song

			StartCoroutine(StartSong());
		}

		private IEnumerator StartSong() {
			GameUI.Instance.SetLoadingText("Loading audio...");
			// Load audio
			foreach (var file in AudioHandler.GetAllSupportedAudioFiles(song.folder.FullName)) {
				var name = Path.GetFileNameWithoutExtension(file);
				if (name == "preview" || name == "crowd") {
					continue;
				}

				// Load file
				var audioHandler = AudioHandler.CreateAudioHandler(file);
				yield return audioHandler.LoadAudioClip();
				var clip = audioHandler.GetAudioClipResult();
				audioHandlers.Add(audioHandler);

				// Create audio source
				var songAudio = Instantiate(soundAudioPrefab, transform);
				var audioSource = songAudio.GetComponent<AudioSource>();
				audioSource.clip = clip;
				audioSources.Add(name, audioSource);

				// Set audio source mixer
				string mixerName = name;
				if (mixerName == "drums_1" || mixerName == "drums_2" || mixerName == "drums_3" || mixerName == "drums_4") {
					mixerName = "drums";
				} else if (mixerName == "vocals_1" || mixerName == "vocals_2") {
					mixerName = "vocals";
				} else if (mixerName == "rhythm") {
					// For now
					mixerName = "bass";
				}
				audioSource.outputAudioMixerGroup = AudioManager.Instance.GetAudioMixerGroup(mixerName);
			}

			// Check for single guitar audio
			if (audioSources.Count == 1 && audioSources.ContainsKey("guitar")) {
				// If so, replace it as the song audio
				// Standardized here: https://github.com/TheNathannator/GuitarGame_ChartFormats/blob/main/doc/FileFormats/Audio%20Files.md#file-names
				audioSources.Add("song", audioSources["guitar"]);

				// Remove old audio
				audioSources.Remove("guitar");
			}

			GameUI.Instance.SetLoadingText("Loading chart...");

			// Load chart (from midi, upgrades, etc.)
			LoadChart();

			GameUI.Instance.SetLoadingText("Spawning tracks...");

			// Spawn tracks
			int i = 0;
			foreach (var player in PlayerManager.players) {
				if (player.chosenInstrument == null) {
					// Skip players that are sitting out
					continue;
				}

				string trackPath = player.inputStrategy.GetTrackPath();

				if (trackPath == null) {
					continue;
				}

				var prefab = Addressables.LoadAssetAsync<GameObject>(trackPath).WaitForCompletion();
				var track = Instantiate(prefab, new Vector3(i * 25f, 100f, 0f), prefab.transform.rotation);
				track.GetComponent<AbstractTrack>().player = player;

				i++;
			}

			yield return new WaitForSeconds(SONG_START_OFFSET);

			// Start all audio at the same time
			foreach (var (_, audioSource) in audioSources) {
				audioSource.pitch = speed;
				audioSource.Play();
			}
			realSongTime = audioSources.First().Value.time;
			SongStarted = true;

			// Hide loading screen
			GameUI.Instance.loadingContainer.SetActive(false);

			// Call events
			OnSongStart?.Invoke(song);
		}

		private void LoadChart() {
			// Add main file
			var files = new List<string> {
				Path.Combine(song.folder.FullName, "notes.mid")
			};

			// Look for upgrades and add
			var upgradeFolder = new DirectoryInfo(Path.Combine(song.folder.FullName, "yarg_upgrade"));
			if (upgradeFolder.Exists) {
				foreach (var midi in upgradeFolder.GetFiles("*.mid")) {
					files.Add(midi.FullName);
				}
			}

			// Parse
			var parser = new MidiParser(song, files.ToArray());
			chart = new Chart();
			parser.Parse(chart);
		}

		private void Update() {
			if (!SongStarted) {
				return;
			}

			// Pausing
			if (Keyboard.current.escapeKey.wasPressedThisFrame) {
				Paused = !Paused;
			}

			if (Paused) {
				return;
			}

			var leaderAudioSource = audioSources.First().Value;
			if (!SettingsManager.GetSettingValue<bool>("useAudioTime") || !leaderAudioSource.isPlaying) {
				realSongTime += Time.deltaTime * speed;
			} else {
				// TODO: Use "timeSamples" for better accuracy
				realSongTime = leaderAudioSource.time;
			}

			// Audio raising and lowering based on player preformance
			if (SettingsManager.GetSettingValue<bool>("muteOnMiss")) {
				// Mute guitars
				UpdateAudio(new string[] {
					"guitar",
					"realGuitar"
				}, new string[] {
					"guitar"
				});

				// Mute bass
				UpdateAudio(new string[] {
					"bass",
					"realBass"
				}, new string[] {
					"bass",
					"rhythm"
				});

				// Mute keys
				UpdateAudio(new string[] {
					"keys",
					"realKeys"
				}, new string[] {
					"keys"
				});

				// Mute drums
				UpdateAudio(new string[] {
					"drums",
					"realDrums"
				}, new string[] {
					"drums",
					"drums_1",
					"drums_2",
					"drums_3",
					"drums_4"
				});
			}

			// Update beats
			while (chart.beats.Count > beatIndex && chart.beats[beatIndex] <= SongTime) {
				BeatEvent?.Invoke();
				beatIndex++;
			}

			// Update lyrics
			if (lyricIndex < chart.genericLyrics.Count) {
				var lyric = chart.genericLyrics[lyricIndex];
				if (lyricPhraseIndex >= lyric.lyric.Count) {
					lyricPhraseIndex = 0;
					lyricIndex++;
				} else if (lyric.lyric[lyricPhraseIndex].time < SongTime) {
					// Consolidate lyrics
					string o = "<color=#ffb700>";
					for (int i = 0; i < lyric.lyric.Count; i++) {
						var (_, str) = lyric.lyric[i];

						if (str.EndsWith("-")) {
							o += str[0..^1].Replace("=", "-");
						} else {
							o += str.Replace("=", "-") + " ";
						}

						if (i + 1 > lyricPhraseIndex) {
							o += "</color>";
						}
					}

					GameUI.Instance.SetGenericLyric(o);
					lyricPhraseIndex++;
				}
			}

			// End song
			if (realSongTime > song.songLength + 0.5f) {
				MainMenu.isPostSong = true;
				Exit();
			}
		}

		private void UpdateAudio(string[] names, string[] audio) {
			// Get total amount of players with the instrument (and the amount lowered)
			int amountWithInstrument = 0;
			int amountLowered = 0;
			for (int i = 0; i < names.Length; i++) {
				amountWithInstrument += PlayerManager.PlayersWithInstrument(names[i]);
				amountLowered += audioLowering.GetCount(names[i]);
			}

			// Skip if no one is playing the instrument
			if (amountWithInstrument <= 0) {
				return;
			}

			// Lower all volumes to a minimum of 5%
			float percent = 1f - (float) amountLowered / amountWithInstrument;
			foreach (var name in audio) {
				if (!audioSources.TryGetValue(name, out var audioSource)) {
					continue;
				}

				audioSource.volume = percent * 0.95f + 0.05f;
			}
		}

		public void Exit() {
			// Dispose of all audio
			foreach (var audioHandler in audioHandlers) {
				try {
					audioHandler.Finish();
				} catch (Exception e) {
					Debug.LogError(e);
				}
			}

			// Call events
			OnSongEnd?.Invoke(song);

			// Unpause just in case
			Time.timeScale = 1f;

			GameManager.Instance.LoadScene(SceneIndex.MENU);
		}

		public void LowerAudio(string name) {
			audioLowering.Add(name);
		}

		public void RaiseAudio(string name) {
			audioLowering.Remove(name);
		}
	}
}
