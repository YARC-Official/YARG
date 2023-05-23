using System;
using UnityEngine;

namespace YARG.PlayMode {
	public class VenueManager : MonoBehaviour {
		public static event Action<string> OnEventRecieve;

		private int eventIndex = 0;

		private void Update() {
			if (!Play.Instance.SongStarted) {
				return;
			}

			var chart = Play.Instance.chart;

			// Update venue events
			while (chart.events.Count > eventIndex && chart.events[eventIndex].time <= Play.Instance.SongTime) {
				var name = chart.events[eventIndex].name;
				if (name.StartsWith("venue_")) {
					OnEventRecieve?.Invoke(name);
				}

				eventIndex++;
			}
		}
	}
}