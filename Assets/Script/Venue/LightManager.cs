using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.PlayMode;

namespace YARG.Venue {
	public class LightManager : MonoBehaviour {
		private List<VenueLight> _lights;

		private float _32ndNoteUpdate;
		private int _32ndNoteIndex;

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
				_32ndNoteIndex++;

				foreach (var light in _lights) {
					light.On32ndNote(_32ndNoteIndex);
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
					"venue_light_manualCool" => VenueLightAnimation.ManualCool,
					"venue_light_manualWarm" => VenueLightAnimation.ManualWarm,
					"venue_light_dischord" => VenueLightAnimation.Dischord,
					"venue_light_stomp" => VenueLightAnimation.Stomp,
					"venue_light_loopCool" => VenueLightAnimation.LoopCool,
					"venue_light_loopWarm" => VenueLightAnimation.LoopWarm,
					"venue_light_harmony" => VenueLightAnimation.Harmony,
					"venue_light_frenzy" => VenueLightAnimation.Frenzy,
					"venue_light_silhouettes" => VenueLightAnimation.Silhouettes,
					"venue_light_searchlights" => VenueLightAnimation.Searchlights,
					"venue_light_sweep" => VenueLightAnimation.Sweep,
					"venue_light_strobeFast" => VenueLightAnimation.StrobeFast,
					"venue_light_strobeSlow" => VenueLightAnimation.StrobeSlow,
					"venue_light_blackoutFast" => VenueLightAnimation.BlackoutFast,
					"venue_light_blackoutSlow" => VenueLightAnimation.BlackoutSlow,
					"venue_light_flareFast" => VenueLightAnimation.FlareFast,
					"venue_light_flareSlow" => VenueLightAnimation.FlareSlow,
					"venue_light_bre" => VenueLightAnimation.BRE,
					"venue_light_verse" => VenueLightAnimation.Verse,
					"venue_light_chorus" => VenueLightAnimation.Chorus,
					_ => VenueLightAnimation.None
				};

				foreach (var light in _lights) {
					light.Animation = anim;
				}
			}
		}
	}
}