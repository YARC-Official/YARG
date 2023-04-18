using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using YARG.Data;
using YARG.Input;
using YARG.Settings;
using YARG.UI;

namespace YARG.PlayMode {
	public abstract class AbstractTrack : MonoBehaviour {
		public const float TRACK_SPAWN_OFFSET = 3f;
		public const float TRACK_END_OFFSET = 1.8f;

		public delegate void StarpowerMissAction();
		public event StarpowerMissAction StarpowerMissEvent;

		public PlayerManager.Player player;
		public float RelativeTime => Play.Instance.SongTime +
			((TRACK_SPAWN_OFFSET + TRACK_END_OFFSET) / (player.trackSpeed / Play.speed));

		protected List<NoteInfo> Chart => Play.Instance.chart
			.GetChartByName(player.chosenInstrument)[(int) player.chosenDifficulty];

		protected int visualChartIndex = 0;
		protected int inputChartIndex = 0;
		protected int hitChartIndex = 0;
		protected int eventChartIndex = 0;

		protected CommonTrack commonTrack;

		public EventInfo StarpowerSection {
			get;
			protected set;
		} = null;
		public EventInfo SoloSection {
			get;
			protected set;
		} = null;

		protected float starpowerCharge;
		protected bool starpowerActive;
		protected Light comboSunburstEmbeddedLight;

		// Overdrive animation parameters
		protected Vector3 trackStartPos;
		protected Vector3 trackEndPos = new(0, 0.08f, 0.13f);
		protected float spAnimationDuration = 0.2f;
		protected float elapsedTimeAnim = 0;
		protected bool gotStartPos = false;
		protected bool depressed = false;
		protected bool ascended = false;
		protected bool resetTime = false;

		// Solo stuff
		private int soloNoteCount = -1;
		protected int soloNotesHit = 0;
		private float soloHitPercent = 0;
		private int lastHit = -1;

		private bool FullCombo = true;
		private int SavedCombo = 0;
		private bool switchedRingMaterial = false;
		protected bool startFCDetection = false;
		protected bool missedAnyNote = false;

		private int _combo = 0;
		protected int Combo {
			get => _combo;
			set {
				if (_combo >= 10 && value == 0) {
					GameManager.AudioManager.PlaySoundEffect(SfxSample.NoteMiss);
				}

				_combo = value;

				// End starpower if combo ends
				if (StarpowerSection?.time <= Play.Instance.SongTime && value == 0) {
					StarpowerSection = null;
					StarpowerMissEvent?.Invoke();
				}
			}
		}

		protected int MaxMultiplier => (player.chosenInstrument == "bass" ? 6 : 4) * (starpowerActive ? 2 : 1);
		protected int Multiplier => Mathf.Min((Combo / 10 + 1) * (starpowerActive ? 2 : 1), MaxMultiplier);

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

			// Set up render texture
			var descriptor = new RenderTextureDescriptor(
				Screen.width, Screen.height,
				RenderTextureFormat.ARGBHalf
			);
			descriptor.mipCount = 0;
			var renderTexture = new RenderTexture(descriptor);
			commonTrack.trackCamera.targetTexture = renderTexture;

			// Set up camera
			var info = commonTrack.trackCamera.GetComponent<UniversalAdditionalCameraData>();
			if (SettingsManager.GetSettingValue<bool>("lowQuality")) {
				info.antialiasing = AntialiasingMode.None;
			} else {
				info.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
				info.antialiasingQuality = AntialiasingQuality.Low;
			}

			susTracker = new(Play.Instance.chart.beats);
		}

		private void Start() {
			player.track = this;
			FullCombo = true;

			player.inputStrategy.StarpowerEvent += StarpowerAction;
			player.inputStrategy.PauseEvent += PauseAction;
			Play.BeatEvent += BeatAction;

			player.lastScore = null;

			GameUI.Instance.AddTrackImage(commonTrack.trackCamera.targetTexture);

			// Adjust hit window
			var scale = commonTrack.hitWindow.localScale;
			commonTrack.hitWindow.localScale = new(scale.x, Constants.HIT_MARGIN * player.trackSpeed * 2f, scale.z);
			commonTrack.hitWindow.gameObject.SetActive(SettingsManager.GetSettingValue<bool>("showHitWindow"));

			comboSunburstEmbeddedLight = commonTrack.comboSunburst.GetComponent<Light>();

			scoreKeeper = new();

			StartTrack();
		}

		protected abstract void StartTrack();

		protected virtual void OnDestroy() {
			// Release render texture
			commonTrack.trackCamera.targetTexture.Release();

			player.inputStrategy.StarpowerEvent -= StarpowerAction;
			player.inputStrategy.PauseEvent -= PauseAction;
			Play.BeatEvent -= BeatAction;
		}

		private void Update() {
			// Don't update if paused
			if (Play.Instance.Paused) {
				// Update navigation for pause menu
				player.inputStrategy.UpdateNavigationMode();

				return;
			}

			UpdateMaterial();

			UpdateTrack();

			if (hitChartIndex > lastHit) {
				lastHit = hitChartIndex;
			}

			UpdateInfo();
			UpdateStarpower();
			UpdateFullComboState();

			if (Multiplier >= MaxMultiplier) {
				commonTrack.comboSunburst.gameObject.SetActive(true);
				commonTrack.comboSunburst.transform.Rotate(0f, 0f, Time.deltaTime * -15f);

				commonTrack.maxComboLight.gameObject.SetActive(!starpowerActive);
				commonTrack.starpowerLight.gameObject.SetActive(starpowerActive);
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
			if (starpowerActive) {
				trackMaterial.SetFloat("StarpowerState", Mathf.Lerp(currentStarpower, 1f, Time.deltaTime * 2f));
			} else {
				trackMaterial.SetFloat("StarpowerState", Mathf.Lerp(currentStarpower, 0f, Time.deltaTime * 4f));
			}

			float currentSolo = trackMaterial.GetFloat("SoloState");
			if (Play.Instance.SongTime >= SoloSection?.time - 2 && Play.Instance.SongTime <= SoloSection?.EndTime - 1) {
				trackMaterial.SetFloat("SoloState", Mathf.Lerp(currentSolo, 1f, Time.deltaTime * 2f));
			} else {
				trackMaterial.SetFloat("SoloState", Mathf.Lerp(currentSolo, 0f, Time.deltaTime * 2f));
			}

			// Update starpower bar
			var starpowerMat = commonTrack.starpowerBarTop.material;
			starpowerMat.SetFloat("Fill", starpowerCharge);
			if (Beat) {
				float pulseAmount = 0f;
				if (starpowerActive) {
					pulseAmount = 0.25f;
				} else if (!starpowerActive && starpowerCharge >= 0.5f) {
					pulseAmount = 1f;
				}

				starpowerMat.SetFloat("Pulse", pulseAmount);
			} else {
				float currentPulse = starpowerMat.GetFloat("Pulse");
				starpowerMat.SetFloat("Pulse", Mathf.Lerp(currentPulse, 0f, Time.deltaTime * 16f));
			}
		}

		private void StarpowerTrackAnim() {
			// Start track animation
			elapsedTimeAnim += Time.deltaTime;
			float percentageComplete = elapsedTimeAnim / spAnimationDuration;
			if (!depressed && !ascended) {
				spAnimationDuration = 0.065f;
				commonTrack.trackCamera.transform.position = Vector3.Lerp(trackStartPos, trackStartPos + trackEndPos, percentageComplete);

				if (commonTrack.trackCamera.transform.position == trackStartPos + trackEndPos) {
					resetTime = true;
					depressed = true;
				}
			}

			if (resetTime) {
				elapsedTimeAnim = 0f;
				resetTime = false;
			}

			// End track animation
			if (depressed && !ascended) {
				spAnimationDuration = 0.2f;
				commonTrack.trackCamera.transform.position = Vector3.Lerp(trackStartPos + trackEndPos, trackStartPos, percentageComplete);

				if (commonTrack.trackCamera.transform.position == trackStartPos + trackEndPos) {
					resetTime = true;
					ascended = true;
				}
			}
		}

		private void StarpowerTrackAnimReset() {
			if (!gotStartPos) {
				trackStartPos = commonTrack.trackCamera.transform.position;
				gotStartPos = true;
			}

			depressed = false;
			ascended = false;
			elapsedTimeAnim = 0f;
		}

		private void UpdateStarpower() {
			// Update starpower region
			if (IsStarpowerHit()) {
				StarpowerSection = null;
				starpowerCharge += 0.25f;
				if (starpowerCharge > 1f) {
					starpowerCharge = 1f;
				}

				GameManager.AudioManager.PlaySoundEffect(SfxSample.StarPowerAward);
			}

			// Update starpower active
			if (starpowerActive) {
				if (starpowerCharge <= 0f) {
					starpowerActive = false;
					starpowerCharge = 0f;
					GameManager.AudioManager.PlaySoundEffect(SfxSample.StarPowerRelease);
					SetReverb(false);
				} else {
					starpowerCharge -= Time.deltaTime / 25f * Play.speed;
				}

				StarpowerTrackAnim();

				// Update Sunburst color and light
				commonTrack.comboSunburst.sprite = commonTrack.sunBurstSpriteStarpower;
				commonTrack.comboSunburst.color = new Color(255, 255, 255, 141);
			} else {
				StarpowerTrackAnimReset();

				//Reset Sunburst color and light to original
				commonTrack.comboSunburst.sprite = commonTrack.sunBurstSprite;
				commonTrack.comboSunburst.color = Color.white;
			}
		}

		private void PauseAction() {
			Play.Instance.Paused = !Play.Instance.Paused;
		}

		private void UpdateInfo() {
			// Update text
			if (Multiplier == 1) {
				commonTrack.comboText.text = null;
			} else {
				commonTrack.comboText.text = $"{Multiplier}<sub>x</sub>";
			}

			// Update solo note count
			if (Play.Instance.SongTime >= SoloSection?.time - 5 && Play.Instance.SongTime <= SoloSection?.time) {
				soloNoteCount = 0;

				for (int i = hitChartIndex; i < Chart.Count; i++) {
					if (Chart[i].time > SoloSection?.EndTime) {
						break;
					} else {
						soloNoteCount++;
					}
				}
			}

			// Set solo box and text
			if (Play.Instance.SongTime >= SoloSection?.time && Play.Instance.SongTime <= SoloSection?.EndTime) {
				commonTrack.soloBox.sprite = commonTrack.soloDefaultSprite;
				commonTrack.soloBox.gameObject.SetActive(true);

				// Set text color
				commonTrack.soloText.colorGradient = new VertexGradient(
					new Color(1f, 1f, 1f),
					new Color(1f, 1f, 1f),
					new Color(0.1320755f, 0.1320755f, 0.1320755f),
					new Color(0.1320755f, 0.1320755f, 0.1320755f)
				);
				commonTrack.soloText.gameObject.SetActive(true);

				soloHitPercent = Mathf.RoundToInt(soloNotesHit / (float) soloNoteCount * 100f);
				commonTrack.soloText.text = $"{soloHitPercent}%\n<size=10><alpha=#66>{soloNotesHit}/{soloNoteCount}</size>";
			} else if (Play.Instance.SongTime >= SoloSection?.EndTime && Play.Instance.SongTime <= SoloSection?.EndTime + 4) {
				if (soloHitPercent >= 100f) {
					// Set text color
					commonTrack.soloText.colorGradient = new VertexGradient(
						new Color(1f, 0.619472f, 0f),
						new Color(1f, 0.619472f, 0f),
						new Color(0.5377358f, 0.2550798f, 0f),
						new Color(0.5377358f, 0.2550798f, 0f)
					);

					commonTrack.soloBox.sprite = commonTrack.soloPerfectSprite;
					commonTrack.soloText.text = "PERFECT\nSOLO!";
				} else if (soloHitPercent >= 95f) {
					commonTrack.soloText.text = "AWESOME\nSOLO!";
				} else if (soloHitPercent >= 90f) {
					commonTrack.soloText.text = "GREAT\nSOLO!";
				} else if (soloHitPercent >= 80f) {
					commonTrack.soloText.text = "GOOD\nSOLO!";
				} else if (soloHitPercent >= 70f) {
					commonTrack.soloText.text = "SOLID\nSOLO!";
				} else if (soloHitPercent >= 60f) {
					commonTrack.soloText.text = "OKAY\nSOLO!";
				} else {
					// Set text color
					commonTrack.soloText.colorGradient = new VertexGradient(
						new Color(1f, 0.1933962f, 0.1933962f),
						new Color(1f, 0.1933962f, 0.1933962f),
						new Color(1f, 0.1332366f, 0.06132078f),
						new Color(1f, 0.1332366f, 0.06132078f)
					);

					commonTrack.soloBox.sprite = commonTrack.soloMessySprite;
					commonTrack.soloText.text = "MESSY\nSOLO!";
				}
			} else {
				commonTrack.soloText.text = null;
				commonTrack.soloText.gameObject.SetActive(false);
				commonTrack.soloBox.gameObject.SetActive(false);
			}

			// Update status
			int index = Combo % 10;
			if (Multiplier != 1 && index == 0) {
				index = 10;
			} else if (Multiplier == MaxMultiplier) {
				index = 10;
			}

			commonTrack.comboMeterRenderer.material.SetFloat("SpriteNum", index);
		}

		private void BeatAction() {
			if (starpowerActive && GameManager.AudioManager.UseStarpowerFx) {
				GameManager.AudioManager.PlaySoundEffect(SfxSample.Clap);
			}
			Beat = true;
		}

		private void StarpowerAction(InputStrategy inputStrategy) {
			if (!starpowerActive && starpowerCharge >= 0.5f) {
				GameManager.AudioManager.PlaySoundEffect(SfxSample.StarPowerDeploy);
				SetReverb(true);
				starpowerActive = true;
			}
		}

		protected float CalcLagCompensation(float currentTime, float noteTime) {
			return (currentTime - noteTime) * (player.trackSpeed / Play.speed);
		}

		private bool IsStarpowerHit() {
			if (Chart.Count > hitChartIndex) {
				return Chart[hitChartIndex].time >= StarpowerSection?.EndTime;
			}

			return false;
		}

		public abstract void SetReverb(bool on);
	}
}