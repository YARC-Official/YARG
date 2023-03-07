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
		public const float TRACK_SPEED = 4f;

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

		private List<PlayerManager.Player> micInputs = new();
		public Dictionary<MicInputStrategy, AudioSource> dummyAudioSources = new();

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
					micInputs.Add(player);

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
				GameUI.Instance.RemoveVocalTrackImage();
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

			// (ONLY ONE FOR NOW)
			var player = micInputs[0];
			var micInput = (MicInputStrategy) player.inputStrategy;

			// Update inputs
			micInput.UpdatePlayerMode();

			// Get chart
			var chart = Play.Instance.chart.realLyrics[(int) player.chosenDifficulty];

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
			while (chart.Count > visualChartIndex && chart[visualChartIndex].time <= RelativeTime) {
				var lyricInfo = chart[visualChartIndex];

				SpawnLyric(lyricInfo, RelativeTime);
				visualChartIndex++;
			}

			// Set current lyric
			if (currentLyric == null) {
				while (chart.Count > chartIndex && chart[chartIndex].time <= Play.Instance.SongTime) {
					currentLyric = chart[chartIndex];
					chartIndex++;
				}
			} else if (currentLyric.EndTime < Play.Instance.SongTime) {
				currentLyric = null;
			}

			// See if the pitch is correct 

			bool pitchCorrect = micInput.VoiceDetected;
			if (currentLyric != null && !currentLyric.inharmonic && micInput.VoiceDetected) {
				float correctRange = player.chosenDifficulty switch {
					Difficulty.MEDIUM => 4f,
					Difficulty.HARD => 3f,
					Difficulty.EXPERT => 2f,
					Difficulty.EXPERT_PLUS => 0.5f,
					_ => throw new System.Exception("Unreachable.")
				};

				// Get the needed pitch
				float timeIntoNote = Play.Instance.SongTime - currentLyric.time;
				float neededNote = currentLyric.GetLerpedNoteAtTime(timeIntoNote);

				// Get the note the player is singing
				float currentNote = micInput.VoiceNote + micInput.VoiceOctave * 12f;

				// Check if it is in the right threshold
				float dist = Mathf.Abs(neededNote - currentNote);
				pitchCorrect = dist <= correctRange;
			}

			// Update needle

			needleModel.gameObject.SetActive(micInput.VoiceDetected);

			if (pitchCorrect && currentLyric != null) {
				if (!needleParticles.isEmitting) {
					needleParticles.Play();
				}
			} else {
				if (needleParticles.isEmitting) {
					needleParticles.Stop();
				}
			}

			float z = NoteAndOctaveToZ(micInput.VoiceNote, micInput.VoiceOctave);
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
				notePool.AddNoteHarmonic(lyricInfo.pitchOverTime, lyricInfo.length, pos);
			}
		}

		protected float CalcLagCompensation(float currentTime, float noteTime) {
			return (currentTime - noteTime) * (TRACK_SPEED / Play.speed);
		}

		public static float NoteAndOctaveToZ(float note, int octave) {
			float z = -0.353f +
				(note / 12f * 0.42f) +
				(octave - 3) * 0.42f;
			//z = Mathf.Clamp(z, -0.45f, 0.93f);

			return z;
		}
	}
}
