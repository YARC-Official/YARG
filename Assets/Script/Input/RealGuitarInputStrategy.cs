using System;
using System.Collections.Generic;
using PlasticBand.Devices;
using UnityEngine;
using UnityEngine.InputSystem.Controls;
using YARG.Data;
using YARG.PlayMode;

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

		private List<NoteInfo> botChart;

		private int[] fretCache = new int[6];
		private float[] velocityCache = new float[6];

		private float? stringGroupingTimer = null;
		private StrumFlag stringGroupingFlag = StrumFlag.NONE;

		protected override void OnUpdate() {
			base.OnUpdate();

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
		}

		protected override void UpdatePlayerMode() {
			if (InputDevice is not ProGuitar input) {
				return;
			}

			// Update frets
			for (int i = 0; i < 6; i++) {
				int fret = GetFret(input, i).ReadValue();
				if (fret != fretCache[i]) {
					FretChangeEvent?.Invoke(i, fret);
					fretCache[i] = fret;
				}
			}

			// Update strums
			for (int i = 0; i < 6; i++) {
				float vel = GetVelocity(input, i).ReadValue();
				if (vel != velocityCache[i]) {
					stringGroupingFlag |= StrumFlagFromInt(i);
					velocityCache[i] = vel;

					// Start grouping if not already
					stringGroupingTimer ??= 0.05f;
				}
			}

			// Constantly activate starpower (for now)
			CallStarpowerEvent();
		}

		// TODO: Ideally these should be directly implemented in PlasticBand
		private IntegerControl GetFret(ProGuitar input, int i) {
			return i switch {
				0 => input.fret1,
				1 => input.fret2,
				2 => input.fret3,
				3 => input.fret4,
				4 => input.fret5,
				5 => input.fret6,
				_ => null
			};
		}

		private AxisControl GetVelocity(ProGuitar input, int i) {
			return i switch {
				0 => input.velocity1,
				1 => input.velocity2,
				2 => input.velocity3,
				3 => input.velocity4,
				4 => input.velocity5,
				5 => input.velocity6,
				_ => null
			};
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
				var note = botChart[botChartIndex];

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

		protected override void UpdateNavigationMode() {
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