using System;
using UnityEngine;

namespace YARG.PlayMode {
	public class VenueManager : MonoBehaviour {
		public static event Action<string> OnEventReceive;

		private int _eventIndex;

		private void Update() {
			if (!Play.Instance.SongStarted) {
				return;
			}

			var chart = Play.Instance.chart;

			// Update venue events
			while (chart.events.Count > _eventIndex && chart.events[_eventIndex].time <= Play.Instance.SongTime) {
				var name = chart.events[_eventIndex].name;
				if (name.StartsWith("venue_")) {
					OnEventReceive?.Invoke(name);
				}

				_eventIndex++;
			}
		}
	}
}