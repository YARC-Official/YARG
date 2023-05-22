using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.PlayMode;

namespace YARG.Venue {
	public enum LightLocation {
		Left,
		Right,
		Front,
		Center,
		Back,
		Crowd
	}

	public enum LightAnimation {
		Constant,
		StrobeFast,
		OnOff,
	}

	public class LightManager : MonoBehaviour {
		public struct LightState {
			public bool Enabled;
			public Color Color;
			public LightAnimation Animation;
		}

		[Serializable]
		public class LightInfo {
			public LightLocation Location;
			public Light Light;
		}

		[SerializeField]
		private List<LightInfo> _lights;

		private Dictionary<LightInfo, LightState> _currentLightStates;
		private Dictionary<LightInfo, LightState> _defaultStates;

		private float _32ndNoteUpdate;

		private void Start() {
			// Get default lighting info
			_defaultStates = new();
			foreach (var lightInfo in _lights) {
				_defaultStates[lightInfo] = new LightState {
					Enabled = lightInfo.Light.gameObject.activeSelf,
					Color = lightInfo.Light.color
				};
			}

			// Current light states are the default
			_currentLightStates = new(_defaultStates);
		}

		private void OnEnable() {
			VenueManager.OnEventRecieve += VenueEvent;
		}

		private void OnDisable() {
			VenueManager.OnEventRecieve -= VenueEvent;
		}

		private void Update() {
			bool flash = false;
			_32ndNoteUpdate += 1f / Play.Instance.CurrentBeatsPerSecond * Time.deltaTime;
			if (_32ndNoteUpdate >= 1f / 32f) {
				flash = true;
				_32ndNoteUpdate = 0f;
			}

			foreach (var lightInfo in _lights) {
				if (!_currentLightStates.TryGetValue(lightInfo, out var state)) {
					return;
				}

				if (state.Animation == LightAnimation.StrobeFast) {
					if (flash) {
						lightInfo.Light.gameObject.SetActive(!lightInfo.Light.gameObject.activeSelf);
					}
				}
			}
		}

		private void VenueEvent(string name) {
			switch (name) {
				case "venue_light_default":
					SetLightsDefault();
					break;
				case "venue_light_strobeFast":
					SetLightState(new LightState {
						Enabled = true,
						Color = Color.white,
						Animation = LightAnimation.StrobeFast
					});
					break;
				case "venue_light_onOffMode":
					SetLightState(new LightState {
						Enabled = true,
						Color = Color.white,
						Animation = LightAnimation.OnOff
					});
					break;
				case "venue_lightFrame_next":
					foreach (var lightInfo in _lights) {
						if (!_currentLightStates.TryGetValue(lightInfo, out var state)) {
							return;
						}

						if (state.Animation == LightAnimation.OnOff) {
							lightInfo.Light.gameObject.SetActive(!lightInfo.Light.gameObject.activeSelf);
						}
					}
					break;
			}
		}

		public void SetLightsDefault() {
			foreach (var lightInfo in _lights) {
				if (!_defaultStates.TryGetValue(lightInfo, out var state)) {
					return;
				}

				SetLightInfoFromState(lightInfo, state);
			}
		}

		public void SetLightState(LightLocation location, LightState state) {
			foreach (var lightInfo in _lights) {
				if (lightInfo.Location != location) {
					continue;
				}

				SetLightInfoFromState(lightInfo, state);
			}
		}

		public void SetLightState(LightState state) {
			foreach (var lightInfo in _lights) {
				SetLightInfoFromState(lightInfo, state);
			}
		}

		public void SetLightInfoFromState(LightInfo info, LightState state) {
			_currentLightStates[info] = state;

			info.Light.gameObject.SetActive(state.Enabled);
			if (state.Enabled) {
				info.Light.color = state.Color;
			}
		}
	}
}