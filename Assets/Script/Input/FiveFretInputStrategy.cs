using System.Collections.Generic;
using YARG.Data;
using YARG.PlayMode;

namespace YARG.Input {
	public class FiveFretInputStrategy : InputStrategy {
		public const string GREEN = "green";
		public const string RED = "red";
		public const string YELLOW = "yellow";
		public const string BLUE = "blue";
		public const string ORANGE = "orange";

		public const string STRUM_UP = "strum_up";
		public const string STRUM_DOWN = "strum_down";

		public const string STAR_POWER = "star_power";
		public const string PAUSE = "pause";

		private List<NoteInfo> botChart;

		public delegate void FretChangeAction(bool pressed, int fret);
		public delegate void StrumAction();

		public event FretChangeAction FretChangeEvent;
		public event StrumAction StrumEvent;

		protected override Dictionary<string, ControlBinding> GetMappings() => new() {
			{ GREEN,      new(BindingType.BUTTON, "Green", GREEN) },
			{ RED,        new(BindingType.BUTTON, "Red", RED) },
			{ YELLOW,     new(BindingType.BUTTON, "Yellow", YELLOW) },
			{ BLUE,       new(BindingType.BUTTON, "Blue", BLUE) },
			{ ORANGE,     new(BindingType.BUTTON, "Orange", ORANGE) },

			{ STRUM_UP,   new(BindingType.BUTTON, "Strum Up", STRUM_UP, STRUM_DOWN) },
			{ STRUM_DOWN, new(BindingType.BUTTON, "Strum Down", STRUM_DOWN, STRUM_UP) },

			{ STAR_POWER, new(BindingType.BUTTON, "Star Power", STAR_POWER) },
			{ PAUSE,      new(BindingType.BUTTON, "Pause", PAUSE) },
		};

		public override string GetIconName() {
			return "guitar";
		}

		public override void InitializeBotMode(object rawChart) {
			botChart = (List<NoteInfo>) rawChart;
		}

		protected override void UpdatePlayerMode() {
			void HandleFret(string mapping, int index) {
				if (WasMappingPressed(mapping)) {
					FretChangeEvent?.Invoke(true, index);
				} else if (WasMappingReleased(mapping)) {
					FretChangeEvent?.Invoke(false, index);
				}
			}

			// Deal with fret inputs

			HandleFret(GREEN, 0);
			HandleFret(RED, 1);
			HandleFret(YELLOW, 2);
			HandleFret(BLUE, 3);
			HandleFret(ORANGE, 4);

			// Deal with strumming

			if (WasMappingPressed(STRUM_UP)) {
				StrumEvent?.Invoke();
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed(STRUM_DOWN)) {
				StrumEvent?.Invoke();
				CallGenericCalbirationEvent();
			}

			// Starpower

			if (WasMappingPressed(STAR_POWER)) {
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
			NavigationEventForMapping(MenuAction.Confirm, GREEN);
			NavigationEventForMapping(MenuAction.Back, RED);

			NavigationEventForMapping(MenuAction.Shortcut1, YELLOW);
			NavigationEventForMapping(MenuAction.Shortcut2, BLUE);
			NavigationEventForMapping(MenuAction.Shortcut3, ORANGE);

			NavigationHoldableForMapping(MenuAction.Up, STRUM_UP);
			NavigationHoldableForMapping(MenuAction.Down, STRUM_DOWN);

			NavigationEventForMapping(MenuAction.More, STAR_POWER);

			if (WasMappingPressed(PAUSE)) {
				CallPauseEvent();
			}
		}

		public override Instrument[] GetAllowedInstruments() {
			return new Instrument[] {
				Instrument.GUITAR,
				Instrument.BASS,
				Instrument.KEYS,
				Instrument.GUITAR_COOP,
				Instrument.RHYTHM,
			};
		}

		public override string GetTrackPath() {
			return "Tracks/FiveFret";
		}
	}
}