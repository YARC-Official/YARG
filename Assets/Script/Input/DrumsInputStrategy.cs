using System.Collections.Generic;
using YARG.Data;

namespace YARG.Input {
	public class DrumsInputStrategy : InputStrategy {
		public static readonly string[] MAPPING_NAMES = new string[] {
			"red_pad",
			"yellow_pad",
			"blue_pad",
			"green_pad",
			"yellow_cymbal",
			"yellow_cymbal_alt",
			"blue_cymbal",
			"green_cymbal",
			"kick",
			"kick_alt"
		};

		public delegate void DrumHitAction(int drum, bool cymbal);

		public event DrumHitAction DrumHitEvent;

		public override string[] GetMappingNames() {
			return MAPPING_NAMES;
		}

		public override void UpdatePlayerMode() {
			// Deal with drum inputs

			if (MappingAsButton("red_pad")?.wasPressedThisFrame ?? false) {
				DrumHitEvent?.Invoke(0, false);
			}

			if (MappingAsButton("yellow_pad")?.wasPressedThisFrame ?? false) {
				DrumHitEvent?.Invoke(1, false);
			}

			if (MappingAsButton("blue_pad")?.wasPressedThisFrame ?? false) {
				DrumHitEvent?.Invoke(2, false);
			}

			if (MappingAsButton("green_pad")?.wasPressedThisFrame ?? false) {
				DrumHitEvent?.Invoke(3, false);
			}

			if (MappingAsButton("yellow_cymbal")?.wasPressedThisFrame ?? false) {
				DrumHitEvent?.Invoke(1, true);
			}

			if (MappingAsButton("yellow_cymbal_alt")?.wasPressedThisFrame ?? false) {
				DrumHitEvent?.Invoke(1, true);
			}

			if (MappingAsButton("blue_cymbal")?.wasPressedThisFrame ?? false) {
				DrumHitEvent?.Invoke(2, true);
			}

			if (MappingAsButton("green_cymbal")?.wasPressedThisFrame ?? false) {
				DrumHitEvent?.Invoke(3, true);
			}

			if (MappingAsButton("kick")?.wasPressedThisFrame ?? false) {
				DrumHitEvent?.Invoke(4, false);
			}

			if (MappingAsButton("kick_alt")?.wasPressedThisFrame ?? false) {
				DrumHitEvent?.Invoke(4, false);
			}

			// Constantly activate starpower
			CallStarpowerEvent();
		}

		public override void UpdateBotMode(object rawChart, float songTime) {
			var chart = (List<NoteInfo>) rawChart;

			while (chart.Count > botChartIndex && chart[botChartIndex].time <= songTime) {
				var noteInfo = chart[botChartIndex];
				botChartIndex++;

				// Hit
				DrumHitEvent?.Invoke(noteInfo.fret, noteInfo.hopo);
			}

			// Constantly activate starpower
			CallStarpowerEvent();
		}

		public override void UpdateNavigationMode() {
			// TODO
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