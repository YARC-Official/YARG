using System.Collections.Generic;
using System.Linq;
using YARG.Data;
using YARG.Input;
using YARG.PlayMode;
using YARG.Settings;

namespace YARG {
	public static class PlayerManager {
		public struct LastScore {
			public DiffPercent percentage;
			public DiffScore score;
			public int notesHit;
			public int notesMissed;
		}

		public class Player {
			private static int nextPlayerName = 1;

			public string name;
			public string DisplayName => name + (inputStrategy.botMode ? " (BOT)" : "");

			public InputStrategy inputStrategy;

			public float trackSpeed = 5f;
			public bool leftyFlip = false;

			public bool brutalMode = false;

			public string chosenInstrument = "guitar";
			public Difficulty chosenDifficulty = Difficulty.EXPERT;

			public LastScore? lastScore = null;
			public AbstractTrack track = null;

			public Player() {
				name = $"New Player {nextPlayerName++}";
			}
		}

		public static List<Player> players = new();

		public static float GlobalCalibration => SettingsManager.Settings.CalibrationNumber.Data / 1000f;

		public static int PlayersWithInstrument(string instrument) {
			return players.Count(i => i.chosenInstrument == instrument);
		}
	}
}