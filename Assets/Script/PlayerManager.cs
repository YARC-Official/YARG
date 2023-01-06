using System.Collections.Generic;
using System.Linq;
using YARG.Input;
using YARG.PlayMode;

namespace YARG {
	public static class PlayerManager {
		public struct Score {
			public float percentage;
			public int notesHit;
			public int notesMissed;
		}

		public class Player {
			private static int nextPlayerName = 1;

			public string name;
			public string DisplayName => name + (inputStrategy.botMode ? " (BOT)" : "");

			public InputStrategy inputStrategy;
			public float trackSpeed = 5f;

			public string chosenInstrument = "guitar";
			public int chosenDifficulty = 4;
			public Score? lastScore = null;
			public FiveFretTrack track = null;

			public Player() {
				name = $"New Player {nextPlayerName++}";
			}
		}

		public static List<Player> players = new();

		public static float globalCalibration = -0.15f;

		public static int PlayersWithInstrument(string instrument) {
			return players.Count(i => i.chosenInstrument == instrument);
		}
	}
}