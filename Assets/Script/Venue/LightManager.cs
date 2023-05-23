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
					"venue_light_verse" => VenueLightAnimation.Verse,
					"venue_light_chorus" => VenueLightAnimation.Chorus,
					"venue_light_manual_cool" => VenueLightAnimation.Manual_Cool,
					"venue_light_manual_warm" => VenueLightAnimation.Manual_Warm,
					"venue_light_dischord" => VenueLightAnimation.Dischord,
					"venue_light_loop_cool" => VenueLightAnimation.Loop_Cool,
					"venue_light_silhouettes" => VenueLightAnimation.Silhouettes,
					"venue_light_loop_warm" => VenueLightAnimation.Loop_Warm,
					"venue_light_frenzy" => VenueLightAnimation.Frenzy,
					"venue_light_blackout_fast" => VenueLightAnimation.Blackout_Fast,
					"venue_light_flare_fast" => VenueLightAnimation.Flare_Fast,
					"venue_light_searchlights" => VenueLightAnimation.Searchlights,
					"venue_light_harmony" => VenueLightAnimation.Harmony,
					"venue_light_sweep" => VenueLightAnimation.Sweep,
					"venue_light_bre" => VenueLightAnimation.Bre,
					"venue_light_blackout_slow" => VenueLightAnimation.Blackout_Slow,
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