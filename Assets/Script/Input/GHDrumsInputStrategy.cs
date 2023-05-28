using System.Collections.Generic;
using YARG.Data;
using YARG.PlayMode;
using YARG.Settings;

namespace YARG.Input {
	public class GHDrumsInputStrategy : InputStrategy {
		public const string RED_PAD = "red_pad";
		public const string YELLOW_CYMBAL = "yellow_cymbal";
		public const string BLUE_PAD = "blue_pad";
		public const string ORANGE_CYMBAL = "orange_cymbal";
		public const string GREEN_PAD = "green_pad";

		public const string KICK = "kick";
		public const string KICK_ALT = "kick_alt";

		public const string PAUSE = "pause";
		public const string UP = "up";
		public const string DOWN = "down";

		private List<NoteInfo> botChart;

		public delegate void DrumHitAction(int drum);

		public event DrumHitAction DrumHitEvent;

		public GHDrumsInputStrategy() {
			InputMappings = new() {
				{ RED_PAD,       new(BindingType.BUTTON, "Red Pad", RED_PAD) },
				{ YELLOW_CYMBAL, new(BindingType.BUTTON, "Yellow Cymbal", YELLOW_CYMBAL) },
				{ BLUE_PAD,      new(BindingType.BUTTON, "Blue Pad", BLUE_PAD) },
				{ ORANGE_CYMBAL, new(BindingType.BUTTON, "Orange Cymbal", ORANGE_CYMBAL) },
				{ GREEN_PAD,     new(BindingType.BUTTON, "Green Pad", GREEN_PAD) },

				{ KICK,          new(BindingType.BUTTON, "Kick", KICK) },
				{ KICK_ALT,      new(BindingType.BUTTON, "Kick Alt", KICK_ALT) },

				{ PAUSE,         new(BindingType.BUTTON, "Pause", PAUSE) },
				{ UP,            new(BindingType.BUTTON, "Navigate Up", UP) },
				{ DOWN,          new(BindingType.BUTTON, "Navigate Down", DOWN) },
			};
		}

		public override string GetIconName() {
			return "ghDrums";
		}

		protected override void UpdatePlayerMode() {
			// Deal with drum inputs

			if (WasMappingPressed(RED_PAD)) {
				DrumHitEvent?.Invoke(0);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed(YELLOW_CYMBAL)) {
				DrumHitEvent?.Invoke(1);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed(BLUE_PAD)) {
				DrumHitEvent?.Invoke(2);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed(ORANGE_CYMBAL)) {
				DrumHitEvent?.Invoke(3);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed(GREEN_PAD)) {
				DrumHitEvent?.Invoke(4);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed(KICK)) {
				DrumHitEvent?.Invoke(5);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed(KICK_ALT)) {
				DrumHitEvent?.Invoke(5);
				CallGenericCalbirationEvent();
			}

			// Constantly activate starpower
			//CallStarpowerEvent();
		}

		public override void InitializeBotMode(object rawChart) {
			botChart = (List<NoteInfo>) rawChart;
		}

		protected override void UpdateBotMode() {
			if (botChart == null) {
				return;
			}

			float songTime = Play.Instance.SongTime;

			while (botChart.Count > BotChartIndex && botChart[BotChartIndex].time <= songTime) {
				var noteInfo = botChart[BotChartIndex];
				BotChartIndex++;

				if (noteInfo.fret == 5 && SettingsManager.Settings.NoKicks.Data) {
					continue;
				}

				// Hit
				DrumHitEvent?.Invoke(noteInfo.fret);
			}

			// Constantly activate starpower
			//CallStarpowerEvent();
		}

		protected override void UpdateNavigationMode() {
			NavigationEventForMapping(MenuAction.Confirm, GREEN_PAD);
			NavigationEventForMapping(MenuAction.Back, RED_PAD);

			NavigationEventForMapping(MenuAction.Shortcut1, YELLOW_CYMBAL);

			NavigationEventForMapping(MenuAction.Up, UP);
			NavigationEventForMapping(MenuAction.Down, DOWN);

			NavigationEventForMapping(MenuAction.More, KICK);
			NavigationEventForMapping(MenuAction.More, KICK_ALT);

			if (WasMappingPressed(PAUSE)) {
				CallPauseEvent();
			}
		}

		public override Instrument[] GetAllowedInstruments() {
			return new Instrument[] {
				Instrument.GH_DRUMS,
			};
		}

		public override string GetTrackPath() {
			return "Tracks/GHDrums";
		}
	}
}