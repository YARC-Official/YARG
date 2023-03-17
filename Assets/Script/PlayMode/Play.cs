using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using YARG.Data;
using YARG.Serialization.Parser;
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
		public event BeatAction BeatEvent;

		[SerializeField]
		private GameObject soundAudioPrefab;

		public bool SongStarted {
			get;
			private set;
		} = false;

		private Dictionary<string, AudioSource> audioSources = new();
		private OccurrenceList<string> audioLowering = new();

		private float realSongTime = 0f;
		public float SongTime {
			get => realSongTime + PlayerManager.globalCalibration * speed;
		}

		public Chart chart;

		private int beatIndex = 0;
		private int lyricIndex = 0;
		private int lyricPhraseIndex = 0;

		private bool paused = false;

		private void Awake() {
			Instance = this;

			// Song

			StartCoroutine(StartSong());
		}

		private IEnumerator StartSong() {
			// Load audio
			foreach (var file in song.folder.GetFiles("*.ogg")) {
				if (file.Name == "preview.ogg") {
					continue;
				}

				if (GameManager.Instance.KaraokeMode && file.Name == "vocals.ogg") {
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
				audioSources.Add(Path.GetFileNameWithoutExtension(file.Name), audioSource);
			}

			// Load chart (from midi, upgrades, etc.)
			LoadChart();

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
			var parser = new MidiParser(files.ToArray(), song.delay);
			chart = new Chart();
			parser.Parse(chart);
		}

		private void Update() {
			if (!SongStarted) {
				return;
			}

			// Pausing
			if (Keyboard.current.spaceKey.wasPressedThisFrame) {
				paused = !paused;
				GameUI.Instance.pauseMenu.SetActive(paused);

				if (paused) {
					Time.timeScale = 0f;
					foreach (var (_, source) in audioSources) {
						source.Pause();
					}
				} else {
					Time.timeScale = 1f;
					foreach (var (_, source) in audioSources) {
						source.UnPause();
					}
				}
			}

			if (paused) {
				return;
			}

			var leaderAudioSource = audioSources.First().Value;
			if (!GameManager.Instance.useAudioTime || !leaderAudioSource.isPlaying) {
				realSongTime += Time.deltaTime * speed;
			} else {
				// TODO: Use "timeSamples" for better accuracy
				realSongTime = leaderAudioSource.time;
			}

			// Audio raising and lowering based on player preformance
			UpdateAudio("guitar", "realGuitar", "guitar", null);
			UpdateAudio("bass", "realBass", "bass", "rhythm");
			UpdateAudio("keys", "realKeys", "keys", null);

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

		private void UpdateAudio(string name, string altName, string audioName, string altAudioName = null) {
			// Skip if that audio track doesn't exist and alt track doesn't exist
			if (!audioSources.TryGetValue(audioName, out AudioSource audioSource)) {
				if (altAudioName == null || !audioSources.TryGetValue(altAudioName, out audioSource)) {
					return;
				}
			}

			int total = PlayerManager.PlayersWithInstrument(name) +
				PlayerManager.PlayersWithInstrument(altName);

			// Skip if no one is playing the instrument
			if (total <= 0) {
				return;
			}

			float percent = 1f - (float) (audioLowering.GetCount(name) +
				audioLowering.GetCount(altName)) / total;

			// Lower volume to a minimum of 5%
			audioSource.volume = percent * 0.95f + 0.05f;
		}

		public void Exit() {
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