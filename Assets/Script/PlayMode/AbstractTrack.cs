using System.ComponentModel.DataAnnotations;
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
		
		[SerializeField]
		protected Camera trackCamera;

		[Space]
		[SerializeField]
		protected MeshRenderer trackRenderer;
		[SerializeField]
		protected Transform hitWindow;

		[Space]
		[SerializeField]
		protected TextMeshPro soloText;
		[SerializeField]
		protected TextMeshPro comboText;
		[SerializeField]
		protected MeshRenderer comboMeterRenderer;
		[SerializeField]
		protected MeshRenderer starpowerBarTop;

		[Space]
		[SerializeField]
		protected SpriteRenderer comboSunburst;
		[SerializeField]
		protected GameObject maxComboLight;
		[SerializeField]
		protected GameObject starpowerLight;
		[SerializeField]
		protected Sprite sunBurstSprite;
		[SerializeField]
		protected Sprite sunBurstSpriteStarpower;

		public EventInfo StarpowerSection {
			get;
			protected set;
		} = null;

		protected float starpowerCharge;
		protected bool starpowerActive;
		protected Light comboSunburstEmbeddedLight;
		
		public EventInfo SoloSection {
			get;
			protected set;
			
		} = null;
		[Space]
		[SerializeField]
		protected SpriteRenderer soloBox;
		[SerializeField]
		protected Sprite soloMessySprite;
		[SerializeField]
		protected Sprite soloPerfectSprite;
		[SerializeField]
		protected Sprite soloDefaultSprite;
		// Overdrive animation parameters
		
		protected Vector3 trackStartPos;
		protected Vector3 trackEndPos = new(0, 0.13f, 0.2f);
		protected float spAnimationDuration = 0.2f;
		protected float elapsedTimeAnim = 0;
		protected bool gotStartPos = false;
		protected bool depressed = false;
		protected bool ascended = false;
		protected bool resetTime = false;

		[SerializeField]
		protected AnimationCurve spStartAnimCurve;
		[SerializeField]
		protected AnimationCurve spEndAnimCurve;

		private int _combo = 0;
		protected int Combo {
			get => _combo;
			set {
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


		private int soloNoteCount=-1;
		protected int soloNotesHit=0;
		private float soloHitPercent=0;
		private int lastHit=-1;
		private void Awake() {
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
			if (SettingsManager.GetSettingValue<bool>("lowQuality")) {
				info.antialiasing = AntialiasingMode.None;
			} else {
				info.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
				info.antialiasingQuality = AntialiasingQuality.Low;
			}
		}

		private void Start() {
			player.track = this;

			player.inputStrategy.StarpowerEvent += StarpowerAction;
			player.inputStrategy.PauseEvent += PauseAction;
			Play.BeatEvent += BeatAction;

			player.lastScore = null;

			GameUI.Instance.AddTrackImage(trackCamera.targetTexture);

			// Adjust hit window
			var scale = hitWindow.localScale;
			hitWindow.localScale = new(scale.x, Constants.HIT_MARGIN * player.trackSpeed * 2f, scale.z);
			hitWindow.gameObject.SetActive(SettingsManager.GetSettingValue<bool>("showHitWindow"));

			comboSunburstEmbeddedLight = comboSunburst.GetComponent<Light>();

			StartTrack();
		}

		protected abstract void StartTrack();

		protected virtual void OnDestroy() {
			// Release render texture
			trackCamera.targetTexture.Release();

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
			if(hitChartIndex>lastHit){
				lastHit=hitChartIndex;
			}
			UpdateInfo();
			UpdateStarpower();

			if (Multiplier >= MaxMultiplier) {
				comboSunburst.gameObject.SetActive(true);
				comboSunburst.transform.Rotate(0f, 0f, Time.deltaTime * -15f);

				maxComboLight.gameObject.SetActive(!starpowerActive);
			} else {
				comboSunburst.gameObject.SetActive(false);

				maxComboLight.gameObject.SetActive(false);
			}

			Beat = false;
		}

		protected abstract void UpdateTrack();

		private void UpdateMaterial() {
			// Update track UV
			var trackMaterial = trackRenderer.material;
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
			if (Play.Instance.SongTime>=SoloSection?.time-2 && Play.Instance.SongTime<=SoloSection?.EndTime-1) {
				trackMaterial.SetFloat("SoloState", Mathf.Lerp(currentSolo, 1f, Time.deltaTime * 2f));
			}else{
				trackMaterial.SetFloat("SoloState", Mathf.Lerp(currentSolo, 0f, Time.deltaTime * 2f));
			}
			
			// Update starpower bar
			var starpowerMat = starpowerBarTop.material;
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

		private void UpdateStarpower() {
			// Update starpower region
			if (IsStarpowerHit()) {
				StarpowerSection = null;
				starpowerCharge += 0.25f;
				if (starpowerCharge > 1f) {
					starpowerCharge = 1f;
				}
			}

			// Update starpower active
			if (starpowerActive) {
				if (starpowerCharge <= 0f) {
					starpowerActive = false;
					starpowerCharge = 0f;
				} else {
					starpowerCharge -= Time.deltaTime / 25f;
				}

				// Start track animation
				elapsedTimeAnim += Time.deltaTime;
				float percentageComplete = elapsedTimeAnim / spAnimationDuration;
				if (!depressed && !ascended) {
					spAnimationDuration = 0.065f;
					trackCamera.transform.position = Vector3.Lerp(trackStartPos, trackStartPos + trackEndPos,
						spStartAnimCurve.Evaluate(percentageComplete));

					if (trackCamera.transform.position == trackStartPos + trackEndPos) {
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
					trackCamera.transform.position = Vector3.Lerp(trackStartPos + trackEndPos, trackStartPos,
						spEndAnimCurve.Evaluate(percentageComplete));

					if (trackCamera.transform.position == trackStartPos + trackEndPos) {
						resetTime = true;
						ascended = true;
					}
				}

				// Update Sunburst color and light
				comboSunburst.sprite = sunBurstSpriteStarpower;
				comboSunburst.color = new Color(255, 255, 255, 141);

				starpowerLight.SetActive(true);
			} else {
				if (!gotStartPos) {
					trackStartPos = trackCamera.transform.position;
					gotStartPos = true;
				}

				depressed = false;
				ascended = false;
				elapsedTimeAnim = 0f;

				//Reset Sunburst color and light to original
				comboSunburst.sprite = sunBurstSprite;
				comboSunburst.color = Color.white;

				starpowerLight.SetActive(false);
			}
		}

		private void PauseAction() {
			Play.Instance.Paused = !Play.Instance.Paused;
		}

		private void UpdateInfo() {
			// Update text
			if (Multiplier == 1) {
				comboText.text = null;
			} else {
				comboText.text = $"{Multiplier}<sub>x</sub>";
			}
			if (Play.Instance.SongTime>=SoloSection?.time-5 && Play.Instance.SongTime<=SoloSection?.time) {
				soloNoteCount=0;
				for(int i=hitChartIndex;i<Chart.Count;i++){
					if(Chart[i].time>SoloSection?.EndTime){
						break;
					}else{
						soloNoteCount++;
					}
					
				}
				
			}

			if (Play.Instance.SongTime>=SoloSection?.time && Play.Instance.SongTime<=SoloSection?.EndTime) {
				soloBox.sprite=soloDefaultSprite;
				soloBox.gameObject.SetActive(true);
				soloText.colorGradient=new VertexGradient(new Color(1f,1f,1f), new Color(1f,1f,1f), new Color(0.1320755f,0.1320755f,0.1320755f), new Color(0.1320755f,0.1320755f,0.1320755f));
				soloText.gameObject.SetActive(true);
				soloHitPercent=Mathf.RoundToInt((soloNotesHit/(float)soloNoteCount)*100f);
				soloText.text = $"{soloHitPercent}%\n<size=10><alpha=#66>{soloNotesHit}/{soloNoteCount}</size>";
			} else if (Play.Instance.SongTime>=SoloSection?.EndTime && Play.Instance.SongTime<=SoloSection?.EndTime+4) {
				
				if(soloHitPercent==100){
					soloText.colorGradient=new VertexGradient(new Color(1f,0.619472F,0f), new Color(1f,0.619472F,0f), new Color(0.5377358f,0.2550798f,0f), new Color(0.5377358f,0.2550798f,0f));
					soloBox.sprite=soloPerfectSprite;
					soloText.text="PERFECT\nSOLO!";
				}else if(soloHitPercent>=95){
					soloText.text="AWESOME\nSOLO!";
				}else if(soloHitPercent>=90){
					soloText.text="GREAT\nSOLO!";
				}else if(soloHitPercent>=80){
					soloText.text="GOOD\nSOLO!";
				}else if(soloHitPercent>=70){
					soloText.text="SOLID\nSOLO!";
				}else if(soloHitPercent>=60){
					soloText.text="OKAY\nSOLO!";
				}else{
					soloBox.sprite=soloMessySprite;
					soloText.colorGradient=new VertexGradient(new Color(1f,0.1933962f,0.1933962f), new Color(1f,0.1933962f,0.1933962f), new Color(1f,0.1332366f,0.06132078f), new Color(1f,0.1332366f,0.06132078f));
					soloText.text="MESSY\nSOLO!";
				}
			}else{
				soloText.text = null;
				soloText.gameObject.SetActive(false);
				soloBox.gameObject.SetActive(false);
			}
			// Update status

			int index = Combo % 10;
			if (Multiplier != 1 && index == 0) {
				index = 10;
			} else if (Multiplier == MaxMultiplier) {
				index = 10;
			}

			comboMeterRenderer.material.SetFloat("SpriteNum", index);
		}

		private void BeatAction() {
			Beat = true;
		}

		private void StarpowerAction(InputStrategy inputStrategy) {
			if (!starpowerActive && starpowerCharge >= 0.5f) {
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
	}
}