using System.Collections.Generic;
using UnityEngine.InputSystem;
using YARG.Data;

namespace YARG.Input {
	public class DrumsInputStrategy : InputStrategy {
		public static readonly string[] MAPPING_NAMES = new string[] {
			"green",
			"red",
			"yellow",
			"blue",
			"kick"
		};

		public delegate void DrumHitAction(int drum);

		public event DrumHitAction DrumHitEvent;

		public DrumsInputStrategy(InputDevice inputDevice, bool botMode) : base(inputDevice, botMode) {

		}

		public override string[] GetMappingNames() {
			return MAPPING_NAMES;
		}

		public override void UpdatePlayerMode() {
			// Deal with drum inputs

			for (int i = 0; i < 5; i++) {
				var key = MappingAsButton(MAPPING_NAMES[i]);
				if (key == null) {
					continue;
				}

				if (key.wasPressedThisFrame) {
					DrumHitEvent?.Invoke(i);
				}
			}

			// Constantly activate starpower
			CallStarpowerEvent();
		}

		public override void UpdateBotMode(List<NoteInfo> chart, float songTime) {
			while (chart.Count > botChartIndex && chart[botChartIndex].time <= songTime) {
				var noteInfo = chart[botChartIndex];
				botChartIndex++;

				// Hit
				DrumHitEvent?.Invoke(noteInfo.fret);
			}

			// Constantly activate starpower
			CallStarpowerEvent();
		}
	}
}