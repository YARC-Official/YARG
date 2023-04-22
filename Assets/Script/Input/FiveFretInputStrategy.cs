using System.Collections.Generic;
using YARG.Data;
using YARG.PlayMode;

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

		private List<NoteInfo> botChart;

		public delegate void FretChangeAction(bool pressed, int fret);
		public delegate void StrumAction();

		public event FretChangeAction FretChangeEvent;
		public event StrumAction StrumEvent;

		public override string[] GetMappingNames() {
			return MAPPING_NAMES;
		}

		public override void InitializeBotMode(object rawChart) {
			botChart = (List<NoteInfo>) rawChart;
		}

		protected override void UpdatePlayerMode() {
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

			// Starpower

			if (WasMappingPressed("starpower")) {
				CallStarpowerEvent();
			}
		}

		protected override void UpdateBotMode() {
			if (botChart == null) {
				return;
			}

			float songTime = Play.Instance.SongTime;

			bool resetForChord = false;
			while (botChart.Count > botChartIndex && botChart[botChartIndex].time <= songTime) {
				// Release old frets
				if (!resetForChord) {
					for (int i = 0; i < 5; i++) {
						FretChangeEvent?.Invoke(false, i);
					}
					resetForChord = true;
				}

				var noteInfo = botChart[botChartIndex];
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

		protected override void UpdateNavigationMode() {
			CallGenericNavigationEventForButton("strumUp", NavigationType.UP);
			CallGenericNavigationEventForButton("strumDown", NavigationType.DOWN);

			CallGenericNavigationEventForButton("green", NavigationType.PRIMARY);
			CallGenericNavigationEventForButton("red", NavigationType.SECONDARY);
			CallGenericNavigationEventForButton("yellow", NavigationType.TERTIARY);

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