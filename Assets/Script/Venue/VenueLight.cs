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
		}

		private void ResetToDefault() {
			_animationFrame = 0;

			transform.rotation = _defaultRotation;
			_light.color = _defaultColor;
			_light.intensity = _defaultIntensity;
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