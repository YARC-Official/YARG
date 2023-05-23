using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.PlayMode;

namespace YARG.Venue {
	public class LightManager : MonoBehaviour {
		private List<VenueLight> _lights;

		private float _32ndNoteUpdate;

		private void Start() {
			_lights = transform.parent.GetComponentsInChildren<VenueLight>().ToList();
			VenueManager.OnEventRecieve += VenueEvent;
		}

		private void OnDestroy() {
			VenueManager.OnEventRecieve -= VenueEvent;
		}

		private void Update() {
			// Call "On32ndNote" events
			_32ndNoteUpdate += 1f / Play.Instance.CurrentBeatsPerSecond * Time.deltaTime;
			if (_32ndNoteUpdate >= 1f / 32f) {
				foreach (var light in _lights) {
					light.On32ndNote();
				}

				_32ndNoteUpdate = 0f;
			}
		}

		private void VenueEvent(string eventName) {
			if (eventName == "venue_lightFrame_next") {
				foreach (var light in _lights) {
					light.AnimationFrame++;
				}
			} else {
				VenueLightAnimation anim = eventName switch {
					"venue_light_strobeFast" => VenueLightAnimation.StrobeFast,
					"venue_light_onOffMode" => VenueLightAnimation.Stomp,
					_ => VenueLightAnimation.None
				};

				foreach (var light in _lights) {
					light.Animation = anim;
				}
			}
		}
	}
}