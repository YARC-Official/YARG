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
			if (eventName.StartsWith("venue_lightFrame_")) {
				eventName = eventName.Replace("venue_lightFrame_", "");
				switch (eventName) {
					case "next":
						foreach (var light in _lights) {
							light.AnimationFrame++;
						}
						break;
					case "previous":
						foreach (var light in _lights) {
							light.AnimationFrame--;
						}
						break;
					case "first":
						foreach (var light in _lights) {
							light.AnimationFrame = 0;
						}
						break;
				}
			} else if (eventName.StartsWith("venue_light_")) {
				eventName = eventName.Replace("venue_light_", "");
				VenueLightAnimation anim = eventName switch {
					"manual_cool" => VenueLightAnimation.ManualCool,
					"manual_warm" => VenueLightAnimation.ManualWarm,
					"dischord" => VenueLightAnimation.Dischord,
					"stomp" => VenueLightAnimation.Stomp,
					"loop_cool" => VenueLightAnimation.LoopCool,
					"loop_warm" => VenueLightAnimation.LoopWarm,
					"harmony" => VenueLightAnimation.Harmony,
					"frenzy" => VenueLightAnimation.Frenzy,
					"silhouettes" => VenueLightAnimation.Silhouettes,
					"searchlights" => VenueLightAnimation.Searchlights,
					"sweep" => VenueLightAnimation.Sweep,
					"strobe_fast" => VenueLightAnimation.StrobeFast,
					"strobe_slow" => VenueLightAnimation.StrobeSlow,
					"blackout_fast" => VenueLightAnimation.BlackoutFast,
					"blackout_slow" => VenueLightAnimation.BlackoutSlow,
					"flare_fast" => VenueLightAnimation.FlareFast,
					"flare_slow" => VenueLightAnimation.FlareSlow,
					"bre" => VenueLightAnimation.BRE,
					"verse" => VenueLightAnimation.Verse,
					"chorus" => VenueLightAnimation.Chorus,
					_ => VenueLightAnimation.None
				};

				foreach (var light in _lights) {
					light.Animation = anim;
				}
			}
		}
	}
}