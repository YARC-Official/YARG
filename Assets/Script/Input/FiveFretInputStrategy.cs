using System.Collections.Generic;
using YARG.Data;

namespace YARG.Input {
	public class FiveFretInputStrategy : InputStrategy {
		public static readonly string[] MAPPING_NAMES = new string[] {
			"green",
			"red",
			"yellow",
			"blue",
			"orange",
			"strumUp",
			"strumDown",
			"starpower",
			"pause"
		};

		public delegate void FretChangeAction(bool pressed, int fret);
		public delegate void StrumAction();

		public event FretChangeAction FretChangeEvent;
		public event StrumAction StrumEvent;

		public override string[] GetMappingNames() {
			return MAPPING_NAMES;
		}

		public override void UpdatePlayerMode() {
			// Deal with fret inputs

			for (int i = 0; i < 5; i++) {
				if (WasMappingPressed(MAPPING_NAMES[i])) {
					FretChangeEvent?.Invoke(true, i);
				} else if (WasMappingReleased(MAPPING_NAMES[i])) {
					FretChangeEvent?.Invoke(false, i);
				}
			}

			// Deal with strumming

			if (WasMappingPressed("strumUp")) {
				StrumEvent?.Invoke();
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed("strumDown")) {
				StrumEvent?.Invoke();
				CallGenericCalbirationEvent();
			}

			// Starpower & Pause

			if (WasMappingPressed("starpower")) {
				CallStarpowerEvent();
			}

			if (WasMappingPressed("pause")) {
				CallPauseEvent();
			}
		}

		public override void UpdateBotMode(object rawChart, float songTime) {
			var chart = (List<NoteInfo>) rawChart;

			bool resetForChord = false;
			while (chart.Count > botChartIndex && chart[botChartIndex].time <= songTime) {
				// Release old frets
				if (!resetForChord) {
					for (int i = 0; i < 5; i++) {
						FretChangeEvent?.Invoke(false, i);
					}
					resetForChord = true;
				}

				var noteInfo = chart[botChartIndex];
				botChartIndex++;

				// Skip fret press if open note
				if (noteInfo.fret != 5) {
					FretChangeEvent?.Invoke(true, noteInfo.fret);
				}

				// Strum
				StrumEvent?.Invoke();
			}

			// Constantly activate starpower
			CallStarpowerEvent();
		}

		public override void UpdateNavigationMode() {
			CallGenericNavigationEventForButton("strumUp", NavigationType.UP);
			CallGenericNavigationEventForButton("strumDown", NavigationType.DOWN);

			if (WasMappingPressed("green")) {
				CallGenericNavigationEvent(NavigationType.PRIMARY, true);
			}

			if (WasMappingPressed("red")) {
				CallGenericNavigationEvent(NavigationType.SECONDARY, true);
			}

			if (WasMappingPressed("yellow")) {
				CallGenericNavigationEvent(NavigationType.TERTIARY, true);
			}

			if (WasMappingPressed("pause")) {
				CallPauseEvent();
			}
		}

		public override string[] GetAllowedInstruments() {
			return new string[] {
				"guitar",
				"bass",
				"keys",
				"guitar_coop",
				"rhythm",
			};
		}

		public override string GetTrackPath() {
			return "Tracks/FiveFret";
		}
	}
}