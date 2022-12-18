using System.Collections.Generic;
using UnityEngine;
using YARG.Input;

namespace YARG {
	public static class PlayerManager {
		public class Player {
			public string name;

			public InputStrategy inputStrategy;
			public float trackSpeed = 7f;

			public string chosenInstrument = "guitar";
			public int chosenDifficulty = 4;
		}

		public static int nextPlayerIndex = 1;
		public static List<Player> players = new();

		private static bool _lowQualityMode = false;
		public static bool LowQualityMode {
			get => _lowQualityMode;
			set {
				_lowQualityMode = value;

				QualitySettings.SetQualityLevel(_lowQualityMode ? 0 : 1, true);
			}
		}
	}
}