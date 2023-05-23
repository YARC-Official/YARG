using UnityEngine;

namespace YARG.Venue {
	public enum VenueLightLocation {
		Left,
		Right,
		Front,
		Back,
		Center,
		Crowd,
	}

	public enum VenueLightAnimation {
		None = 0,
		ManualCool,
		ManualWarm,
		Dischord,
		Stomp,
		LoopCool,
		LoopWarm,
		Harmony,
		Frenzy,
		Silhouettes,
		Searchlights,
		Sweep,
		StrobeFast,
		StrobeSlow,
		BlackoutFast,
		BlackoutSlow,
		FlareFast,
		FlareSlow,
		BRE,
		Verse,
		Chorus,
	}

	[RequireComponent(typeof(Light))]
	public class VenueLight : MonoBehaviour {
		private Light _light;

		[field: Header("This GameObject MUST be enabled by default!")]
		[field: SerializeField]
		public VenueLightLocation Location { get; private set; }

		private Quaternion _defaultRotation;
		private Color _defaultColor;
		private float _defaultIntensity;

		private VenueLightAnimation _animation;
		public VenueLightAnimation Animation {
			get => _animation;
			set {
				ResetToDefault();
				_animation = value;

				switch (_animation) {
					case VenueLightAnimation.BlackoutFast:
					case VenueLightAnimation.BlackoutSlow:
						SetOn(false);
						break;
				}
			}
		}

		private int _animationFrame = 0;
		public int AnimationFrame {
			get => _animationFrame;
			set {
				_animationFrame = value;

				switch (Animation) {
					case VenueLightAnimation.Stomp:
					case VenueLightAnimation.FlareSlow:
					case VenueLightAnimation.Dischord:
					case VenueLightAnimation.Searchlights:
					case VenueLightAnimation.Sweep:
					case VenueLightAnimation.Silhouettes:
						SetOn(_animationFrame % 2 == 0);
						break;
					default:
						_animationFrame = 0;
						break;
				}
			}
		}

		private void Start() {
			_light = GetComponent<Light>();

			_defaultRotation = transform.rotation;
			_defaultColor = _light.color;
			_defaultIntensity = _light.intensity;
		}

		public void On32ndNote(int noteIndex) {
			switch (Animation) {
				case VenueLightAnimation.StrobeFast:
				case VenueLightAnimation.Frenzy:
				case VenueLightAnimation.FlareFast:
				case VenueLightAnimation.BRE:
					Toggle();
					break;
				case VenueLightAnimation.StrobeSlow:
				case VenueLightAnimation.FlareSlow:
					if (noteIndex % 2 == 1) {
						Toggle();
					}
					break;
			}
		}

		private void ResetToDefault() {
			_animationFrame = 0;

			transform.rotation = _defaultRotation;
			_light.color = _defaultColor;
			_light.intensity = _defaultIntensity;
		}

		private void SetOn(bool on) {
			_light.intensity = on ? _defaultIntensity : 0f;
		}

		private void Toggle() {
			SetOn(_light.intensity == 0f);
		}
	}
}