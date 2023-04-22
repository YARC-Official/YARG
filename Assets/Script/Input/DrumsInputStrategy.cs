using System.Collections.Generic;
using YARG.Data;
using YARG.PlayMode;
using YARG.Settings;

namespace YARG.Input {
	public class DrumsInputStrategy : InputStrategy {
		public static readonly string[] MAPPING_NAMES = new string[] {
			"red_pad",
			"yellow_pad",
			"blue_pad",
			"green_pad",
			"yellow_cymbal",
			"blue_cymbal",
			"green_cymbal",
			"kick",
			"kick_alt"
		};

		private List<NoteInfo> botChart;

		public delegate void DrumHitAction(int drum, bool cymbal);

		public event DrumHitAction DrumHitEvent;

		public override string[] GetMappingNames() {
			return MAPPING_NAMES;
		}

		protected override void UpdatePlayerMode() {
			// Deal with drum inputs

			if (WasMappingPressed("red_pad")) {
				DrumHitEvent?.Invoke(0, false);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed("yellow_pad")) {
				DrumHitEvent?.Invoke(1, false);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed("blue_pad")) {
				DrumHitEvent?.Invoke(2, false);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed("green_pad")) {
				DrumHitEvent?.Invoke(3, false);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed("yellow_cymbal")) {
				DrumHitEvent?.Invoke(1, true);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed("blue_cymbal")) {
				DrumHitEvent?.Invoke(2, true);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed("green_cymbal")) {
				DrumHitEvent?.Invoke(3, true);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed("kick")) {
				DrumHitEvent?.Invoke(4, false);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed("kick_alt")) {
				DrumHitEvent?.Invoke(4, false);
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

			while (botChart.Count > botChartIndex && botChart[botChartIndex].time <= songTime) {
				var noteInfo = botChart[botChartIndex];
				botChartIndex++;

				// Deal with no kicks
				if (noteInfo.fret == 4 && SettingsManager.GetSettingValue<bool>("noKicks")) {
					continue;
				}

				// Hit
				DrumHitEvent?.Invoke(noteInfo.fret, noteInfo.hopo);
			}

			// Constantly activate starpower
			//CallStarpowerEvent();
		}

		public void ActivateStarpower() {
			CallStarpowerEvent();
		}

		protected override void UpdateNavigationMode() {
			CallGenericNavigationEventForButton("yellow_pad", NavigationType.UP);
			CallGenericNavigationEventForButton("blue_pad", NavigationType.DOWN);
			CallGenericNavigationEventForButton("green_pad", NavigationType.PRIMARY);
			CallGenericNavigationEventForButton("red_pad", NavigationType.SECONDARY);
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