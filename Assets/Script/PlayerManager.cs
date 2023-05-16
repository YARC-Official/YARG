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

			public Color[] fretColors;
			public Color[] fretInnerColors;
			public Color[] noteColors;
			public Color[] sustainColors;

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

			// function to test CustomColors
			public void setNevesColors() {
				Debug.Log($"Setting colors for {name}");
				if(name.StartsWith("<link=1>")) {
					// fretColors
					this.fretColors = new Color[] {
						fromHEX("#FBFC7F"),
						fromHEX("#FC996F"),
						fromHEX("#FE767F"),
						fromHEX("#D36BAA"),
						fromHEX("#A56AB4"),
						fromHEX("#8068B6"),
					};

					// fretInnerColors slitghly darker
					this.fretInnerColors = new Color[] {
						fromHEX("#E6E66A"),
						fromHEX("#E67E5A"),
						fromHEX("#E95A6A"),
						fromHEX("#B84F95"),
						fromHEX("#8A4E9F"),
						fromHEX("#654CA1"),
					};

					// noteColors
					this.noteColors = new Color[] {
						fromHEX("#E6E66A"),
						fromHEX("#E67E5A"),
						fromHEX("#E95A6A"),
						fromHEX("#B84F95"),
						fromHEX("#8A4E9F"),
						fromHEX("#654CA1"),
						fromHEX("#F7DAFF"),
					};

					// sustainColors
					this.sustainColors = new Color[] {
						fromHEX("#FBFC7F"),
						fromHEX("#FC996F"),
						fromHEX("#FE767F"),
						fromHEX("#D36BAA"),
						fromHEX("#A56AB4"),
						fromHEX("#8068B6"),
						fromHEX("#F7DAFF"),
					};
				} else if(name.StartsWith("<link=2>")) {
					// fretColors
					this.fretColors = new Color[] {
						fromHEX("#FFFFFF"),
						fromHEX("#FFFFFF"),
						fromHEX("#FFFFFF"),
						fromHEX("#FFFFFF"),
						fromHEX("#FFFFFF"),
						fromHEX("#FFFFFF"),
					};

					// fretInnerColors slitghly darker
					this.fretInnerColors = new Color[] {
						fromHEX("#EECC22"),
						fromHEX("#99DD11"),
						fromHEX("#11DDBB"),
						fromHEX("#8844EE"),
						fromHEX("#CC55DD"),
						fromHEX("#FFCE86"),
					};

					// noteColors
					this.noteColors = new Color[] {
						fromHEX("#EECC22"),
						fromHEX("#99DD11"),
						fromHEX("#11DDBB"),
						fromHEX("#8844EE"),
						fromHEX("#CC55DD"),
						fromHEX("#334499"),
						fromHEX("#E95A6A"),
					};

					// sustainColors
					this.sustainColors = new Color[] {
						fromHEX("#FFEE77"),
						fromHEX("#BBFF55"),
						fromHEX("#66EEDD"),
						fromHEX("#AA88FF"),
						fromHEX("#EE99EE"),
						fromHEX("#6677BB"),
						fromHEX("#E95A6A"),
					};
				}
			}

			private Color fromHEX(string hex) {
				Color color = new Color();
				ColorUtility.TryParseHtmlString(hex, out color);
				return color;
			}
		}

		public static List<Player> players = new();

		public static float GlobalCalibration => SettingsManager.Settings.CalibrationNumber.Data / 1000f;

		public static int PlayersWithInstrument(string instrument) {
			return players.Count(i => i.chosenInstrument == instrument);
		}
	}
}