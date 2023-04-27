using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using YARG.Settings;

namespace YARG.PlayMode {
	public sealed class CommonTrack : MonoBehaviour {
		public Camera TrackCamera { get; private set; }

		[SerializeField]
		private Camera normalCamera;
		[SerializeField]
		private Camera highFovCamera;

		[Space]
		public MeshRenderer trackRenderer;
		public Transform hitWindow;

		[Space]
		public TextMeshPro comboText;
		public MeshRenderer comboMeterRenderer;
		public MeshRenderer starpowerBarTop;

		[Space]
		public MeshRenderer comboRing;
		public Material nonFCRing;
		public SpriteRenderer comboSunburst;
		public GameObject maxComboLight;
		public GameObject starpowerLight;
		public Sprite sunBurstSprite;
		public Sprite sunBurstSpriteStarpower;
		public ParticleSystem starPowerParticles;
		public ParticleSystem starPowerParticles2;
		public Light starPowerParticlesLight;
		public Light starPowerParticles2Light;
		public GameObject starPowerLightIndicators;
		public GameObject kickFlash;

		[Space]
		[SerializeField]
		public Color comboSunburstSPColor;

		[Space]
		public TextMeshPro soloText;
		public SpriteRenderer soloBox;
		public Sprite soloMessySprite;
		public Sprite soloPerfectSprite;
		public Sprite soloDefaultSprite;


		[Space]
		[SerializeField]
		private Color[] fretColors;
		[SerializeField]
		private Color[] fretInnerColors;
		[SerializeField]
		private Color[] noteColors;
		[SerializeField]
		private Color[] sustainColors;

		[Space]
		public int[] colorMappings;

		private Animation cameraAnimation;

		public void SetupCameras() {
			highFovCamera.gameObject.SetActive(false);
			normalCamera.gameObject.SetActive(false);

			// Enable the correct camera
			if (SettingsManager.GetSettingValue<bool>("highFovCamera")) {
				TrackCamera = highFovCamera;
			} else {
				TrackCamera = normalCamera;
			}

			TrackCamera.gameObject.SetActive(true);

			cameraAnimation = TrackCamera.GetComponent<Animation>();

			// Set anti-aliasing
			var info = TrackCamera.GetComponent<UniversalAdditionalCameraData>();
			if (SettingsManager.GetSettingValue<bool>("lowQuality")) {
				info.antialiasing = AntialiasingMode.None;
			} else {
				info.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
				info.antialiasingQuality = AntialiasingQuality.Low;
			}
		}

		public Color FretColor(int i) {
			return fretColors[colorMappings[i]];
		}

		public Color FretInnerColor(int i) {
			return fretInnerColors[colorMappings[i]];
		}

		public Color NoteColor(int i) {
			return noteColors[colorMappings[i]];
		}

		public Color SustainColor(int i) {
			return sustainColors[colorMappings[i]];
		}

		//UNUSED THIS IS NOW PROCEDURAL, under TrackAnimations.cs - Mia
		public void PlayKickCameraAnimation() {
			StopCameraAnimation();

			cameraAnimation["CameraShakeKickDrums"].wrapMode = WrapMode.Once;
			cameraAnimation.Play("CameraShakeKickDrums");
		}

		public void StopCameraAnimation() {
			cameraAnimation.Stop();
			cameraAnimation.Rewind();
		}
	}
}