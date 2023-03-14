using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using YARG.Data;
using YARG.Input;
using YARG.Pools;
using YARG.UI;
using YARG.Util;

namespace YARG.PlayMode {
	public sealed class MicPlayer : MonoBehaviour {
		private class PlayerInfo {
			public PlayerManager.Player player;

			public Transform needle;
			public GameObject needleModel;
			public ParticleSystem needleParticles;

			public MeshRenderer barMesh;

			public int octaveOffset;
			public float singProgress;

			public int sectionsHit;
			public int secitonsFailed;

			public float totalSingTime;
		}

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
		private Transform barContainer;

		[SerializeField]
		private TextMeshProUGUI preformaceText;
		[SerializeField]
		private TextMeshProUGUI comboText;
		[SerializeField]
		private Image comboFill;

		[SerializeField]
		private GameObject needlePrefab;
		[SerializeField]
		private GameObject barPrefab;

		[SerializeField]
		private Camera trackCamera;

		[SerializeField]
		private AudioMixerGroup silentMixerGroup;

		private List<PlayerInfo> micInputs = new();
		public Dictionary<MicInputStrategy, AudioSource> dummyAudioSources = new();

		public float RelativeTime => Play.Instance.SongTime +
			((TRACK_SPAWN_OFFSET + TRACK_END_OFFSET) / (TRACK_SPEED / Play.speed));

		private int visualChartIndex;
		private int chartIndex;
		private int visualEventChartIndex;
		private int eventChartIndex;

		private float sectionSingTime = -1f;
		private float totalSingTime;
		private LyricInfo currentLyric = null;

		private int rawMultiplier = 1;

		private void Start() {
			Instance = this;

			// Start mics
			bool hasMic = false;
			foreach (var player in PlayerManager.players) {
				if (player.inputStrategy is MicInputStrategy micStrategy) {
					// Skip if the player hasn't assigned a mic
					if (micStrategy.microphoneIndex == -1 && !micStrategy.botMode) {
						continue;
					}

					hasMic = true;

					// Spawn needle
					var needle = Instantiate(needlePrefab, transform);
					needle.transform.localPosition = needlePrefab.transform.position;

					// Spawn var
					var bar = Instantiate(barPrefab, barContainer);
					bar.transform.localPosition = new(0f, 0f, 0.8f - (barContainer.childCount - 1) * 0.225f);

					// Create player info
					var playerInfo = new PlayerInfo {
						player = player,

						needle = needle.transform,
						needleModel = needle.GetComponentInChildren<MeshRenderer>().gameObject,
						needleParticles = needle.GetComponentInChildren<ParticleSystem>(),

						barMesh = bar.GetComponent<MeshRenderer>()
					};

					// Add to players
					micInputs.Add(playerInfo);

					if (!micStrategy.botMode) {
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

			// Set scores
			foreach (var playerInfo in micInputs) {
				playerInfo.player.lastScore = new PlayerManager.LastScore {
					percentage = new DiffPercent {
						difficulty = playerInfo.player.chosenDifficulty,
						percent = playerInfo.totalSingTime / totalSingTime
					},
					notesHit = playerInfo.sectionsHit,
					notesMissed = playerInfo.secitonsFailed
				};
			}
		}

		private void Update() {
			// Ignore everything else until the song starts
			if (!Play.Instance.SongStarted) {
				return;
			}

			// Get the first lyric time
			if (sectionSingTime == -1f) {
				CalculateSectionSingTime(0f);
			}

			// Get chart
			var chart = Play.Instance.chart.realLyrics;

			var events = Play.Instance.chart.events;

			// Update event visuals
			while (events.Count > visualEventChartIndex && events[visualEventChartIndex].time <= RelativeTime) {
				var eventInfo = events[visualEventChartIndex];

				float compensation = TRACK_SPAWN_OFFSET - CalcLagCompensation(RelativeTime, eventInfo.time);
				if (eventInfo.name == "vocal_endPhrase") {
					notePool.AddEndPhraseLine(compensation);
				}

				visualEventChartIndex++;
			}

			// Update event logic
			while (events.Count > eventChartIndex && events[eventChartIndex].time <= Play.Instance.SongTime) {
				var eventInfo = events[eventChartIndex];

				if (eventInfo.name == "vocal_endPhrase") {
					float bestPercent = 0f;

					if (sectionSingTime != 0f) {
						// Reset and see if we failed or not
						foreach (var playerInfo in micInputs) {
							float mul = GetSingTimeMultiplier(playerInfo.player.chosenDifficulty);
							float percent = playerInfo.singProgress / (sectionSingTime * mul);

							if (percent >= 1f) {
								playerInfo.sectionsHit++;
							} else {
								playerInfo.secitonsFailed++;
							}

							if (percent > bestPercent) {
								bestPercent = percent;
							}

							playerInfo.totalSingTime += playerInfo.singProgress;
							playerInfo.singProgress = 0f;
						}

						// Set preformance text
						preformaceText.text = bestPercent switch {
							>= 1f => "AWESOME!",
							>= 0.8f => "STRONG",
							>= 0.7f => "GOOD",
							>= 0.6f => "OKAY",
							>= 0.1f => "MESSY",
							_ => "AWFUL"
						};
						preformaceText.color = Color.white;

						// Add to multiplier
						if (bestPercent >= 1f) {
							if (rawMultiplier < 4) {
								rawMultiplier++;
							}
						} else {
							rawMultiplier = 1;
						}
					}

					// Calculate the new sing time
					totalSingTime += sectionSingTime;
					CalculateSectionSingTime(Play.Instance.SongTime);
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

			// Update player specific stuff
			float highestSingProgress = 0f;
			foreach (var playerInfo in micInputs) {
				// (ONLY ONE FOR NOW)
				var player = playerInfo.player;
				var micInput = (MicInputStrategy) player.inputStrategy;

				// Update inputs
				if (micInput.botMode) {
					micInput.UpdateBotMode(chart, Play.Instance.SongTime);
				} else {
					micInput.UpdatePlayerMode();
				}

				// See if the pitch is correct 

				bool pitchCorrect = micInput.VoiceDetected;
				if (currentLyric != null && !currentLyric.inharmonic && micInput.VoiceDetected) {
					float correctRange = player.chosenDifficulty switch {
						Difficulty.EASY => 4f,
						Difficulty.MEDIUM => 4f,
						Difficulty.HARD => 3f,
						Difficulty.EXPERT => 2.5f,
						Difficulty.EXPERT_PLUS => 1f,
						_ => throw new System.Exception("Unreachable.")
					};

					// Get the needed pitch
					float timeIntoNote = Play.Instance.SongTime - currentLyric.time;
					float rawNote = currentLyric.GetLerpedNoteAtTime(timeIntoNote);
					var (neededNote, neededOctave) = Utils.SplitNoteToOctaveAndNote(rawNote);

					// Get the note the player is singing
					float currentNote = micInput.VoiceNote;

					// Check if it is in the right threshold
					float dist = Mathf.Abs(neededNote - currentNote);
					pitchCorrect = dist <= correctRange;

					// Get the octave offset
					if (pitchCorrect) {
						playerInfo.octaveOffset = neededOctave - micInput.VoiceOctave;
					}
				}

				// Update needle

				if (micInput.VoiceDetected) {
					playerInfo.needleModel.SetActive(true);
				} else {
					playerInfo.needleModel.SetActive(micInput.TimeSinceNoVoice < 0.25f);
				}

				if (pitchCorrect && currentLyric != null) {
					playerInfo.singProgress += Time.deltaTime;

					if (!playerInfo.needleParticles.isEmitting) {
						playerInfo.needleParticles.Play();
					}
				} else {
					if (playerInfo.needleParticles.isEmitting) {
						playerInfo.needleParticles.Stop();
					}
				}

				// Update needle
				float z = NoteAndOctaveToZ(micInput.VoiceNote, micInput.VoiceOctave + playerInfo.octaveOffset);
				playerInfo.needle.localPosition = Vector3.Lerp(
					playerInfo.needle.localPosition,
					playerInfo.needle.localPosition.WithZ(z),
					Time.deltaTime * 15f);

				// Update bar
				float singTimeModifier = GetSingTimeMultiplier(player.chosenDifficulty);
				if (sectionSingTime != 0f) {
					playerInfo.barMesh.material.SetFloat("Fill", playerInfo.singProgress / (sectionSingTime * singTimeModifier));
				} else {
					playerInfo.barMesh.material.SetFloat("Fill", 0f);
				}

				// Update highest
				if (playerInfo.singProgress > highestSingProgress) {
					highestSingProgress = playerInfo.singProgress;
				}
			}

			// Update preformance text fading
			var c = preformaceText.color;
			c.a -= Time.deltaTime * 2f;
			preformaceText.color = c;

			// Update combo text
			if (rawMultiplier == 1) {
				comboText.text = null;
			} else {
				comboText.text = $"{rawMultiplier}<sub>x</sub>";
			}

			// Update combo fill
			// TODO: Make only one difficulty option
			float fillMul = GetSingTimeMultiplier(micInputs[0].player.chosenDifficulty);
			if (sectionSingTime != 0f) {
				comboFill.fillAmount = highestSingProgress / (sectionSingTime * fillMul);
			} else {
				comboFill.fillAmount = 0f;
			}
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

		private void CalculateSectionSingTime(float start) {
			// Get the end of the section
			var end = 0f;
			foreach (var e in Play.Instance.chart.events) {
				if (e.time < start) {
					continue;
				}

				if (e.name != "vocal_endPhrase") {
					continue;
				}

				end = e.time;
				break;
			}

			// Get all of the lyric times combined
			sectionSingTime = 0f;
			foreach (var lyric in Play.Instance.chart.realLyrics) {
				if (lyric.time < start) {
					continue;
				}

				if (lyric.time > end) {
					break;
				}

				sectionSingTime += lyric.length;
			}
		}

		private float GetSingTimeMultiplier(Difficulty diff) {
			return diff switch {
				Difficulty.EASY => 0.45f,
				Difficulty.MEDIUM => 0.5f,
				Difficulty.HARD => 0.55f,
				Difficulty.EXPERT => 0.6f,
				Difficulty.EXPERT_PLUS => 0.65f,
				_ => throw new System.Exception("Unreachable.")
			};
		}

		private float CalcLagCompensation(float currentTime, float noteTime) {
			return (currentTime - noteTime) * (TRACK_SPEED / Play.speed);
		}

		public static float NoteAndOctaveToZ(float note, int octave) {
			float z = -0.353f +
				(note / 12f * 0.42f) +
				(octave - 3) * 0.42f;
			z = Mathf.Clamp(z, -0.45f, 0.93f);

			return z;
		}
	}
}
