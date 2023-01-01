using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using YARG.Data;
using YARG.Serialization;
using YARG.UI;

namespace YARG.PlayMode {
	public class Play : MonoBehaviour {
		public static Play Instance {
			get;
			private set;
		}

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

		public bool SongStarted {
			get;
			private set;
		} = false;

		private Dictionary<string, AudioSource> audioSources = new();
		private OccurrenceList<string> audioLowering = new();

		private float realSongTime = 0f;
		public float SongTime {
			get => realSongTime + PlayerManager.globalCalibration;
		}

		public Chart chart;

		private int beatIndex = 0;

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
				var track = Instantiate(trackPrefab,
					new Vector3(i * 25f, 0f, 0f), trackPrefab.transform.rotation);
				track.GetComponent<Track>().player = PlayerManager.players[i];
			}

			yield return new WaitForSeconds(SONG_START_OFFSET);

			// Start all audio at the same time
			foreach (var (_, audioSource) in audioSources) {
				audioSource.Play();
			}
			realSongTime = audioSources.First().Value.time;
			SongStarted = true;
		}

		private void Update() {
			if (!SongStarted) {
				return;
			}

			realSongTime += Time.deltaTime;

			// Audio raising and lowering based on player preformance
			UpdateAudio("guitar", "guitar");
			UpdateAudio("bass", "rhythm");
			UpdateAudio("keys", "keys");

			// Update beats
			while (chart.beats.Count > beatIndex && chart.beats[beatIndex] <= SongTime) {
				BeatEvent?.Invoke();
				beatIndex++;
			}

			// End song
			if (realSongTime > song.songLength.Value + 0.5f) {
				MainMenu.postSong = true;
				Exit();
			}
		}

		private void UpdateAudio(string name, string audioName) {
			// Skip if that audio track doesn't exist
			if (!audioSources.TryGetValue(audioName, out var audioSource)) {
				return;
			}

			int total = PlayerManager.PlayersWithInstrument(name);

			// Skip if no one is playing the instrument
			if (total <= 0) {
				return;
			}

			float percent = 1f - (float) audioLowering.GetCount(name) / total;

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