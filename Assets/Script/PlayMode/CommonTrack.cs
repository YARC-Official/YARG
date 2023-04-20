using TMPro;
using UnityEngine;

namespace YARG.PlayMode {
	public sealed class CommonTrack : MonoBehaviour {
		public Camera trackCamera;

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
		private Color[] noteColors;
		[SerializeField]
		private Color[] sustainColors;

		[Space]
		public int[] colorMappings;

		public Color FretColor(int i) {
			return fretColors[colorMappings[i]];
		}

		public Color NoteColor(int i) {
			return noteColors[colorMappings[i]];
		}

		public Color SustainColor(int i) {
			return sustainColors[colorMappings[i]];
		}
	}
}