using System.Collections.Generic;
using YARG.Data;
using YARG.PlayMode;
using YARG.Settings;

namespace YARG.Input {
	public class DrumsInputStrategy : InputStrategy {
		public const string RED_PAD = "red_pad";
		public const string YELLOW_PAD = "yellow_pad";
		public const string BLUE_PAD = "blue_pad";
		public const string GREEN_PAD = "green_pad";

		public const string YELLOW_CYMBAL = "yellow_cymbal";
		public const string BLUE_CYMBAL = "blue_cymbal";
		public const string GREEN_CYMBAL = "green_cymbal";

		public const string KICK = "kick";
		public const string KICK_ALT = "kick_alt";

		public const string PAUSE = "pause";
		public const string UP = "up";
		public const string DOWN = "down";

		private List<NoteInfo> botChart;

		public delegate void DrumHitAction(int drum, bool cymbal);

		public event DrumHitAction DrumHitEvent;

		protected override Dictionary<string, ControlBinding> GetMappings() => new() {
			{ RED_PAD,       new(BindingType.BUTTON, "Red Pad", RED_PAD) },
			{ YELLOW_PAD,    new(BindingType.BUTTON, "Yellow Pad (Menu Up)", YELLOW_PAD) },
			{ BLUE_PAD,      new(BindingType.BUTTON, "Blue Pad (Menu Down)", BLUE_PAD) },
			{ GREEN_PAD,     new(BindingType.BUTTON, "Green Pad", GREEN_PAD) },

			{ YELLOW_CYMBAL, new(BindingType.BUTTON, "Yellow Cymbal", YELLOW_CYMBAL) },
			{ BLUE_CYMBAL,   new(BindingType.BUTTON, "Blue Cymbal", BLUE_CYMBAL) },
			{ GREEN_CYMBAL,  new(BindingType.BUTTON, "Green Cymbal", GREEN_CYMBAL) },

			{ KICK,          new(BindingType.BUTTON, "Kick", KICK) },
			{ KICK_ALT,      new(BindingType.BUTTON, "Kick Alt", KICK_ALT) },

			{ PAUSE,         new(BindingType.BUTTON, "Pause", PAUSE) },
			{ UP,            new(BindingType.BUTTON, "Navigate Up", UP) },
			{ DOWN,          new(BindingType.BUTTON, "Navigate Down", DOWN) },
		};

		protected override void UpdatePlayerMode() {
			// Deal with drum inputs

			if (WasMappingPressed(RED_PAD)) {
				DrumHitEvent?.Invoke(0, false);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed(YELLOW_PAD)) {
				DrumHitEvent?.Invoke(1, false);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed(BLUE_PAD)) {
				DrumHitEvent?.Invoke(2, false);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed(GREEN_PAD)) {
				DrumHitEvent?.Invoke(3, false);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed(YELLOW_CYMBAL)) {
				DrumHitEvent?.Invoke(1, true);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed(BLUE_CYMBAL)) {
				DrumHitEvent?.Invoke(2, true);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed(GREEN_CYMBAL)) {
				DrumHitEvent?.Invoke(3, true);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed(KICK)) {
				DrumHitEvent?.Invoke(4, false);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed(KICK_ALT)) {
				DrumHitEvent?.Invoke(4, false);
				CallGenericCalbirationEvent();
			}
		}

		public override void InitializeBotMode(object rawChart) {
			botChart = (List<NoteInfo>) rawChart;
		}

		protected override void UpdateBotMode() {
			if (botChart == null) {
				return;
			}

			float songTime = Play.Instance.SongTime;

			while (botChart.Count > botChartIndex && botChart[botChartIndex].time <= songTime) {
				var noteInfo = botChart[botChartIndex];
				botChartIndex++;

				// Deal with no kicks
				if (noteInfo.fret == 4 && SettingsManager.Settings.NoKicks.Data) {
					continue;
				}

				// Hit
				DrumHitEvent?.Invoke(noteInfo.fret, noteInfo.hopo);
			}
		}

		public void ActivateStarpower() {
			CallStarpowerEvent();
		}

		protected override void UpdateNavigationMode() {
			CallGenericNavigationEventForButton(YELLOW_PAD, NavigationType.UP);
			CallGenericNavigationEventForButton(BLUE_PAD, NavigationType.DOWN);
			CallGenericNavigationEventForButton(UP, NavigationType.UP);
			CallGenericNavigationEventForButton(DOWN, NavigationType.DOWN);

			CallGenericNavigationEventForButton(GREEN_PAD, NavigationType.PRIMARY);
			CallGenericNavigationEventForButton(RED_PAD, NavigationType.SECONDARY);
			CallGenericNavigationEventForButton(YELLOW_CYMBAL, NavigationType.TERTIARY);

			if (WasMappingPressed(PAUSE)) {
				CallPauseEvent();
			}
		}

		public override string[] GetAllowedInstruments() {
			return new string[] {
				"drums",
				"realDrums"
			};
		}

		public override string GetTrackPath() {
			return "Tracks/Drums";
		}
	}
}