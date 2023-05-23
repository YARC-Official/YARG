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
		StrobeFast,
		StrobeSlow,
		Verse,
		Chorus,
		Manual_Cool,
		Manual_Warm,
		Dischord,
		Loop_Cool,
		Silhouettes,
		Loop_Warm,
		Frenzy,
		Blackout_Fast,
		Flare_Fast,
		Searchlights,
		Flare_Slow,
		Harmony,
		Sweep,
		Bre,
		Blackout_Slow,
		Stomp,
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
			}
		}

		private int _animationFrame = 0;
		public int AnimationFrame {
			get => _animationFrame;
			set {
				_animationFrame = value;

				switch (Animation) {
					case VenueLightAnimation.Stomp:
						if (_animationFrame % 2 == 0) {
							Toggle();
						}
						break;
					case VenueLightAnimation.Flare_Slow:
						if (_animationFrame % 2 == 0) {
							Toggle();
						}
						break;
					case VenueLightAnimation.Dischord:
						if (_animationFrame % 2 == 0) {
							Toggle();
						}
						break;
					case VenueLightAnimation.Searchlights:
						if (_animationFrame % 2 == 0) {
							Toggle();
						}
						break;
					case VenueLightAnimation.Sweep:
						if (_animationFrame % 2 == 0) {
							Toggle();
						}
						break;
					case VenueLightAnimation.Silhouettes:
						if (_animationFrame % 2 == 0) {
							Toggle();
						}
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

		public void On32ndNote() {
			if (Animation == VenueLightAnimation.StrobeFast) {
				Toggle();
			}
			if (Animation == VenueLightAnimation.Verse) {
				ResetToDefault();
			}
			if (Animation == VenueLightAnimation.Chorus) {
				ResetToDefault();
			}
			if (Animation == VenueLightAnimation.Manual_Cool) {
				ResetToDefault();
			}
			if (Animation == VenueLightAnimation.Manual_Warm) {
				ResetToDefault();
			}
			if (Animation == VenueLightAnimation.Loop_Cool) {
				ResetToDefault();
			}
			if (Animation == VenueLightAnimation.Loop_Warm) {
				ResetToDefault();
			}
			if (Animation == VenueLightAnimation.Frenzy) {
				Toggle();
			}
			if (Animation == VenueLightAnimation.Blackout_Fast) {
				Off();
			}
			if (Animation == VenueLightAnimation.Flare_Fast) {
				Toggle();
			}
			if (Animation == VenueLightAnimation.Harmony) {
				ResetToDefault();
			}
			if (Animation == VenueLightAnimation.Bre) {
				Toggle();
			}
			if (Animation == VenueLightAnimation.Blackout_Slow) {
				Off();
			}

		}

		private void ResetToDefault() {
			_animationFrame = 0;

			transform.rotation = _defaultRotation;
			_light.color = _defaultColor;
			_light.intensity = _defaultIntensity;
		}

		private void Off() {
			_light.intensity = 0f;
		}

		private void Toggle() {
			if (_light.intensity == 0f) {
				_light.intensity = _defaultIntensity;
			} else {
				_light.intensity = 0f;
			}
		}
	}
}