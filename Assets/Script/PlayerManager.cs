using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Input;
using YARG.Server;

namespace YARG {
	public static class PlayerManager {
		public struct Score {
			public float percentage;
			public int notesHit;
			public int notesMissed;
		}

		public class Player {
			public string name;
			public string DisplayName => name + (inputStrategy.botMode ? " (BOT)" : "");

			public InputStrategy inputStrategy;
			public float trackSpeed = 4f;

			public string chosenInstrument = "guitar";
			public int chosenDifficulty = 4;

			public Score? lastScore = null;
		}

		public static int nextPlayerIndex = 1;
		public static List<Player> players = new();
		public static Client client;

		private static bool _lowQualityMode = false;
		public static bool LowQualityMode {
			get => _lowQualityMode;
			set {
				_lowQualityMode = value;

				QualitySettings.SetQualityLevel(_lowQualityMode ? 0 : 1, true);
			}
		}

		public static float globalCalibration = -0.15f;

		public static int PlayersWithInstrument(string instrument) {
			return players.Count(i => i.chosenInstrument == instrument);
		}
	}
}