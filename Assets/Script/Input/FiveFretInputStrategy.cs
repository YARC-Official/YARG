using System.Collections.Generic;
using UnityEngine.InputSystem;
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
			"starpower"
		};

		public delegate void FretChangeAction(bool pressed, int fret);
		public delegate void StrumAction();

		public event FretChangeAction FretChangeEvent;
		public event StrumAction StrumEvent;

		public FiveFretInputStrategy(InputDevice inputDevice, bool botMode) : base(inputDevice, botMode) {

		}

		public override string[] GetMappingNames() {
			return MAPPING_NAMES;
		}

		public override void UpdatePlayerMode() {
			// Deal with fret inputs

			for (int i = 0; i < 5; i++) {
				var key = MappingAsButton(MAPPING_NAMES[i]);
				if (key == null) {
					continue;
				}

				if (key.wasPressedThisFrame) {
					FretChangeEvent?.Invoke(true, i);
				} else if (key.wasReleasedThisFrame) {
					FretChangeEvent?.Invoke(false, i);
				}
			}

			// Deal with strumming

			if (MappingAsButton("strumUp")?.wasPressedThisFrame ?? false) {
				StrumEvent?.Invoke();
				CallGenericCalbirationEvent();
			}

			if (MappingAsButton("strumDown")?.wasPressedThisFrame ?? false) {
				StrumEvent?.Invoke();
				CallGenericCalbirationEvent();
			}

			// Starpower

			if (MappingAsButton("starpower")?.wasPressedThisFrame ?? false) {
				CallStarpowerEvent();
			}
		}

		public override void UpdateBotMode(List<NoteInfo> chart, float songTime) {
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
			CallGenericNavigationEventForButton(MappingAsButton("strumUp"), NavigationType.UP);
			CallGenericNavigationEventForButton(MappingAsButton("strumDown"), NavigationType.DOWN);

			if (MappingAsButton("green")?.wasPressedThisFrame ?? false) {
				CallGenericNavigationEvent(NavigationType.PRIMARY, true);
			}

			if (MappingAsButton("red")?.wasPressedThisFrame ?? false) {
				CallGenericNavigationEvent(NavigationType.SECONDARY, true);
			}
		}
	}
}