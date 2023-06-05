using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using YARG.Audio;
using YARG.Chart;
using YARG.Data;
using YARG.Input;
using YARG.Pools;
using YARG.Settings;
using YARG.UI;

namespace YARG.PlayMode {
	public abstract class AbstractTrack : MonoBehaviour {
		public const float TRACK_SPAWN_OFFSET = 3f;
		public const float TRACK_END_OFFSET = 1.95f;

		public delegate void StarpowerMissAction(EventInfo missedPhrase);
		public event StarpowerMissAction StarpowerMissEvent;

		[SerializeField]
		protected Pool genericPool;

		public PlayerManager.Player player;

		// Time values

		// Defined separately for convenience and extensibility
		public float HitMarginFront => Constants.HIT_MARGIN_FRONT * Play.speed;
		public float HitMarginBack => Constants.HIT_MARGIN_BACK * Play.speed;
		public float HitMargin => HitMarginFront + HitMarginBack;

		// Convenience name for current song time
		public float CurrentTime => Play.Instance.SongTime;
		// Time relative to the start of the hit window
		public float HitMarginStartTime => Play.Instance.SongTime + HitMarginFront;
		// Time relative to the end of the hit window
		public float HitMarginEndTime => Play.Instance.SongTime - HitMarginBack;
		// Time relative to the beginning of the track, used for spawning notes and other visuals
		public float TrackStartTime => Play.Instance.SongTime +
			((TRACK_SPAWN_OFFSET + TRACK_END_OFFSET) / (player.trackSpeed / Play.speed));

		protected List<NoteInfo> Chart => Play.Instance.chart
			.GetChartByName(player.chosenInstrument)[(int) player.chosenDifficulty];

		// Track notes
		protected int visualChartIndex = 0;
		protected int inputChartIndex = 0;
		protected int hitChartIndex = 0;
		protected int currentBeatIndex = 0;

		protected CommonTrack commonTrack;
		protected TrackAnimations trackAnims;

		// Track events
		protected List<EventInfo> starpowerSections = new();
		protected int starpowerIndex = 0;
		protected int starpowerVisualIndex = 0;
		public EventInfo CurrentStarpower =>
			starpowerIndex < starpowerSections.Count ? starpowerSections[starpowerIndex] : null;
		public EventInfo CurrentVisualStarpower =>
			starpowerVisualIndex < starpowerSections.Count ? starpowerSections[starpowerVisualIndex] : null;

		protected List<EventInfo> soloSections = new();
		protected int soloIndex = 0;
		protected int soloVisualIndex = 0;
		public EventInfo CurrentSolo =>
			soloIndex < soloSections.Count ? soloSections[soloIndex] : null;
		public EventInfo CurrentVisualSolo =>
			soloVisualIndex < soloSections.Count ? soloSections[soloVisualIndex] : null;

		protected int notesHit = 0;
		// private int notesMissed = 0;

		public bool IsStarPowerActive { get; protected set; }
		protected float starpowerCharge;
		// For OVERDRIVE READY notif
		protected float recentStarpowerCharge;
		//protected bool starpowerHit = false;
		protected Light comboSunburstEmbeddedLight;

		// Solo stuff
		private bool soloInProgress = false;
		protected int soloNoteCount = -1;
		protected int soloNotesHit = 0;
		private int soloHitPercent = 0;
		private int lastHit = -1;
		private double soloPtsEarned;

		private bool FullCombo = true;

		// Used for performance text purposes
		private bool hotStartChecked = false;
		private bool fullComboChecked = false;
		private bool strongFinishChecked = false;
		private float endTime;
		private float offsetEndTime;  // For moving STRONG FINISH back in case of an FC

		private int SavedCombo = 0;
		private bool switchedRingMaterial = false;
		protected bool startFCDetection = false;
		protected bool missedAnyNote = false;

		private int _combo = 0;
		private int _recentCombo = 0;  // For tracking note streak of size interval / 2
		private int _maxCombo = 0;

		protected int Combo {
			get => _combo;
			set {
				if (_combo >= 10 && value == 0) {
					GameManager.AudioManager.PlaySoundEffect(SfxSample.NoteMiss);
				}

				_combo = value;

				if (value > _maxCombo) {
					_maxCombo = value;
				}

				// End starpower if combo ends
				if (CurrentStarpower?.time <= CurrentTime && value == 0) {
					StarpowerMissEvent?.Invoke(CurrentStarpower);
					// Only move to the next visual phrase if it is also the current logical phrase
					if (starpowerVisualIndex == starpowerIndex) {
						starpowerVisualIndex++;
					}
					starpowerIndex++;
				}
			}
		}

		public int MaxCombo => _maxCombo;

		public int MaxMultiplier => (player.chosenInstrument == "bass" ? 6 : 4) * (IsStarPowerActive ? 2 : 1);
		public int Multiplier => Mathf.Min((Combo / 10 + 1) * (IsStarPowerActive ? 2 : 1), MaxMultiplier);
		public bool recentlyBelowMaxMultiplier = true;

		// For XOO-NOTE STREAK
		private int intervalSize;
		private int halfIntervalSize;
		private int currentNoteStreakInterval = 0;
		private int recentNoteStreakInterval = 0;

		// Scoring trackers
		protected ScoreKeeper scoreKeeper;
		protected StarScoreKeeper starsKeeper;
		protected SustainTracker susTracker;

		private bool _stopAudio = false;
		protected bool StopAudio {
			set {
				if (value == _stopAudio) {
					return;
				}

				_stopAudio = value;

				if (!value) {
					Play.Instance.RaiseAudio(player.chosenInstrument);
				} else {
					Play.Instance.LowerAudio(player.chosenInstrument);
				}
			}
		}

		protected bool Beat {
			get;
			private set;
		}

		private void Awake() {
			commonTrack = GetComponent<CommonTrack>();
			trackAnims = GetComponent<TrackAnimations>();

			// Set up render texture
			var descriptor = new RenderTextureDescriptor(
				Screen.width, Screen.height,
				RenderTextureFormat.ARGBHalf
			);
			descriptor.mipCount = 0;
			var renderTexture = new RenderTexture(descriptor);

			// Assign render texture to camera
			commonTrack.TrackCamera.targetTexture = renderTexture;

			// AMONG US
			susTracker = new(Play.Instance.chart.beats);
		}

		private void Start() {
			player.track = this;
			FullCombo = true;

			player.inputStrategy.StarpowerEvent += StarpowerAction;
			player.inputStrategy.PauseEvent += PauseAction;
			Play.BeatEvent += BeatAction;
			Play.OnPauseToggle += PauseToggled;

			player.lastScore = null;

			GameUI.Instance.AddTrackImage(commonTrack.TrackCamera.targetTexture, commonTrack);

			// Adjust hit window
			const float baseSize = Constants.HIT_MARGIN_FRONT + Constants.HIT_MARGIN_BACK;
			const float baseOffset = (Constants.HIT_MARGIN_FRONT - Constants.HIT_MARGIN_BACK) / 2; // Offsetting is done based on half of the size

			var window = commonTrack.hitWindow;
			window.localScale = window.localScale.WithY(baseSize * player.trackSpeed);
			window.localPosition = window.localPosition.AddZ(baseOffset * player.trackSpeed);

			// Display hit window
			window.gameObject.SetActive(SettingsManager.Settings.ShowHitWindow.Data);

			comboSunburstEmbeddedLight = commonTrack.comboSunburst.GetComponent<Light>();
			commonTrack.kickFlash.SetColor(commonTrack.KickFlashColor);

			scoreKeeper = new();

			// Set the end time for STRONG FINISH and FULL COMBO performance text checking
			endTime = Chart[^1].time + HitMarginBack + commonTrack.bufferPeriod;
			offsetEndTime = endTime + 3f;

			// Initlaize interval sizes
			intervalSize = commonTrack.noteStreakInterval;
			halfIntervalSize = intervalSize / 2;

			// Queue up events
			string spName = $"starpower_{player.chosenInstrument}";
			string soloName = $"solo_{player.chosenInstrument}";
			string fillName = $"fill_{player.chosenInstrument}";
			foreach (var eventInfo in Play.Instance.chart.events) {
				if (eventInfo.name == spName) {
					starpowerSections.Add(eventInfo);
				} else if (eventInfo.name == soloName) {
					soloSections.Add(eventInfo);
				}
			}

			StartTrack();
		}

		protected abstract void StartTrack();

		public virtual void SetPlayerScore() {
			// Set score
			player.lastScore = new PlayerManager.LastScore {
				percentage = new DiffPercent {
					difficulty = player.chosenDifficulty,
					percent = Chart.Count == 0 ? 1f : (float) notesHit / GetChartCount()
				},
				score = new DiffScore {
					difficulty = player.chosenDifficulty,
					score = (int) math.round(scoreKeeper.Score),
					stars = math.clamp((int) starsKeeper.Stars, 0, 6)
				},
				notesHit = notesHit,
				notesMissed = GetChartCount() - notesHit,
				maxCombo = MaxCombo
			};
		}

		protected virtual void OnDestroy() {
			// Release render texture
			commonTrack.TrackCamera.targetTexture.Release();

			player.inputStrategy.StarpowerEvent -= StarpowerAction;
			player.inputStrategy.PauseEvent -= PauseAction;
			Play.BeatEvent -= BeatAction;

			Play.OnPauseToggle -= PauseToggled;

			SetPlayerScore();
		}

		private void Update() {
			// Don't update if paused
			if (Play.Instance.Paused) {
				return;
			}

			// Progress visual SP phrases
			// This is placed here instead of UpdateStarpower() since it causes visual issues otherwise
			// TODO: Tracks need to be refactored to address that
			while (CurrentVisualStarpower?.EndTime < TrackStartTime) {
				starpowerVisualIndex++;
			}

			UpdateMaterial();

			UpdateBeats();
			UpdateTrack();

			if (hitChartIndex > lastHit) {
				lastHit = hitChartIndex;
			}

			// NOTE: UpdateInfo() originally was before UpdateStarpower().
			// Moved it to the end to make performance text things work.
			// I REALLY hope that doesn't break something. - grishhung
			UpdateStarpower();
			UpdateFullComboState();
			UpdateInfo();

			if (Multiplier >= MaxMultiplier) {
				commonTrack.comboSunburst.gameObject.SetActive(true);
				commonTrack.comboSunburst.transform.Rotate(0f, 0f, Time.deltaTime * -15f);

				commonTrack.maxComboLight.gameObject.SetActive(!IsStarPowerActive);
				commonTrack.starpowerLight.gameObject.SetActive(IsStarPowerActive);
			} else {
				commonTrack.comboSunburst.gameObject.SetActive(false);

				commonTrack.maxComboLight.gameObject.SetActive(false);
				commonTrack.starpowerLight.gameObject.SetActive(false);
			}

			Beat = false;
		}

		private void UpdateFullComboState() {
			if (Combo > SavedCombo) {
				SavedCombo = Combo;

			}

			if (Combo < SavedCombo && startFCDetection) {
				FullCombo = false;

			}

			if ((!FullCombo && !switchedRingMaterial) || missedAnyNote) {
				commonTrack.comboRing.material = commonTrack.nonFCRing;
				switchedRingMaterial = true;

			}
		}

		private void UpdateBeats()
		{
			var beats = Play.Instance.chart.beats;
			while (beats.Count > currentBeatIndex && beats[currentBeatIndex].Time <= TrackStartTime) {
				var beatInfo = beats[currentBeatIndex];

				float compensation = TRACK_SPAWN_OFFSET - CalcLagCompensation(TrackStartTime, beatInfo.Time);
				if (beatInfo.Style is BeatStyle.STRONG or BeatStyle.WEAK) {
					genericPool.Add("beatLine_minor", new(0f, 0.01f, compensation));
				} else if (beatInfo.Style == BeatStyle.MEASURE) {
					genericPool.Add("beatLine_major", new(0f, 0.01f, compensation));
				}
				currentBeatIndex++;
			}
		}

		protected abstract void UpdateTrack();

		private void UpdateMaterial() {
			// Update track UV
			var trackMaterial = commonTrack.trackRenderer.material;
			var oldOffset = trackMaterial.GetVector("TexOffset");
			float movement = Time.deltaTime * player.trackSpeed / 4f;
			trackMaterial.SetVector("TexOffset", new(oldOffset.x, oldOffset.y - movement));

			// Update track groove
			float currentGroove = trackMaterial.GetFloat("GrooveState");
			if (Multiplier >= MaxMultiplier) {
				trackMaterial.SetFloat("GrooveState", Mathf.Lerp(currentGroove, 1f, Time.deltaTime * 5f));
			} else {
				trackMaterial.SetFloat("GrooveState", Mathf.Lerp(currentGroove, 0f, Time.deltaTime * 3f));
			}

			// Update track starpower
			float currentStarpower = trackMaterial.GetFloat("StarpowerState");
			if (IsStarPowerActive) {
				trackMaterial.SetFloat("StarpowerState", Mathf.Lerp(currentStarpower, 1f, Time.deltaTime * 2f));
			} else {
				trackMaterial.SetFloat("StarpowerState", Mathf.Lerp(currentStarpower, 0f, Time.deltaTime * 4f));
			}

			float currentSolo = trackMaterial.GetFloat("SoloState");
			if (CurrentTime >= CurrentSolo?.time - 2 && CurrentTime <= CurrentSolo?.EndTime - 1) {
				trackMaterial.SetFloat("SoloState", Mathf.Lerp(currentSolo, 1f, Time.deltaTime * 2f));
			} else {
				trackMaterial.SetFloat("SoloState", Mathf.Lerp(currentSolo, 0f, Time.deltaTime * 2f));
			}

			// Update starpower bar
			var starpowerMat = commonTrack.starpowerBarTop.material;
			starpowerMat.SetFloat("Fill", starpowerCharge);
			if (Beat) {
				float pulseAmount = 0f;
				if (IsStarPowerActive) {
					pulseAmount = 0.25f;
				} else if (!IsStarPowerActive && starpowerCharge >= 0.5f) {
					pulseAmount = 1f;
				}

				starpowerMat.SetFloat("Pulse", pulseAmount);
			} else {
				float currentPulse = starpowerMat.GetFloat("Pulse");
				starpowerMat.SetFloat("Pulse", Mathf.Lerp(currentPulse, 0f, Time.deltaTime * 16f));
			}
		}

		protected virtual void UpdateStarpower() {
			// Update starpower region
			if (IsStarpowerHit()) {
				starpowerIndex++;
				starpowerCharge += 0.25f;
				if (starpowerCharge > 1f) {
					starpowerCharge = 1f;
				}

				trackAnims.StarpowerLightsAnimSingleFrame();
				//starpowerHit = true;
				GameManager.AudioManager.PlaySoundEffect(SfxSample.StarPowerAward);
			}

			// Update starpower active
			if (IsStarPowerActive) {
				if (starpowerCharge <= 0f) {
					IsStarPowerActive = false;
					starpowerCharge = 0f;
					GameManager.AudioManager.PlaySoundEffect(SfxSample.StarPowerRelease);
					SetReverb(false);
				} else {
					// calculates based on 32 beats for a full bar
					starpowerCharge -= Time.deltaTime * Play.Instance.CurrentBeatsPerSecond * (1f / 32f);
				}
				if (!trackAnims.spShakeAscended) {
					trackAnims.StarpowerTrackAnim();

				}
				trackAnims.StarpowerParticleAnim();
				trackAnims.StarpowerLightsAnim();

				// Update Sunburst color and light
				commonTrack.comboSunburst.sprite = commonTrack.sunBurstSpriteStarpower;
				commonTrack.comboSunburst.color = commonTrack.comboSunburstSPColor;

				if (Multiplier >= MaxMultiplier) {
					commonTrack.comboBase.material = commonTrack.baseSP;
				} else {
					commonTrack.comboBase.material = commonTrack.baseNormal;
				}
			} else {

				trackAnims.StarpowerTrackAnimReset();
				trackAnims.StarpowerParticleAnimReset();
				trackAnims.StarpowerLightsAnimReset();

				//Reset Sunburst color and light to original
				commonTrack.comboSunburst.sprite = commonTrack.sunBurstSprite;
				commonTrack.comboSunburst.color = commonTrack.comboSunburstColor;

				if (Multiplier >= MaxMultiplier) {
					commonTrack.comboBase.material = commonTrack.baseGroove;
				} else {
					commonTrack.comboBase.material = commonTrack.baseNormal;
				}
			}

			// Clear out passed SP phrases
			while (CurrentStarpower?.EndTime < HitMarginEndTime) {
				starpowerIndex++;
			}
		}

		private void PauseAction() {
			Play.Instance.Paused = !Play.Instance.Paused;
		}

		protected abstract void PauseToggled(bool pause);

		private void UpdateInfo() {
			// Update text
			if (Multiplier == 1) {
				commonTrack.comboText.text = null;
			} else {
				commonTrack.comboText.text = $"{Multiplier}<sub>x</sub>";
			}

			// Set solo box and text
			if (CurrentSolo?.time <= HitMarginStartTime && CurrentSolo?.EndTime >= HitMarginEndTime) {
				if (!soloInProgress) {
					soloInProgress = true;
					soloNotesHit = 0;
					soloNoteCount = 0;

					float lastNoteTime = -1f;
					for (int i = hitChartIndex; i < Chart.Count; i++) {
						if (Chart[i].time == lastNoteTime) {
							continue;
						}

						if (Chart[i].time > CurrentSolo?.EndTime) {
							break;
						}

						soloNoteCount++;
						lastNoteTime = Chart[i].time;
					}
				}

				// Set text color
				// commonTrack.soloText.colorGradient = new VertexGradient(
				// 	new Color(1f, 1f, 1f),
				// 	new Color(1f, 1f, 1f),
				// 	new Color(0.1320755f, 0.1320755f, 0.1320755f),
				// 	new Color(0.1320755f, 0.1320755f, 0.1320755f)
				// );
				// commonTrack.soloText.gameObject.SetActive(true);

				soloHitPercent = Mathf.FloorToInt((float) soloNotesHit / soloNoteCount * 100f);
				commonTrack.TrackView.SetSoloBox(soloHitPercent + "%", $"{soloNotesHit}/{soloNoteCount}");
			} else if (soloInProgress) {
				commonTrack.TrackView.HideSoloBox(soloHitPercent + "%", soloHitPercent switch {
					69 => "<i>NICE</i>\nSOLO",

					>= 100 => "PERFECT\nSOLO!",
					>= 95  => "AWESOME\nSOLO!",
					>= 90  => "GREAT\nSOLO!",
					>= 80  => "GOOD\nSOLO!",
					>= 70  => "SOLID\nSOLO",
					>= 60  => "OKAY\nSOLO",
					_      => "MESSY\nSOLO",
				});

				soloInProgress = false;
			}
			// } else if (HitMarginEndTime >= SoloSection?.EndTime && HitWindowEndTime <= SoloSection?.EndTime + 3) {
			// 	// run ONCE
			// 	if (soloInProgress) {
			// 		soloPtsEarned = scoreKeeper.AddSolo(soloNotesHit, soloNoteCount);
			// 		soloNotesHit = 0; // Reset count

			// 		// set box text
			// 		if (soloHitPercent >= 100f) {
			// 			// Set text color
			// 			commonTrack.soloText.colorGradient = new VertexGradient(
			// 				new Color(1f, 0.619472f, 0f),
			// 				new Color(1f, 0.619472f, 0f),
			// 				new Color(0.5377358f, 0.2550798f, 0f),
			// 				new Color(0.5377358f, 0.2550798f, 0f)
			// 			);
			// 			commonTrack.soloBox.sprite = commonTrack.soloPerfectSprite;
			// 			commonTrack.soloText.text = "PERFECT\nSOLO!";
			// 		} else if (soloHitPercent >= 95f) {
			// 			commonTrack.soloText.text = "AWESOME\nSOLO!";
			// 		} else if (soloHitPercent >= 90f) {
			// 			commonTrack.soloText.text = "GREAT\nSOLO!";
			// 		} else if (soloHitPercent >= 80f) {
			// 			commonTrack.soloText.text = "GOOD\nSOLO!";
			// 		} else if (soloHitPercent >= 70f) {
			// 			commonTrack.soloText.text = "SOLID\nSOLO!";
			// 		} else if (soloHitPercent >= 60f) {
			// 			commonTrack.soloText.text = "OKAY\nSOLO!";
			// 		} else {
			// 			// Set text color
			// 			commonTrack.soloText.colorGradient = new VertexGradient(
			// 				new Color(1f, 0.1933962f, 0.1933962f),
			// 				new Color(1f, 0.1933962f, 0.1933962f),
			// 				new Color(1f, 0.1332366f, 0.06132078f),
			// 				new Color(1f, 0.1332366f, 0.06132078f)
			// 			);

			// 			commonTrack.soloBox.sprite = commonTrack.soloMessySprite;
			// 			commonTrack.soloText.text = "MESSY\nSOLO!";
			// 		}

			// 		// show points earned after some time
			// 		StartCoroutine(SoloBoxShowScore());

			// 		soloInProgress = false;
			// 	}
			// } else {
			// 	commonTrack.soloText.text = null;
			// 	commonTrack.soloText.gameObject.SetActive(false);
			// 	commonTrack.soloBox.gameObject.SetActive(false);
			// }

			// Update status
			int index = Combo % 10;
			if (Multiplier != 1 && index == 0) {
				index = 10;
			} else if (Multiplier == MaxMultiplier) {
				index = 10;
			}

			commonTrack.comboMeterRenderer.material.SetFloat("SpriteNum", index);

			// HOT START notifs
			if (commonTrack.hotStartNotifsEnabled) {
				if (!hotStartChecked) {
					if (_combo >= commonTrack.hotStartCutoff) {
						hotStartChecked = true;

						if (FullCombo) {
							commonTrack.TrackView.ShowPerformanceText("HOT START");
						}
					}
				}
			}

			// NOTE STREAK notifs
			if (commonTrack.noteStreakNotifsEnabled) {
				if (_recentCombo < halfIntervalSize && _combo >= halfIntervalSize) {
					commonTrack.TrackView.ShowPerformanceText($"{halfIntervalSize}-NOTE STREAK");
				}

				currentNoteStreakInterval = _combo / intervalSize;

				if (recentNoteStreakInterval < currentNoteStreakInterval) {
					commonTrack.TrackView.ShowPerformanceText($"{currentNoteStreakInterval * intervalSize}-NOTE STREAK");
				}
			}

			// BASS GROOVE notifs
			// NOTE: This will always trump "X-NOTE STREAK" (in particular, "50-NOTE STREAK")
			if (commonTrack.bassGrooveNotifsEnabled) {
				// Top 10 programming moments
				// Should affect both "bass" and "proBass"
				if (player.chosenInstrument.Contains("ass")) {
					// int triggerThreshold = IsStarPowerActive ? MaxMultiplier / 2 : MaxMultiplier;

					if (recentlyBelowMaxMultiplier && Multiplier >= MaxMultiplier) {
						commonTrack.TrackView.ShowPerformanceText("BASS GROOVE");
					}
				}
			}

			// OVERDRIVE READY notifs
			if (commonTrack.overdriveReadyNotifsEnabled) {
				if (recentStarpowerCharge < 0.5f && starpowerCharge >= 0.5f && !IsStarPowerActive) {
					commonTrack.TrackView.ShowPerformanceText("OVERDRIVE READY");
				}
			}

			// Deteremine behavior based on whether or not FC trumps SF
			if (commonTrack.fullComboTrumpsStrongFinish) {
				if (!strongFinishChecked) {
					if (CurrentTime > endTime) {
						strongFinishChecked = true;

						if (FullCombo) {
							commonTrack.TrackView.ShowPerformanceText("FULL COMBO");
						} else if (_combo >= commonTrack.strongFinishCutoff && commonTrack.strongFinishNotifsEnabled) {
							commonTrack.TrackView.ShowPerformanceText("STRONG FINISH");
						}
					}
				}
			} else {
				if (!fullComboChecked) {
					if (CurrentTime > endTime) {
						fullComboChecked = true;

						if (FullCombo) {
							commonTrack.TrackView.ShowPerformanceText("FULL COMBO");
						}
					}
				}

				if (commonTrack.strongFinishNotifsEnabled) {
					if (!strongFinishChecked) {
						float checkTime = FullCombo ? offsetEndTime : endTime;

						if (CurrentTime > checkTime) {
							strongFinishChecked = true;

							if (_combo >= commonTrack.strongFinishCutoff) {
								commonTrack.TrackView.ShowPerformanceText("STRONG FINISH");
							}
						}
					}
				}
			}

			// Update recent values
			_recentCombo = _combo;
			recentNoteStreakInterval = currentNoteStreakInterval;
			recentlyBelowMaxMultiplier = Multiplier < MaxMultiplier;
			recentStarpowerCharge = starpowerCharge;

			// Clear out passed solo sections
			while (CurrentSolo?.EndTime < HitMarginEndTime) {
				soloIndex++;
			}
			while (CurrentVisualSolo?.EndTime < TrackStartTime) {
				soloVisualIndex++;
			}
		}

		IEnumerator SoloBoxShowScore() {
			yield return new WaitForSeconds(1.5f);
			// commonTrack.soloText.text = $"{soloPtsEarned:n0}\nPOINTS";
		}

		private void BeatAction() {
			Beat = true;
		}

		private void StarpowerAction(InputStrategy inputStrategy) {
			if (!IsStarPowerActive && starpowerCharge >= 0.5f) {
				GameManager.AudioManager.PlaySoundEffect(SfxSample.StarPowerDeploy);
				SetReverb(true);
				IsStarPowerActive = true;
			}
		}

		protected float CalcLagCompensation(float currentTime, float noteTime) {
			return (currentTime - noteTime) * (player.trackSpeed / Play.speed);
		}

		protected bool IsStarpowerHit() {
			if (Chart.Count > hitChartIndex) {
				return Chart[hitChartIndex].time >= CurrentStarpower?.EndTime;
			}

			return false;
		}

		public abstract void SetReverb(bool on);

		public virtual int GetChartCount() {
			return Chart.Count;
		}
	}
}