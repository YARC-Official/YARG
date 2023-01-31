using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
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

		public static float speed = 1.0f;

		public const float SONG_START_OFFSET = 1f;
		public const float HIT_MARGIN = 0.095f;
		public const bool ANCHORING = true;

		public static SongInfo song = null;

		public delegate void BeatAction();
		public event BeatAction BeatEvent;

		[SerializeField]
		private GameObject soundAudioPrefab;
		[SerializeField]
		private GameObject trackPrefab;
		[SerializeField]
		private GameObject realGuitarTrackPrefab;

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

			// Load midi
			var parser = new MidiParser(Path.Combine(song.folder.FullName, "notes.mid"), song.delay);
			chart = new Chart();
			parser.Parse(chart);

			// Spawn tracks
			for (int i = 0; i < PlayerManager.players.Count; i++) {
				string instrument = PlayerManager.players[i].chosenInstrument;

				GameObject track;
				if (instrument == "realGuitar" || instrument == "realBass") {
					track = Instantiate(realGuitarTrackPrefab, new Vector3(i * 25f, 0f, 0f),
						realGuitarTrackPrefab.transform.rotation);
				} else {
					track = Instantiate(trackPrefab, new Vector3(i * 25f, 0f, 0f),
						trackPrefab.transform.rotation);
				}

				track.GetComponent<AbstractTrack>().player = PlayerManager.players[i];
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

		private void Update() {
			if (!SongStarted) {
				return;
			}

			realSongTime += Time.deltaTime * speed;

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
			if (realSongTime > song.songLength.Value + 0.5f) {
				MainMenu.postSong = true;
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