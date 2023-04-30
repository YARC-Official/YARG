using TMPro;
using UnityEngine;

namespace YARG.PlayMode {
	public sealed class CommonTrack : MonoBehaviour {
		[field: SerializeField]
		public Camera TrackCamera { get; private set; }
		[SerializeField]
		private Animation cameraAnimation;

		[Space]
		public MeshRenderer trackRenderer;
		public Transform hitWindow;

		[Space]
		public TextMeshPro comboText;
		public MeshRenderer comboMeterRenderer;
		public MeshRenderer starpowerBarTop;

		[Space]
		public MeshRenderer comboRing;
		public MeshRenderer comboBase;
		public Material nonFCRing;
		public SpriteRenderer comboSunburst;
		public GameObject maxComboLight;
		public GameObject starpowerLight;
		public Sprite sunBurstSprite;
		public Sprite sunBurstSpriteStarpower;
		public Material baseNormal;
		public Material baseGroove;
		public Material baseSP;
		public ParticleSystem starPowerParticles;
		public ParticleSystem starPowerParticles2;
		public Light starPowerParticlesLight;
		public Light starPowerParticles2Light;
		public GameObject starPowerLightIndicators;
		public GameObject kickFlash;

		[Space]
		[SerializeField]
		public Color comboSunburstColor;
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