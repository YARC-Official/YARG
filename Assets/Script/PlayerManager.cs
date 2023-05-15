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

		private static RandomName RandomNameFromFile() {
			// load credits.txt
			// read each line
			// ignore lines starting with << or <u> or empty lines
			// return random line

			// load Assets/Credits.txt
			var creditsPath = Addressables.LoadAssetAsync<TextAsset>("Credits");
			creditsPath.WaitForCompletion();
			// split credits into lines
			var lines = creditsPath.Result.text.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);


			//var lines = System.IO.File.ReadAllLines(creditsPath);
			var names = new List<string>();
			foreach (var line in lines) {
				if (line.StartsWith("<<") || line.StartsWith("<u>") || line.Length == 0) {
					continue;
				}

				// special conditions
				if (line.Contains("EliteAsian (barely)")) {
					continue;
				}
				if (line.Contains("EliteAsian")) {
					names.Add("<b>E</b>lite<b>A</b>sian");
					continue;
				}
				if (line.Contains("NevesPT")) {
					names.Add("<b>N</b>eves<b>PT</b>");
					continue;
				}

				names.Add(line);
			}

			return new RandomName() {
				name = names[Random.Range(0, names.Count)],
				size = names.Count
			};
		}

		public class Player {
			private static int nextPlayerName = 1;

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
				int counter = 0;
				// do not use the same name twice, if no available names, use "New Player"
				do {
					RandomName randomName = RandomNameFromFile();
					name = randomName.name;
					if (counter++ > randomName.size || name == null) {
						name = $"New Player {nextPlayerName++}";
						break;
					}
				} while (players.Any(i => i.name == name));
			}
		}

		public static List<Player> players = new();

		public static float GlobalCalibration => SettingsManager.Settings.CalibrationNumber.Data / 1000f;

		public static int PlayersWithInstrument(string instrument) {
			return players.Count(i => i.chosenInstrument == instrument);
		}
	}
}