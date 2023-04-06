using System.Collections.Generic;
using YARG.Data;
using YARG.Settings;

namespace YARG.Input {
	public class GHDrumsInputStrategy : InputStrategy {
		public static readonly string[] MAPPING_NAMES = new string[] {
			"red_pad",
			"yellow_cymbal",
			"blue_pad",
			"orange_cymbal",
			"green_pad",
			"kick",
			"kick_alt"
		};

		public delegate void DrumHitAction(int drum);

		public event DrumHitAction DrumHitEvent;

		public override string[] GetMappingNames() {
			return MAPPING_NAMES;
		}

		public override void UpdatePlayerMode() {
			// Deal with drum inputs

			if (WasMappingPressed("red_pad")) {
				DrumHitEvent?.Invoke(0);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed("yellow_cymbal")) {
				DrumHitEvent?.Invoke(1);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed("blue_pad")) {
				DrumHitEvent?.Invoke(2);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed("orange_cymbal")) {
				DrumHitEvent?.Invoke(3);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed("green_pad")) {
				DrumHitEvent?.Invoke(4);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed("kick")) {
				DrumHitEvent?.Invoke(5);
				CallGenericCalbirationEvent();
			}

			if (WasMappingPressed("kick_alt")) {
				DrumHitEvent?.Invoke(5);
				CallGenericCalbirationEvent();
			}

			// Constantly activate starpower
			CallStarpowerEvent();
		}

		public override void UpdateBotMode(object rawChart, float songTime) {
			var chart = (List<NoteInfo>) rawChart;

			while (chart.Count > botChartIndex && chart[botChartIndex].time <= songTime) {
				var noteInfo = chart[botChartIndex];
				botChartIndex++;

				if (noteInfo.fret == 5 && SettingsManager.GetSettingValue<bool>("noKicks")) {
					continue;
				}

				// Hit
				DrumHitEvent?.Invoke(noteInfo.fret);
			}

			// Constantly activate starpower
			CallStarpowerEvent();
		}

		public override void UpdateNavigationMode() {
			// TODO
		}

		public override string[] GetAllowedInstruments() {
			return new string[] {
				"ghDrums"
			};
		}

		public override string GetTrackPath() {
			return "Tracks/GHDrums";
		}
	}
}