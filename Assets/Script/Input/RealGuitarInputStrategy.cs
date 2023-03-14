using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Data;

namespace YARG.Input {
	public class RealGuitarInputStrategy : InputStrategy {
		[Flags]
		public enum StrumFlag {
			NONE = 0,
			STR_0 = 1,
			STR_1 = 2,
			STR_2 = 4,
			STR_3 = 8,
			STR_4 = 16,
			STR_5 = 32
		}

		public static StrumFlag StrumFlagFromInt(int str) {
			return (StrumFlag) (1 << str);
		}

		public delegate void FretChangeAction(int str, int fret);
		public delegate void StrumAction(StrumFlag strFlag);

		public event FretChangeAction FretChangeEvent;
		public event StrumAction StrumEvent;

		private int[] fretCache = new int[6];
		private int[] stringCache = new int[6];

		private float? stringGroupingTimer = null;
		private StrumFlag stringGroupingFlag = StrumFlag.NONE;

		public override string[] GetMappingNames() {
			return new string[0];
		}

		public override void UpdatePlayerMode() {
			if (inputDevice is not AbstractProGuitarGampad input) {
				return;
			}

			// Update frets
			for (int i = 0; i < 6; i++) {
				int fret = input.GetFretControl(i).ReadValue();
				if (fret != fretCache[i]) {
					FretChangeEvent?.Invoke(i, fret);
					fretCache[i] = fret;
				}
			}

			// Update strums
			for (int i = 0; i < 6; i++) {
				int vel = input.GetStringControl(i).ReadValue();
				if (vel != stringCache[i]) {
					stringGroupingFlag |= StrumFlagFromInt(i);
					stringCache[i] = vel;

					// Start grouping if not already
					stringGroupingTimer ??= 0.05f;
				}
			}

			// Group up strums
			if (stringGroupingTimer != null) {
				stringGroupingTimer -= Time.deltaTime;

				if (stringGroupingTimer <= 0f) {
					StrumEvent?.Invoke(stringGroupingFlag);
					stringGroupingFlag = StrumFlag.NONE;
					stringGroupingTimer = null;

					CallGenericCalbirationEvent();
				}
			}

			// Constantly activate starpower (for now)
			CallStarpowerEvent();
		}

		public override void UpdateBotMode(object rawChart, float songTime) {
			var chart = (List<NoteInfo>) rawChart;

			while (chart.Count > botChartIndex && chart[botChartIndex].time <= songTime) {
				var note = chart[botChartIndex];

				// Press correct frets
				for (int i = 0; i < 6; i++) {
					int fret = note.stringFrets[i];
					if (fret == -1) {
						fret = 0;
					}

					FretChangeEvent?.Invoke(i, fret);
				}

				// Strum correct strings
				StrumFlag flag = StrumFlag.NONE;
				for (int i = 0; i < 6; i++) {
					if (note.stringFrets[i] != -1) {
						flag |= StrumFlagFromInt(i);
					}
				}
				StrumEvent?.Invoke(flag);

				botChartIndex++;
			}

			// Constantly activate starpower
			CallStarpowerEvent();
		}

		public override void UpdateNavigationMode() {
			// TODO
		}

		public override string[] GetAllowedInstruments() {
			return new string[] {
				"realGuitar",
				"realBass"
			};
		}

		public override string GetTrackPath() {
			return "Tracks/RealGuitar";
		}
	}
}