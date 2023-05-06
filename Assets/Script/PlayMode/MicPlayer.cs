using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using YARG.Data;
using YARG.Input;
using YARG.Pools;
using YARG.Settings;
using YARG.UI;
using YARG.Util;

namespace YARG.PlayMode {
	public sealed class MicPlayer : MonoBehaviour {
		private class PlayerInfo {
			public PlayerManager.Player player;

			public Transform needle;
			public GameObject needleModel;

			public ParticleGroup nonActiveParticles;
			public ParticleGroup activeParticles;
			public Light needleLight;

			public int octaveOffset;
			public float[] singProgresses;

			public bool hittingNote;
		}

		public static readonly Color[] HARMONIC_COLORS = new Color[] {
			new Color32(0, 204, 255, 255),
			new Color32(255, 133, 0, 255),
			new Color32(255, 219, 0, 255)
		};

		public const float TRACK_SPEED = 4f;

		public const float TRACK_SPAWN_OFFSET = 12f;
		public const float TRACK_END_OFFSET = 5f;

		public const float STARPOWER_ACTIVATE_MARGIN = 0.1f;
		public const float STARPOWER_ACTIVATE_MIN = 0.2f;

		public static MicPlayer Instance {
			get; private set;
		}

		[SerializeField]
		private LyricPool lyricPool;
		[SerializeField]
		private VocalNotePool notePool;

		[Space]
		[SerializeField]
		private MeshRenderer trackRenderer;
		[SerializeField]
		private Texture2D harmonyTexture;

		[Space]
		[SerializeField]
		private TextMeshProUGUI preformaceText;
		[SerializeField]
		private TextMeshProUGUI comboText;
		[SerializeField]
		private Image comboFill;
		[SerializeField]
		private Image comboRim;
		[SerializeField]
		private Image comboSunburst;
		[SerializeField]
		private Image starpowerFill;
		[SerializeField]
		private Image starpowerBarOverlay;
		[SerializeField]
		private MeshRenderer starpowerOverlay;

		[Space]
		[SerializeField]
		private Sprite maxedComboFill;
		[SerializeField]
		private Sprite maxedComboRim;
		[SerializeField]
		private Sprite starpoweredComboRim;
		[SerializeField]
		private Sprite comboSunburstNormal;
		[SerializeField]
		private Sprite comboSunburstStarpower;

		private Sprite normalComboFill;
		private Sprite normalComboRim;

		[Space]
		[SerializeField]
		private GameObject needlePrefab;
		[SerializeField]
		private GameObject barContainer;
		[SerializeField]
		private Image[] barImages;

		[SerializeField]
		private Camera trackCamera;

		[SerializeField]
		private AudioMixerGroup silentMixerGroup;

		private bool hasMic = false;
		private List<PlayerInfo> micInputs = new();
		public Dictionary<MicInputStrategy, AudioSource> dummyAudioSources = new();

		private bool onSongStartCalled = false;

		private List<List<LyricInfo>> charts;

		public float RelativeTime => Play.Instance.SongTime +
			((TRACK_SPAWN_OFFSET + TRACK_END_OFFSET) / (TRACK_SPEED / Play.speed));

		private bool beat = false;

		private int[] visualChartIndex;
		private int[] chartIndex;
		private int visualEventChartIndex;
		private int eventChartIndex;

		private string EndPhraseName {
			get {
				string endPhraseName = "vocal_endPhrase";
				if (micInputs[0].player.chosenInstrument == "harmVocals") {
					endPhraseName = "harmVocal_endPhrase";
				}

				return endPhraseName;
			}
		}

		private float[] sectionSingTime;
		private LyricInfo[] currentLyrics;

		private EventInfo visualStarpowerSection;
		private EventInfo starpowerSection;

		private ScoreKeeper scoreKeeper;
		// easy, medium, hard, expert
		// https://rockband.scorehero.com/forum/viewtopic.php?t=4545
		// max harmony pts = 10% of main points per extra mic
		private readonly int[] MAX_POINTS = { 200, 400, 800, 1000, 1000 };
		private StarScoreKeeper starsKeeper;
		private int ptsPerPhrase; // pts per phrase, set depending on difficulty

		private int rawMultiplier = 1;
		private int Multiplier => rawMultiplier * (starpowerActive ? 2 : 1);

		private float starpowerCharge;
		private bool starpowerActive;

		public bool StarpowerReady => !starpowerActive && starpowerCharge >= 0.5f;

		private int sectionsHit;
		private int sectionsFailed;
		private float totalSingPercent;

		private string lastSecondHarmonyLyric = "";
		
		public float fontSize;
		public float animTimeLength;
		private float animTimeRemaining;

		private void Start() {
			Instance = this;

			normalComboFill = comboFill.sprite;
			normalComboRim = comboRim.sprite;

			// Start mics
			foreach (var player in PlayerManager.players) {
				// Skip people who are sitting out
				if (player.chosenInstrument != "vocals" && player.chosenInstrument != "harmVocals") {
					continue;
				}

				// Skip over non-mic strategy players
				if (player.inputStrategy is not MicInputStrategy micStrategy) {
					continue;
				}

				// Skip if the player hasn't assigned a mic
				if (micStrategy.microphoneIndex == InputStrategy.INVALID_MIC_INDEX && !micStrategy.botMode) {
					continue;
				}

				micStrategy.ResetForSong();
				hasMic = true;

				// Spawn needle
				var needle = Instantiate(needlePrefab, transform).GetComponent<VocalNeedle>();
				needle.transform.localPosition = needlePrefab.transform.position;

				// Create player info
				var playerInfo = new PlayerInfo {
					player = player,

					needle = needle.transform,
					needleModel = needle.meshRenderer.gameObject,
					nonActiveParticles = needle.nonActiveParticles,
					activeParticles = needle.activeParticles,
					needleLight = needle.needleLight,
				};

				// Bind events
				player.inputStrategy.StarpowerEvent += StarpowerAction;

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
			if (SettingsManager.Settings.LowQuality.Data) {
				info.antialiasing = AntialiasingMode.None;
			} else {
				info.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
				info.antialiasingQuality = AntialiasingQuality.Low;
			}

			// Set render texture on UI
			GameUI.Instance.SetVocalTrackImage(renderTexture);

			// Bind events
			Play.BeatEvent += BeatAction;

			// Hide starpower
			starpowerOverlay.material.SetFloat("AlphaMultiplier", 0f);

			// Setup scoring vars
			scoreKeeper = new();

			int phrases = 0;
			foreach (var ev in Play.Instance.chart.events) {
				if (ev.name == EndPhraseName)
					phrases++;
			}

			// note: micInput.Count = number of players on vocals
			ptsPerPhrase = MAX_POINTS[(int) micInputs[0].player.chosenDifficulty];
			starsKeeper = new(scoreKeeper, micInputs[0].player.chosenInstrument, phrases, ptsPerPhrase);
			
			// Prepare performance text characteristics
			fontSize = 24.0f;
			animTimeLength = 1.0f;
			animTimeRemaining = 0.0f;
			preformaceText.color = Color.white;
		}

		private void OnDestroy() {
			if (Instance == this) {
				Instance = null;
			}

			if (!hasMic) {
				return;
			}

			// Release render texture
			trackCamera.targetTexture.Release();

			// Create score
			int totalSections = sectionsFailed + sectionsHit;
			var score = new PlayerManager.LastScore {
				percentage = new DiffPercent {
					difficulty = micInputs[0].player.chosenDifficulty,
					percent = totalSingPercent / totalSections
				},
				score = new DiffScore {
					difficulty = micInputs[0].player.chosenDifficulty,
					score = (int) math.round(scoreKeeper.Score),
					stars = math.clamp((int) starsKeeper.Stars, 0, 6)
				},
				notesHit = sectionsHit,
				notesMissed = sectionsFailed
			};

			foreach (var playerInfo in micInputs) {
				// Set scores
				playerInfo.player.lastScore = score;

				// Unbind events
				playerInfo.player.inputStrategy.StarpowerEvent -= StarpowerAction;
			}

			// Unbind events
			Play.BeatEvent -= BeatAction;
		}

		private void OnSongStart() {
			// Get chart(s)
			if (micInputs[0].player.chosenInstrument == "harmVocals") {
				charts = Play.Instance.chart.harmLyrics.ToList();
			} else {
				charts = new List<List<LyricInfo>> { Play.Instance.chart.realLyrics };
			}

			// Set up harmony vocal track
			if (micInputs[0].player.chosenInstrument == "harmVocals") {
				trackRenderer.material.SetTexture("_BaseMap", harmonyTexture);
			}

			// Get count of harmony parts
			int harmonyCount = 1;
			if (micInputs[0].player.chosenInstrument == "harmVocals") {
				harmonyCount = Play.Instance.chart.harmLyrics.Length;
			}

			// Set up chart indices
			visualChartIndex = new int[harmonyCount];
			chartIndex = new int[harmonyCount];

			// Set up bars
			for (int i = 0; i < 3; i++) {
				barImages[i].color = HARMONIC_COLORS[i];
			}

			// Hide bars if solo
			if (harmonyCount == 1) {
				barContainer.SetActive(false);
			}

			// Set up sing progresses
			int botChartIndex = 0;
			foreach (var playerInfo in micInputs) {
				playerInfo.singProgresses = new float[harmonyCount];
				var player = playerInfo.player;
				var micInput = (MicInputStrategy) player.inputStrategy;

				// Update inputs
				if (micInput.botMode) {
					micInput.InitializeBotMode(charts[botChartIndex]);
					botChartIndex++;
					botChartIndex %= charts.Count;
				}
			}

			// Set up current lyrics
			currentLyrics = new LyricInfo[harmonyCount];
			sectionSingTime = new float[harmonyCount];
			CalculateSectionSingTime(0f);

			// Size starpower overlay
			if (charts.Count > 1) {
				starpowerOverlay.transform.localPosition = starpowerOverlay.transform.localPosition.WithZ(0f);
				starpowerOverlay.transform.localScale = starpowerOverlay.transform.localScale.WithY(1.18f);
			}
		}

		private void Update() {
			// Ignore everything else until the song starts
			if (!Play.Instance.SongStarted) {
				return;
			}

			// Ignore if paused
			if (Play.Instance.Paused) {
				return;
			}

			// Call "OnSongStart"
			if (!onSongStartCalled) {
				onSongStartCalled = true;
				OnSongStart();
			}

			var events = Play.Instance.chart.events;

			// Update event visuals
			while (events.Count > visualEventChartIndex && events[visualEventChartIndex].time <= RelativeTime) {
				var eventInfo = events[visualEventChartIndex];

				float compensation = TRACK_SPAWN_OFFSET - CalcLagCompensation(RelativeTime, eventInfo.time);
				if (eventInfo.name == EndPhraseName) {
					notePool.AddEndPhraseLine(compensation);
				} else if (eventInfo.name == "starpower_vocals") {
					visualStarpowerSection = eventInfo;
				}

				visualEventChartIndex++;
			}

			// Update visual starpower
			if (visualStarpowerSection?.EndTime < RelativeTime) {
				visualStarpowerSection = null;
			}

			// Update event logic
			while (events.Count > eventChartIndex && events[eventChartIndex].time <= Play.Instance.SongTime) {
				var eventInfo = events[eventChartIndex];

				if (eventInfo.name == EndPhraseName) {
					UpdateEndPhrase();
				} else if (eventInfo.name == "starpower_vocals") {
					starpowerSection = eventInfo;
				}

				eventChartIndex++;
			}

			for (int i = 0; i < charts.Count; i++) {
				var chart = charts[i];

				// Spawn lyrics and starpower activate sections
				while (chart.Count > visualChartIndex[i] && chart[visualChartIndex[i]].time <= RelativeTime) {
					var lyricInfo = chart[visualChartIndex[i]];

					SpawnLyric(lyricInfo, RelativeTime, i);

					if (i <= 1 && visualChartIndex[i] + 1 < chart.Count) {
						SpawnStarpowerActivate(lyricInfo, chart[visualChartIndex[i] + 1], RelativeTime, i == 1);
					}

					visualChartIndex[i]++;
				}

				// Set current lyric
				if (currentLyrics[i] == null) {
					while (chart.Count > chartIndex[i] && chart[chartIndex[i]].time <= Play.Instance.SongTime) {
						currentLyrics[i] = chart[chartIndex[i]];
						chartIndex[i]++;
					}
				} else if (currentLyrics[i].EndTime < Play.Instance.SongTime) {
					currentLyrics[i] = null;
				}
			}

			// Update player specific stuff
			foreach (var playerInfo in micInputs) {
				var player = playerInfo.player;
				var micInput = (MicInputStrategy) player.inputStrategy;

				// Get the correct range
				float correctRange = player.chosenDifficulty switch {
					Difficulty.EASY => 4f,
					Difficulty.MEDIUM => 4f,
					Difficulty.HARD => 3f,
					Difficulty.EXPERT => 2.5f,
					Difficulty.EXPERT_PLUS => 2.5f,
					_ => throw new Exception("Unreachable.")
				};

				// See if the (any) pitch is correct
				bool pitchCorrect = micInput.VoiceDetected;
				int targetLyricIndex = -1;
				if (micInput.VoiceDetected) {
					// Get the best matching pitch
					float bestDistance = float.MaxValue;
					int bestDistanceIndex = -1;
					int bestDistanceOctave = -1;
					for (int i = 0; i < currentLyrics.Length; i++) {
						var currentLyric = currentLyrics[i];

						if (currentLyric == null) {
							continue;
						}

						if (currentLyric.inharmonic) {
							bestDistance = correctRange;
							bestDistanceIndex = i;
							bestDistanceOctave = micInput.VoiceOctave;

							continue;
						}

						// Get the needed pitch
						float timeIntoNote = Play.Instance.SongTime - currentLyric.time;
						var (neededNote, neededOctave) = currentLyric.GetLerpedAndSplitNoteAtTime(timeIntoNote);

						// Get the note the player is singing
						float currentNote = micInput.VoiceNote;

						// Check if it is in the right threshold
						float dist = Mathf.Abs(neededNote - currentNote);

						// Check to see if it is the best
						if (dist < bestDistance) {
							bestDistance = dist;
							bestDistanceIndex = i;
							bestDistanceOctave = neededOctave;
						} else if (Mathf.Approximately(dist, bestDistance)) {
							// If it is the same distance, check the octave distance
							int bestOctaveDistance = Mathf.Abs(bestDistanceOctave - micInput.VoiceOctave);
							int neededOctaveDistance = Mathf.Abs(neededOctave - micInput.VoiceOctave);

							// Close one wins!
							if (neededOctaveDistance < bestOctaveDistance) {
								bestDistance = dist;
								bestDistanceIndex = i;
								bestDistanceOctave = neededOctave;
							}
						}
					}

					// Check if the best pitch is in the threshold
					if (bestDistanceIndex != -1) {
						pitchCorrect = bestDistance <= correctRange;

						// Get the octave offset
						if (pitchCorrect) {
							playerInfo.octaveOffset = bestDistanceOctave - micInput.VoiceOctave;
						}

						targetLyricIndex = bestDistanceIndex;
					}
				}

				// Update needle
				if (micInput.VoiceDetected) {
					playerInfo.needleModel.SetActive(true);
				} else {
					playerInfo.needleModel.SetActive(micInput.TimeSinceNoVoice < 0.25f);
				}


				if (pitchCorrect && targetLyricIndex != -1) {
					playerInfo.hittingNote = true;
					playerInfo.singProgresses[targetLyricIndex] += Time.deltaTime;

					playerInfo.activeParticles.Play();
					playerInfo.nonActiveParticles.Stop();

					// Fade in the needle light
					playerInfo.needleLight.intensity =
						Mathf.Lerp(playerInfo.needleLight.intensity, 0.35f,
						Time.deltaTime * 8f);

					// Changes colors of particles according to the note hit.
					playerInfo.needleLight.color = HARMONIC_COLORS[targetLyricIndex];
					playerInfo.activeParticles.Colorize(HARMONIC_COLORS[targetLyricIndex]);
				} else {
					playerInfo.hittingNote = false;

					// Fade out the needle light
					playerInfo.needleLight.intensity =
						Mathf.Lerp(playerInfo.needleLight.intensity, 0f,
						Time.deltaTime * 8f);

					playerInfo.activeParticles.Stop();

					if (micInput.VoiceDetected) {
						playerInfo.nonActiveParticles.Play();
					} else {
						playerInfo.nonActiveParticles.Stop();
					}
				}

				// Update needle
				float z = NoteAndOctaveToZ(micInput.VoiceNote, micInput.VoiceOctave + playerInfo.octaveOffset);
				playerInfo.needle.localPosition = Vector3.Lerp(
					playerInfo.needle.localPosition,
					playerInfo.needle.localPosition.WithZ(z),
					Time.deltaTime * 15f);
			}

			// Get the highest sing progresses
			float[] highestSingProgresses = new float[currentLyrics.Length];
			foreach (var playerInfo in micInputs) {
				float singTimeMultiplier = GetSingTimeMultiplier(playerInfo.player.chosenDifficulty);
				for (int i = 0; i < playerInfo.singProgresses.Length; i++) {
					float realProgress = playerInfo.singProgresses[i] / (sectionSingTime[i] * singTimeMultiplier);

					if (highestSingProgresses[i] < realProgress) {
						highestSingProgresses[i] = realProgress;
					}
				}
			}

			// Update bars
			for (int i = 0; i < highestSingProgresses.Length; i++) {
				if (sectionSingTime[i] != 0f) {
					barImages[i].fillAmount = highestSingProgresses[i];
				} else {
					barImages[i].fillAmount = 0f;
				}
			}

			// Animate performance text over one second
			animTimeRemaining -= Time.deltaTime;
			preformaceText.fontSize = PerformanceTextFontSize(fontSize, animTimeLength, animTimeRemaining);

			// Update combo text
			if (Multiplier == 1) {
				comboText.text = null;
			} else {
				comboText.text = $"{Multiplier}<sub>x</sub>";
			}

			// Update combo fill
			comboFill.fillAmount = 0f;
			for (int i = 0; i < highestSingProgresses.Length; i++) {
				if (sectionSingTime[i] != 0f) {
					comboFill.fillAmount = highestSingProgresses.Max();
					break;
				}
			}

			// Show/hide maxed out combo stuff
			if (rawMultiplier >= 4) {
				comboSunburst.gameObject.SetActive(true);
				comboSunburst.transform.Rotate(0f, 0f, Time.deltaTime * -25f);

				comboFill.sprite = maxedComboFill;
				if (starpowerActive) {
					comboRim.sprite = starpoweredComboRim;
					comboSunburst.sprite = comboSunburstStarpower;
				} else {
					comboRim.sprite = maxedComboRim;
					comboSunburst.sprite = comboSunburstNormal;
				}
			} else {
				comboSunburst.gameObject.SetActive(false);

				comboFill.sprite = normalComboFill;
				comboRim.sprite = normalComboRim;
			}

			// Update starpower active
			if (starpowerActive) {
				if (starpowerCharge <= 0f) {
					starpowerActive = false;
					starpowerCharge = 0f;

				} else {
					starpowerCharge -= Time.deltaTime / 25f;
				}
			}

			// Update starpower fill
			starpowerFill.fillAmount = starpowerCharge;
			starpowerBarOverlay.fillAmount = starpowerCharge;

			// Update starpower bar overlay
			if (beat) {
				float pulseAmount = 0f;
				if (starpowerActive) {
					pulseAmount = 0.25f;
				} else if (!starpowerActive && starpowerCharge >= 0.5f) {
					pulseAmount = 1f;
				}

				starpowerBarOverlay.color = new Color(1f, 1f, 1f, pulseAmount);
			} else {
				var col = starpowerBarOverlay.color;
				col.a = Mathf.Lerp(col.a, 0f, Time.deltaTime * 16f);
				starpowerBarOverlay.color = col;
			}

			// Show/hide starpower overlay
			float currentStarpower = starpowerOverlay.material.GetFloat("AlphaMultiplier");
			if (starpowerActive) {
				starpowerOverlay.material.SetFloat("AlphaMultiplier",
					Mathf.Lerp(currentStarpower, 1f, Time.deltaTime * 2f));
			} else {
				starpowerOverlay.material.SetFloat("AlphaMultiplier",
					Mathf.Lerp(currentStarpower, 0f, Time.deltaTime * 4f));
			}

			// Unset
			beat = false;
		}

		private void UpdateEndPhrase() {
			// Skip if there is no singing
			if (sectionSingTime.Max() <= 0f) {
				// Calculate the new sing time
				CalculateSectionSingTime(Play.Instance.SongTime);

				return;
			}

			float bestPercent = 0f;

			// Reset and see if we failed or not
			foreach (var playerInfo in micInputs) {
				float mul = GetSingTimeMultiplier(playerInfo.player.chosenDifficulty);
				float bestPlayerPercent = 0f;
				for (int i = 0; i < playerInfo.singProgresses.Length; i++) {
					if (sectionSingTime[i] == 0f) {
						continue;
					}

					// Get percent (0-1)
					float percent = playerInfo.singProgresses[i] / (sectionSingTime[i] * mul);

					// Set best (player) percent
					playerInfo.singProgresses[i] = 0f;
					if (percent > bestPlayerPercent) {
						bestPlayerPercent = percent;
					} else {
						continue;
					}

					// Set best percent
					if (percent > bestPercent) {
						bestPercent = percent;
					}
				}
			}

			// Get portion sang from bar graphic
			// WHAT. Redo this!
			var sectionPercents = new List<float>();
			foreach (var bar in barImages) {
				sectionPercents.Add(bar.fillAmount);
			}
			sectionPercents.Sort();

			// Add to ScoreKeeper
			for (int i = sectionPercents.Count - 1; i >= 0; --i) {
				var phraseScore = math.clamp((double) sectionPercents[i] * ptsPerPhrase, 0, ptsPerPhrase);
				if (i != sectionPercents.Count - 1) {
					phraseScore *= 0.1;
				}
				scoreKeeper.Add(Multiplier * phraseScore);
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

			// Begin animation and start countdown
			animTimeRemaining = animTimeLength;

			// Add to sing percent
			totalSingPercent += Mathf.Min(bestPercent, 1f);

			// Add to multiplier
			if (bestPercent >= 1f) {
				if (rawMultiplier < 4) {
					rawMultiplier++;
				}

				// Starpower
				if (starpowerSection != null && starpowerSection.EndTime <= Play.Instance.SongTime) {
					starpowerCharge += 0.25f;
					starpowerSection = null;
				}

				sectionsHit++;
			} else {
				rawMultiplier = 1;

				sectionsFailed++;
			}

			// Calculate the new sing time
			CalculateSectionSingTime(Play.Instance.SongTime);
		}

		private void SpawnLyric(LyricInfo lyricInfo, float time, int harmIndex) {
			// Get correct position
			float lagCompensation = CalcLagCompensation(time, lyricInfo.time);
			var pos = TRACK_SPAWN_OFFSET - lagCompensation;

			// Spawn text
			if (harmIndex == 0) {
				lyricPool.AddLyric(lyricInfo, visualStarpowerSection != null, pos, false);
			} else if (harmIndex == 1) {
				lyricPool.AddLyric(lyricInfo, visualStarpowerSection != null, pos, true);
				lastSecondHarmonyLyric = lyricInfo.lyric;
			} else if (harmIndex == 2 && lastSecondHarmonyLyric != lyricInfo.lyric) {
				// Only add if it's not the same as the last second harmony
				lyricPool.AddLyric(lyricInfo, visualStarpowerSection != null, pos, true);
			}

			// Spawn note
			if (lyricInfo.inharmonic) {
				notePool.AddNoteInharmonic(lyricInfo.length, pos, charts.Count != 1, harmIndex);
			} else {
				notePool.AddNoteHarmonic(lyricInfo.pitchOverTime, lyricInfo.length, pos, harmIndex);
			}
		}

		private void SpawnStarpowerActivate(LyricInfo firstLyric, LyricInfo nextLyric, float time, bool onTop) {
			float start = firstLyric.EndTime + STARPOWER_ACTIVATE_MARGIN;
			float end = nextLyric.time - STARPOWER_ACTIVATE_MARGIN;
			float length = end - start;

			if (length < STARPOWER_ACTIVATE_MIN) {
				return;
			}

			// Get correct position
			float lagCompensation = CalcLagCompensation(time, start);
			var pos = TRACK_SPAWN_OFFSET - lagCompensation;

			// Spawn section
			lyricPool.AddStarpowerActivate(pos, length, onTop);
		}

		private void CalculateSectionSingTime(float start) {
			// Get the end of the section
			var end = 0f;
			foreach (var e in Play.Instance.chart.events) {
				if (e.time < start) {
					continue;
				}

				if (e.name != EndPhraseName) {
					continue;
				}

				end = e.time;
				break;
			}

			// Get all of the lyric times combined
			for (int i = 0; i < charts.Count; i++) {
				sectionSingTime[i] = 0f;
				foreach (var lyric in charts[i]) {
					if (lyric.time < start) {
						continue;
					}

					if (lyric.time > end) {
						break;
					}

					sectionSingTime[i] += lyric.length;
				}
			}
		}

		private float GetSingTimeMultiplier(Difficulty diff) {
			return diff switch {
				Difficulty.EASY => 0.45f,
				Difficulty.MEDIUM => 0.5f,
				Difficulty.HARD => 0.55f,
				Difficulty.EXPERT => 0.6f,
				Difficulty.EXPERT_PLUS => 0.7f,
				_ => throw new Exception("Unreachable.")
			};
		}

		private float CalcLagCompensation(float currentTime, float noteTime) {
			return (currentTime - noteTime) * (TRACK_SPEED / Play.speed);
		}

		public static float NoteAndOctaveToZ(float note, int octave) {
			float z = -0.353f +
				(note / 12f * 0.42f) +
				(octave - 3) * 0.42f;

			if (Instance.micInputs[0].player.chosenInstrument == "harmVocals") {
				z = Mathf.Clamp(z, -0.61f, 0.61f);
			} else {
				z = Mathf.Clamp(z, -0.61f, 0.93f);
			}

			return z;
		}

		private void BeatAction() {
			beat = true;
		}

		private void StarpowerAction(InputStrategy inputStrategy) {
			if (starpowerCharge < 0.5f) {
				return;
			}

			// Get playerInfo
			var playerInfo = micInputs.Find(x => x.player.inputStrategy == inputStrategy);
			if (playerInfo == null) {
				return;
			}

			// See if we are in a starpower activate section
			bool inStarpowerSection = false;
			for (int i = 0; i < charts.Count; i++) {
				var chart = charts[i];

				// Only top and bottom
				if (i >= 2) {
					break;
				}

				if (chartIndex[i] - 1 > 0 && chartIndex[i] < chart.Count) {
					var firstLyric = chart[chartIndex[i] - 1];
					var nextLyric = chart[chartIndex[i]];

					float start = firstLyric.EndTime + STARPOWER_ACTIVATE_MARGIN;
					float end = nextLyric.time - STARPOWER_ACTIVATE_MARGIN;
					float length = end - start;

					if (length < STARPOWER_ACTIVATE_MIN) {
						continue;
					}

					if (Play.Instance.SongTime < start || Play.Instance.SongTime > end) {
						continue;
					}

					inStarpowerSection = true;
					break;
				}
			}

			// If so, activate!
			if (inStarpowerSection && !playerInfo.hittingNote) {
				starpowerActive = true;
			}
		}

		private float PerformanceTextFontSize(float fontSize, float animTimeLength,
			float animTimeRemaining, bool representsPeakSize = false) {
			// Define the relative size before font sizing is applied
			float relativeSize = 0.0f;
			float halfwayPoint = animTimeLength / 2.0f;

			// Determine the text size relative to its countdown timestamp
			if ((animTimeRemaining > animTimeLength) || (animTimeRemaining < 0.0f)) {
				relativeSize = 0.0f;
			} else if (animTimeRemaining >= halfwayPoint) {
				if (animTimeRemaining > (animTimeLength - 0.16666f)) {
					relativeSize = (-1290.0f * Mathf.Pow(
						animTimeRemaining - animTimeLength + 0.16666f, 4.0f)) + 0.9984f;
				} else {
					float denominator = 1.0f + Mathf.Pow((float) Math.E,
						-50.0f * (animTimeRemaining - animTimeLength + 0.25f));
					relativeSize = (0.1f / denominator) + 0.9f;
				}
			} else if (animTimeRemaining < halfwayPoint) {
				if (animTimeRemaining < 0.16666f) {
					relativeSize = (-1290.0f * Mathf.Pow(
						animTimeRemaining - 0.16666f, 4.0f)) + 0.9984f;
				} else {
					float denominator = 1.0f + Mathf.Pow((float) Math.E,
						-50.0f * (animTimeRemaining - 0.25f));
					relativeSize = (-0.1f / denominator) + 1.0f;
				}
			}

			// Consider if fontSize represents the "peak" or the "rest" size
			return relativeSize * fontSize * (representsPeakSize ? 1.0f : 1.11111f);
		}
	}
}