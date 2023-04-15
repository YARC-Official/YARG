using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using YARG.Data;
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
		
		public static SongInfo song = null;

		public delegate void BeatAction();
		public static event BeatAction BeatEvent;

		public delegate void SongStateChangeAction(SongInfo songInfo);
		public static event SongStateChangeAction OnSongStart;
		public static event SongStateChangeAction OnSongEnd;

		public delegate void PauseStateChangeAction(bool pause);
		public static event PauseStateChangeAction OnPauseToggle;

		[SerializeField]
		private GameObject soundAudioPrefab;

		public bool SongStarted {
			get;
			private set;
		} = false;

		private OccurrenceList<string> audioLowering = new();
		private OccurrenceList<string> audioReverb   = new();

		private float realSongTime => GameManager.AudioManager.CurrentPositionF;
		public float SongTime {
			get => realSongTime + PlayerManager.GlobalCalibration * speed;
		}

		public float SongLength {
			get;
			private set;
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

					GameManager.AudioManager.Pause();

				} else {
					Time.timeScale = 1f;

					GameManager.AudioManager.Play();
				}
				OnPauseToggle(_paused);
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
			var stems = AudioHelpers.GetSupportedStems(song.folder.FullName);
			
			GameManager.AudioManager.LoadSong(stems);
			SongLength = GameManager.AudioManager.AudioLengthF;

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

			GameManager.AudioManager.Play();
			
			SongStarted = true;

			// Hide loading screen
			GameUI.Instance.loadingContainer.SetActive(false);

			// End events override the audio length
			foreach (var chartEvent in chart.events) {
				// TODO: "chart.events" does not include the "end" event, as it is
				// intermdiate representation of the midi file. The "end" event must be parsed.
				if (chartEvent.name == "end") {
					SongLength = chartEvent.time;
					break;
				}
			}

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
			if (realSongTime > SongLength + 0.5f) {
				MainMenu.isPostSong = true;
				Exit();
			}
		}

		private void UpdateAudio(string[] trackNames, string[] stemNames) {
			if (SettingsManager.GetSettingValue<bool>("muteOnMiss")) {
				// Get total amount of players with the instrument (and the amount lowered)
				int amountWithInstrument = 0;
				int amountLowered = 0;
			
				for (int i = 0; i < trackNames.Length; i++) {
					amountWithInstrument += PlayerManager.PlayersWithInstrument(trackNames[i]);
					amountLowered += audioLowering.GetCount(trackNames[i]);
				}
			
				// Skip if no one is playing the instrument
				if (amountWithInstrument <= 0) {
					return;
				}
			
				// Lower all volumes to a minimum of 5%
				float percent = 1f - (float) amountLowered / amountWithInstrument;
				foreach (var name in stemNames) {
					var stem = AudioHelpers.GetStemFromName(name);

					GameManager.AudioManager.SetStemVolume(stem, percent * 0.95f + 0.05f);
				}
			}
			
			// Reverb audio with starpower
			
			foreach (var name in stemNames) {
				var stem = AudioHelpers.GetStemFromName(name);
				
				bool applyReverb = audioReverb.GetCount(name) > 0;
				
				GameManager.AudioManager.ApplyReverb(stem, applyReverb);
			}
		}

		public void Exit() {
			// Dispose of all audio
			GameManager.AudioManager.UnloadSong();

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
		
		public void ReverbAudio(string name, bool apply) {
			if (apply) {
				audioReverb.Add(name);
			} else {
				audioReverb.Remove(name);
			}
		}
	}
}
