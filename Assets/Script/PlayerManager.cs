using System.Collections.Generic;
using System.Linq;
using YARG.Data;
using YARG.Input;
using YARG.PlayMode;
using YARG.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace YARG {
	public static class PlayerManager {
		public struct LastScore {
			public DiffPercent percentage;
			public DiffScore score;
			public int notesHit;
			public int notesMissed;
			public int maxCombo;
		}

		public struct RandomName {
			public string name;
			public int size;
		}

		public class Player {
			private static int _nextPlayerName = 1;

			public string name;
			public string DisplayName => name + (inputStrategy.botMode ? " <color=#00DBFD>BOT</color>" : "");

			public InputStrategy inputStrategy;

			public float trackSpeed = 5f;
			public bool leftyFlip = false;

			public bool brutalMode = false;

			public string chosenInstrument = "guitar";
			public Difficulty chosenDifficulty = Difficulty.EXPERT;

			public LastScore? lastScore = null;
			public AbstractTrack track = null;

			public Player() {
				name = $"New Player {_nextPlayerName++}";
			}

			public void TryPickRandomName() {
				// Skip if it is not a bot
				if (!inputStrategy?.botMode ?? true) {
					return;
				}

				var shuffledNames = new List<string>(RandomPlayerNames);
				shuffledNames.Shuffle();

				// Do not use the same name twice, if no available names, use "New Player"
				foreach (string t in shuffledNames) {
					name = t;

					// If no player has this name, finish!
					if (players.All(i => i.name != name)) {
						break;
					}
				}

				// Make the name colored
				name = $"<color=yellow>{name}</color>";
			}
		}

		private static readonly List<string> RandomPlayerNames = new();
		public static List<Player> players = new();

		public static float AudioCalibration => SettingsManager.Settings.AudioCalibration.Data / 1000f;

		static PlayerManager() {
			// Load credits file
			var creditsPath = Addressables.LoadAssetAsync<TextAsset>("Credits").WaitForCompletion();

			// Split credits into lines
			var lines = creditsPath.text.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);

			foreach (var line in lines) {
				if (line.StartsWith("<<") || line.StartsWith("<u>") || line.Length == 0) {
					continue;
				}

				// Special conditions
				if (line.Contains("EliteAsian (barely)")) {
					continue;
				}

				RandomPlayerNames.Add(line);
			}
		}

		public static int PlayersWithInstrument(string instrument) {
			return players.Count(i => i.chosenInstrument == instrument);
		}
	}
}