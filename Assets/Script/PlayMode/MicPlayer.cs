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
			public PlayerManager.Player Player;

			public Transform Needle;
			public GameObject NeedleModel;

			public ParticleGroup NonActiveParticles;
			public ParticleGroup ActiveParticles;
			public Light NeedleLight;

			public int OctaveOffset;
			public float[] SingProgresses;

			public bool HittingNote;
		}

		public static readonly Color[] HarmonicColors = {
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

		private Sprite _normalComboFill;
		private Sprite _normalComboRim;

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

		private bool _hasMic = false;
		private List<PlayerInfo> _micInputs = new();

		private bool _onSongStartCalled = false;

		private List<List<LyricInfo>> _charts;

		private float RelativeTime => Play.Instance.SongTime +
			((TRACK_SPAWN_OFFSET + TRACK_END_OFFSET) / (TRACK_SPEED / Play.speed));

		private bool _beat = false;

		private int[] _visualChartIndex;
		private int[] _chartIndex;
		private int _visualEventChartIndex;
		private int _eventChartIndex;

		private string EndPhraseName {
			get {
				string endPhraseName = "vocal_endPhrase";
				if (_micInputs[0].Player.chosenInstrument == "harmVocals") {
					endPhraseName = "harmVocal_endPhrase";
				}

				return endPhraseName;
			}
		}

		private float[] _sectionSingTime;
		private LyricInfo[] _currentLyrics;

		private EventInfo _visualStarpowerSection;
		private EventInfo _starpowerSection;

		private ScoreKeeper _scoreKeeper;
		// easy, medium, hard, expert
		// https://rockband.scorehero.com/forum/viewtopic.php?t=4545
		// max harmony pts = 10% of main points per extra mic
		private readonly int[] _maxPoints = { 400, 800, 1600, 2000, 2000 };
		private StarScoreKeeper _starsKeeper;
		private int _ptsPerPhrase; // pts per phrase, set depending on difficulty

		private int _rawMultiplier = 1;
		private int Multiplier => _rawMultiplier * (_starpowerActive ? 2 : 1);

		private float _starpowerCharge;
		private bool _starpowerActive;

		public bool StarpowerReady => !_starpowerActive && _starpowerCharge >= 0.5f;

		private int _sectionsHit;
		private int _sectionsFailed;
		private float _totalSingPercent;

		private string _lastSecondHarmonyLyric = "";

		[Space]
		public PerformanceTextSizer PerfTextSizer;
		public float fontSize;
		public float animTimeLength;

		private void Start() {
			Instance = this;

			_normalComboFill = comboFill.sprite;
			_normalComboRim = comboRim.sprite;

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
				if (micStrategy.MicDevice == null) {
					continue;
				}

				micStrategy.ResetForSong();
				_hasMic = true;

				// Spawn needle
				var needle = Instantiate(needlePrefab, transform).GetComponent<VocalNeedle>();
				needle.transform.localPosition = needlePrefab.transform.position;

				// Create player info
				var playerInfo = new PlayerInfo {
					Player = player,

					Needle = needle.transform,
					NeedleModel = needle.meshRenderer.gameObject,
					NonActiveParticles = needle.nonActiveParticles,
					ActiveParticles = needle.activeParticles,
					NeedleLight = needle.needleLight,
				};

				// Bind events
				player.inputStrategy.PauseEvent += PauseAction;
				player.inputStrategy.StarpowerEvent += StarpowerAction;

				// Add to players
				_micInputs.Add(playerInfo);
			}

			// Destroy if no mic is connected
			if (!_hasMic) {
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
			_scoreKeeper = new();

			int phrases = 0;
			foreach (var ev in Play.Instance.chart.events) {
				if (ev.name == EndPhraseName)
					phrases++;
			}

			// note: micInput.Count = number of players on vocals
			_ptsPerPhrase = _maxPoints[(int) _micInputs[0].Player.chosenDifficulty];
			_starsKeeper = new(_scoreKeeper, _micInputs[0].Player.chosenInstrument, phrases, _ptsPerPhrase);

			// Prepare performance text characteristics
			PerfTextSizer = new PerformanceTextSizer(fontSize, animTimeLength);
			preformaceText.color = Color.white;
		}

		public void SetPlayerScore() {
			// Create score
			int totalSections = _sectionsFailed + _sectionsHit;
			var score = new PlayerManager.LastScore {
				percentage = new DiffPercent {
					difficulty = _micInputs[0].Player.chosenDifficulty,
					percent = _totalSingPercent / totalSections
				},
				score = new DiffScore {
					difficulty = _micInputs[0].Player.chosenDifficulty,
					score = (int) math.round(_scoreKeeper.Score),
					stars = math.clamp((int) _starsKeeper.Stars, 0, 6)
				},
				notesHit = _sectionsHit,
				notesMissed = _sectionsFailed
			};

			foreach (var playerInfo in _micInputs) {
				// Set scores
				playerInfo.Player.lastScore = score;

				// Unbind events
				playerInfo.Player.inputStrategy.PauseEvent -= PauseAction;
				playerInfo.Player.inputStrategy.StarpowerEvent -= StarpowerAction;
			}
		}

		private void OnDestroy() {
			if (Instance == this) {
				Instance = null;
			}

			if (!_hasMic) {
				return;
			}

			// Release render texture
			trackCamera.targetTexture.Release();

			SetPlayerScore();

			// Unbind events
			Play.BeatEvent -= BeatAction;
		}

		private void OnSongStart() {
			// Get chart(s)
			if (_micInputs[0].Player.chosenInstrument == "harmVocals") {
				_charts = Play.Instance.chart.harmLyrics.ToList();
			} else {
				_charts = new List<List<LyricInfo>> { Play.Instance.chart.realLyrics };
			}

			// Set up harmony vocal track
			if (_micInputs[0].Player.chosenInstrument == "harmVocals") {
				trackRenderer.material.SetTexture("_BaseMap", harmonyTexture);
			}

			// Get count of harmony parts
			int harmonyCount = 1;
			if (_micInputs[0].Player.chosenInstrument == "harmVocals") {
				harmonyCount = Play.Instance.chart.harmLyrics.Length;
			}

			// Set up chart indices
			_visualChartIndex = new int[harmonyCount];
			_chartIndex = new int[harmonyCount];

			// Set up bars
			for (int i = 0; i < 3; i++) {
				barImages[i].color = HarmonicColors[i];
			}

			// Hide bars if solo
			if (harmonyCount == 1) {
				barContainer.SetActive(false);
			}

			// Set up sing progresses
			int botChartIndex = 0;
			foreach (var playerInfo in _micInputs) {
				playerInfo.SingProgresses = new float[harmonyCount];
				var player = playerInfo.Player;
				var micInput = (MicInputStrategy) player.inputStrategy;

				// Update inputs
				if (micInput.BotMode) {
					micInput.InitializeBotMode(_charts[botChartIndex]);
					botChartIndex++;
					botChartIndex %= _charts.Count;
				}
			}

			// Set up current lyrics
			_currentLyrics = new LyricInfo[harmonyCount];
			_sectionSingTime = new float[harmonyCount];
			CalculateSectionSingTime(0f);

			// Size starpower overlay
			if (_charts.Count > 1) {
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
			if (!_onSongStartCalled) {
				_onSongStartCalled = true;
				OnSongStart();
			}

			var events = Play.Instance.chart.events;

			// Update event visuals
			while (events.Count > _visualEventChartIndex && events[_visualEventChartIndex].time <= RelativeTime) {
				var eventInfo = events[_visualEventChartIndex];

				float compensation = TRACK_SPAWN_OFFSET - CalcLagCompensation(RelativeTime, eventInfo.time);
				if (eventInfo.name == EndPhraseName) {
					notePool.AddEndPhraseLine(compensation);
				} else if (eventInfo.name == "starpower_vocals") {
					_visualStarpowerSection = eventInfo;
				}

				_visualEventChartIndex++;
			}

			// Update visual starpower
			if (_visualStarpowerSection?.EndTime < RelativeTime) {
				_visualStarpowerSection = null;
			}

			// Update event logic
			while (events.Count > _eventChartIndex && events[_eventChartIndex].time <= Play.Instance.SongTime) {
				var eventInfo = events[_eventChartIndex];

				if (eventInfo.name == EndPhraseName) {
					UpdateEndPhrase();
				} else if (eventInfo.name == "starpower_vocals") {
					_starpowerSection = eventInfo;
				}

				_eventChartIndex++;
			}

			for (int i = 0; i < _charts.Count; i++) {
				var chart = _charts[i];

				// Spawn lyrics and starpower activate sections
				while (chart.Count > _visualChartIndex[i] && chart[_visualChartIndex[i]].time <= RelativeTime) {
					var lyricInfo = chart[_visualChartIndex[i]];

					SpawnLyric(lyricInfo, RelativeTime, i);

					if (i <= 1 && _visualChartIndex[i] + 1 < chart.Count) {
						SpawnStarpowerActivate(lyricInfo, chart[_visualChartIndex[i] + 1], RelativeTime, i == 1);
					}

					_visualChartIndex[i]++;
				}

				// Set current lyric
				if (_currentLyrics[i] == null) {
					while (chart.Count > _chartIndex[i] && chart[_chartIndex[i]].time <= Play.Instance.SongTime) {
						_currentLyrics[i] = chart[_chartIndex[i]];
						_chartIndex[i]++;
					}
				} else if (_currentLyrics[i].EndTime < Play.Instance.SongTime) {
					_currentLyrics[i] = null;
				}
			}

			// Update player specific stuff
			foreach (var playerInfo in _micInputs) {
				var player = playerInfo.Player;
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
					for (int i = 0; i < _currentLyrics.Length; i++) {
						var currentLyric = _currentLyrics[i];

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
							playerInfo.OctaveOffset = bestDistanceOctave - micInput.VoiceOctave;
						}

						targetLyricIndex = bestDistanceIndex;
					}
				}

				// Update needle
				if (micInput.VoiceDetected) {
					playerInfo.NeedleModel.SetActive(true);
				} else {
					playerInfo.NeedleModel.SetActive(micInput.TimeSinceNoVoice < 0.25f);
				}


				if (pitchCorrect && targetLyricIndex != -1) {
					playerInfo.HittingNote = true;
					playerInfo.SingProgresses[targetLyricIndex] += Time.deltaTime;

					playerInfo.ActiveParticles.Play();
					playerInfo.NonActiveParticles.Stop();

					// Fade in the needle light
					playerInfo.NeedleLight.intensity =
						Mathf.Lerp(playerInfo.NeedleLight.intensity, 0.35f,
						Time.deltaTime * 8f);

					// Changes colors of particles according to the note hit.
					playerInfo.NeedleLight.color = HarmonicColors[targetLyricIndex];
					playerInfo.ActiveParticles.Colorize(HarmonicColors[targetLyricIndex]);
				} else {
					playerInfo.HittingNote = false;

					// Fade out the needle light
					playerInfo.NeedleLight.intensity =
						Mathf.Lerp(playerInfo.NeedleLight.intensity, 0f,
						Time.deltaTime * 8f);

					playerInfo.ActiveParticles.Stop();

					if (micInput.VoiceDetected) {
						playerInfo.NonActiveParticles.Play();
					} else {
						playerInfo.NonActiveParticles.Stop();
					}
				}

				// Update needle
				float z = NoteAndOctaveToZ(micInput.VoiceNote, micInput.VoiceOctave + playerInfo.OctaveOffset);
				playerInfo.Needle.localPosition = Vector3.Lerp(
					playerInfo.Needle.localPosition,
					playerInfo.Needle.localPosition.WithZ(z),
					Time.deltaTime * 15f);
			}

			// Get the highest sing progresses
			float[] highestSingProgresses = new float[_currentLyrics.Length];
			foreach (var playerInfo in _micInputs) {
				float singTimeMultiplier = GetSingTimeMultiplier(playerInfo.Player.chosenDifficulty);
				for (int i = 0; i < playerInfo.SingProgresses.Length; i++) {
					float realProgress = playerInfo.SingProgresses[i] / (_sectionSingTime[i] * singTimeMultiplier);

					if (highestSingProgresses[i] < realProgress) {
						highestSingProgresses[i] = realProgress;
					}
				}
			}

			// Update bars
			for (int i = 0; i < highestSingProgresses.Length; i++) {
				if (_sectionSingTime[i] != 0f) {
					barImages[i].fillAmount = highestSingProgresses[i];
				} else {
					barImages[i].fillAmount = 0f;
				}
			}

			// Animate and get performance text size given the current timestamp
			PerfTextSizer.AnimTimeRemaining -= Time.deltaTime;
			preformaceText.fontSize = PerfTextSizer.PerformanceTextFontSize();

			// Update combo text
			if (Multiplier == 1) {
				comboText.text = null;
			} else {
				comboText.text = $"{Multiplier}<sub>x</sub>";
			}

			// Update combo fill
			comboFill.fillAmount = 0f;
			for (int i = 0; i < highestSingProgresses.Length; i++) {
				if (_sectionSingTime[i] != 0f) {
					comboFill.fillAmount = highestSingProgresses.Max();
					break;
				}
			}

			// Show/hide maxed out combo stuff
			if (_rawMultiplier >= 4) {
				comboSunburst.gameObject.SetActive(true);
				comboSunburst.transform.Rotate(0f, 0f, Time.deltaTime * -25f);

				comboFill.sprite = maxedComboFill;
				if (_starpowerActive) {
					comboRim.sprite = starpoweredComboRim;
					comboSunburst.sprite = comboSunburstStarpower;
				} else {
					comboRim.sprite = maxedComboRim;
					comboSunburst.sprite = comboSunburstNormal;
				}
			} else {
				comboSunburst.gameObject.SetActive(false);

				comboFill.sprite = _normalComboFill;
				comboRim.sprite = _normalComboRim;
			}

			// Update starpower active
			if (_starpowerActive) {
				if (_starpowerCharge <= 0f) {
					_starpowerActive = false;
					_starpowerCharge = 0f;

				} else {
					_starpowerCharge -= Time.deltaTime / 25f;
				}
			}

			// Update starpower fill
			starpowerFill.fillAmount = _starpowerCharge;
			starpowerBarOverlay.fillAmount = _starpowerCharge;

			// Update starpower bar overlay
			if (_beat) {
				float pulseAmount = 0f;
				if (_starpowerActive) {
					pulseAmount = 0.25f;
				} else if (!_starpowerActive && _starpowerCharge >= 0.5f) {
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
			if (_starpowerActive) {
				starpowerOverlay.material.SetFloat("AlphaMultiplier",
					Mathf.Lerp(currentStarpower, 1f, Time.deltaTime * 2f));
			} else {
				starpowerOverlay.material.SetFloat("AlphaMultiplier",
					Mathf.Lerp(currentStarpower, 0f, Time.deltaTime * 4f));
			}

			// Unset
			_beat = false;
		}

		private void UpdateEndPhrase() {
			// Skip if there is no singing
			if (_sectionSingTime.Max() <= 0f) {
				// Calculate the new sing time
				CalculateSectionSingTime(Play.Instance.SongTime);

				return;
			}

			float bestPercent = 0f;

			// Reset and see if we failed or not
			foreach (var playerInfo in _micInputs) {
				float mul = GetSingTimeMultiplier(playerInfo.Player.chosenDifficulty);
				float bestPlayerPercent = 0f;
				for (int i = 0; i < playerInfo.SingProgresses.Length; i++) {
					if (_sectionSingTime[i] == 0f) {
						continue;
					}

					// Get percent (0-1)
					float percent = playerInfo.SingProgresses[i] / (_sectionSingTime[i] * mul);

					// Set best (player) percent
					playerInfo.SingProgresses[i] = 0f;
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
				var phraseScore = math.clamp((double) sectionPercents[i] * _ptsPerPhrase, 0, _ptsPerPhrase);
				if (i != sectionPercents.Count - 1) {
					phraseScore *= 0.1;
				}
				_scoreKeeper.Add(Multiplier * phraseScore);
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
			PerfTextSizer.AnimTimeRemaining = animTimeLength;

			// Add to sing percent
			_totalSingPercent += Mathf.Min(bestPercent, 1f);

			// Add to multiplier
			if (bestPercent >= 1f) {
				if (_rawMultiplier < 4) {
					_rawMultiplier++;
				}

				// Starpower
				if (_starpowerSection != null && _starpowerSection.EndTime <= Play.Instance.SongTime) {
					_starpowerCharge += 0.25f;
					_starpowerSection = null;
				}

				_sectionsHit++;
			} else {
				_rawMultiplier = 1;

				_sectionsFailed++;
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
				lyricPool.AddLyric(lyricInfo, _visualStarpowerSection != null, pos, false);
			} else if (harmIndex == 1) {
				lyricPool.AddLyric(lyricInfo, _visualStarpowerSection != null, pos, true);
				_lastSecondHarmonyLyric = lyricInfo.lyric;
			} else if (harmIndex == 2 && _lastSecondHarmonyLyric != lyricInfo.lyric) {
				// Only add if it's not the same as the last second harmony
				lyricPool.AddLyric(lyricInfo, _visualStarpowerSection != null, pos, true);
			}

			// Spawn note
			if (lyricInfo.inharmonic) {
				notePool.AddNoteInharmonic(lyricInfo.length, pos, _charts.Count != 1, harmIndex);
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
			for (int i = 0; i < _charts.Count; i++) {
				_sectionSingTime[i] = 0f;
				foreach (var lyric in _charts[i]) {
					if (lyric.time < start) {
						continue;
					}

					if (lyric.time > end) {
						break;
					}

					_sectionSingTime[i] += lyric.length;
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

			if (Instance._micInputs[0].Player.chosenInstrument == "harmVocals") {
				z = Mathf.Clamp(z, -0.61f, 0.61f);
			} else {
				z = Mathf.Clamp(z, -0.61f, 0.93f);
			}

			return z;
		}

		private void BeatAction() {
			_beat = true;
		}

		private void PauseAction() {
			Play.Instance.Paused = !Play.Instance.Paused;
		}

		private void StarpowerAction(InputStrategy inputStrategy) {
			if (_starpowerCharge < 0.5f) {
				return;
			}

			// Get playerInfo
			var playerInfo = _micInputs.Find(x => x.Player.inputStrategy == inputStrategy);
			if (playerInfo == null) {
				return;
			}

			// See if we are in a starpower activate section
			bool inStarpowerSection = false;
			for (int i = 0; i < _charts.Count; i++) {
				var chart = _charts[i];

				// Only top and bottom
				if (i >= 2) {
					break;
				}

				if (_chartIndex[i] - 1 > 0 && _chartIndex[i] < chart.Count) {
					var firstLyric = chart[_chartIndex[i] - 1];
					var nextLyric = chart[_chartIndex[i]];

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
			if (inStarpowerSection && !playerInfo.HittingNote) {
				_starpowerActive = true;
			}
		}
	}
}