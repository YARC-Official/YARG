using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering.Universal;
using YARG.Data;
using YARG.Input;
using YARG.Pools;
using YARG.UI;

namespace YARG.PlayMode {
	public class MicPlayer : MonoBehaviour {
		public const float TRACK_SPEED = 7f;

		private const float TRACK_SPAWN_OFFSET = 12f;
		private const float TRACK_END_OFFSET = 5f;

		public static MicPlayer Instance {
			get; private set;
		}

		[SerializeField]
		private LyricPool lyricPool;
		[SerializeField]
		private VocalNotePool notePool;

		[SerializeField]
		private Transform needle;
		[SerializeField]
		private GameObject needleModel;
		[SerializeField]
		private ParticleSystem needleParticles;

		[SerializeField]
		private Camera trackCamera;

		[SerializeField]
		private AudioMixerGroup silentMixerGroup;

		private List<MicInputStrategy> micInputs = new();
		public Dictionary<MicInputStrategy, AudioSource> dummyAudioSources = new();

		public List<LyricInfo> Chart => Play.Instance.chart.realLyrics;

		public float RelativeTime => Play.Instance.SongTime +
			((TRACK_SPAWN_OFFSET + TRACK_END_OFFSET) / (TRACK_SPEED / Play.speed));

		private int visualChartIndex = 0;
		private int chartIndex = 0;
		private int eventChartIndex = 0;

		private LyricInfo currentLyric = null;

		private void Start() {
			Instance = this;

			// Start mics
			bool hasMic = false;
			foreach (var player in PlayerManager.players) {
				if (player.inputStrategy is MicInputStrategy micStrategy) {
					if (micStrategy.microphoneIndex == -1) {
						continue;
					}

					hasMic = true;

					// Add to inputs
					micInputs.Add(micStrategy);

					// Add child dummy audio source (for mic input reading)
					var go = new GameObject();
					go.transform.parent = transform;
					var audio = go.AddComponent<AudioSource>();
					dummyAudioSources.Add(micStrategy, audio);
					audio.outputAudioMixerGroup = silentMixerGroup;
					audio.loop = true;

					// Start the mic!
					var micName = Microphone.devices[micStrategy.microphoneIndex];
					audio.clip = Microphone.Start(micName, true, 1, AudioSettings.outputSampleRate);

					// Wait for the mic to start, then start the audio
					while (Microphone.GetPosition(micName) <= 0) {
						// This loop is weird, but it works.
					}
					audio.Play();
				}
			}

			// Destroy if no mic is connected
			if (!hasMic) {
				Destroy(gameObject);
				return;
			}

			// Set up render texture
			var descriptor = new RenderTextureDescriptor(
				Screen.width, Screen.height,
				RenderTextureFormat.ARGBHalf
			);
			descriptor.mipCount = 0;
			var renderTexture = new RenderTexture(descriptor);
			trackCamera.targetTexture = renderTexture;

			// Set up camera
			var info = trackCamera.GetComponent<UniversalAdditionalCameraData>();
			if (GameManager.Instance.LowQualityMode) {
				info.antialiasing = AntialiasingMode.None;
			} else {
				info.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
				info.antialiasingQuality = AntialiasingQuality.Low;
			}

			// Set render texture on UI
			GameUI.Instance.SetVocalTrackImage(renderTexture);
		}

		private void OnDestroy() {
			// Release render texture
			trackCamera.targetTexture.Release();
		}

		private void Update() {
			// Ignore everything else until the song starts
			if (!Play.Instance.SongStarted) {
				return;
			}

			// Update inputs
			foreach (var inputStrategy in micInputs) {
				inputStrategy.UpdatePlayerMode();
			}

			// Update events
			var events = Play.Instance.chart.events;
			while (events.Count > eventChartIndex && events[eventChartIndex].time <= RelativeTime) {
				var eventInfo = events[eventChartIndex];

				float compensation = TRACK_SPAWN_OFFSET - CalcLagCompensation(RelativeTime, eventInfo.time);
				if (eventInfo.name == "vocal_endPhrase") {
					notePool.AddEndPhraseLine(compensation);
				}

				eventChartIndex++;
			}

			// Spawn lyrics
			while (Chart.Count > visualChartIndex && Chart[visualChartIndex].time <= RelativeTime) {
				var lyricInfo = Chart[visualChartIndex];

				SpawnLyric(lyricInfo, RelativeTime);
				visualChartIndex++;
			}

			// Set current lyric
			if (currentLyric == null) {
				while (Chart.Count > chartIndex && Chart[chartIndex].time <= Play.Instance.SongTime) {
					currentLyric = Chart[chartIndex];
					chartIndex++;
				}
			} else if (currentLyric.EndTime < Play.Instance.SongTime) {
				currentLyric = null;
			}

			// Update needle

			needleModel.gameObject.SetActive(micInputs[0].VoiceDetected);

			if (micInputs[0].VoiceDetected && currentLyric != null) {
				if (!needleParticles.isEmitting) {
					needleParticles.Play();
				}
			} else {
				if (needleParticles.isEmitting) {
					needleParticles.Stop();
				}
			}

			float z = -0.353f +
				(micInputs[0].VoiceNote / 12f * 0.42f) +
				(micInputs[0].VoiceOctave - 3) * 0.42f;
			z = Mathf.Clamp(z, -0.45f, 0.93f);

			needle.transform.localPosition = needle.transform.localPosition.WithZ(z);
		}

		private void SpawnLyric(LyricInfo lyricInfo, float time) {
			// Set correct position
			float lagCompensation = CalcLagCompensation(time, lyricInfo.time);
			var pos = TRACK_SPAWN_OFFSET - lagCompensation;

			// Spawn text
			lyricPool.AddLyric(lyricInfo.lyric, pos);

			// Spawn note
			if (lyricInfo.inharmonic) {
				notePool.AddNoteInharmonic(lyricInfo.length, pos);
			} else {
				// TODO
			}
		}

		protected float CalcLagCompensation(float currentTime, float noteTime) {
			return (currentTime - noteTime) * (TRACK_SPEED / Play.speed);
		}
	}
}
