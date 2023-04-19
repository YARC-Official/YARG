using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Data;
using YARG.Input;
using YARG.PlayMode;

namespace YARG.UI {
	public class DifficultySelect : MonoBehaviour {
		private enum State {
			INSTRUMENT,
			DIFFICULTY,

			VOCALS,
			VOCALS_DIFFICULTY
		}

		Dictionary<PlayerManager.Player, string> tempInstruments = new();
		Dictionary<PlayerManager.Player, Difficulty> tempDifficulties = new();

		[SerializeField]
		private GenericOption[] options;
		[SerializeField]
		private TextMeshProUGUI header;
		[SerializeField]
		private TMP_InputField speedInput;

		private int playerIndex;
		private string[] instruments;
		private State state;

		private int optionCount;
		private int selected;

		private bool isSetlistMode = false;

		public delegate void InstrumentSelectionAction(PlayerManager.Player playerInfo);
		public static event InstrumentSelectionAction OnInstrumentSelection;

		private void Start() {
			foreach (var option in options) {
				option.MouseHoverEvent += HoverOption;
				option.MouseClickEvent += ClickOption;
			}
		}

		private void OnEnable() {
			bool anyMics = false;

			// Bind input events
			foreach (var player in PlayerManager.players) {
				player.inputStrategy.GenericNavigationEvent += OnGenericNavigation;

				if (player.inputStrategy is MicInputStrategy) {
					anyMics = true;
				}
			}

			// Update options
			playerIndex = 0;
			if (anyMics) {
				UpdateVocalOptions();
			} else {
				UpdateInstrument();
			}
		}

		private void OnDisable() {
			// Unbind input events
			foreach (var player in PlayerManager.players) {
				player.inputStrategy.GenericNavigationEvent -= OnGenericNavigation;
			}
			tempDifficulties.Clear();
			tempInstruments.Clear();
		}

		private void OnDestroy() {
			foreach (var option in options) {
				option.MouseHoverEvent -= HoverOption;
				option.MouseClickEvent -= ClickOption;
			}
		}

		private void Update() {
			// Update player navigation
			foreach (var player in PlayerManager.players) {
				player.inputStrategy.UpdateNavigationMode();
			}
		}

		private void OnGenericNavigation(NavigationType navigationType, bool firstPressed) {
			if (!firstPressed) {
				return;
			}

			if (navigationType == NavigationType.UP) {
				MoveOption(-1);
			} else if (navigationType == NavigationType.DOWN) {
				MoveOption(1);
			} else if (navigationType == NavigationType.PRIMARY) {
				Next();
			} else if (navigationType == NavigationType.SECONDARY) {
				MainMenu.Instance.ShowSongSelect();
			}
		}

		private void MoveOption(int i) {
			// Deselect old one
			options[selected].SetSelected(false);

			selected += i;

			if (selected < 0) {
				selected = optionCount - 1;
			} else if (selected >= optionCount) {
				selected = 0;
			}

			// Select new one
			options[selected].SetSelected(true);
		}

		private void HoverOption(GenericOption option) {
			// Deselect old one
			options[selected].SetSelected(false);

			selected = Array.IndexOf(options, option);

			// Slighty different than with the keyboard.
			// Don't need to bound the top. The bottom should stop and not roll over or go to an empty option.
			if (selected >= optionCount) {
				selected = optionCount - 1;
			}

			// Select new one
			options[selected].SetSelected(true);
		}

		private void ClickOption(GenericOption option) {
			Next();
		}

		public void Next() {
			var player = PlayerManager.players[playerIndex];

			string instrument = "";
			if (state == State.INSTRUMENT) {
				if (selected >= instruments.Length) {
					IncreasePlayerIndex();
				} else {
					instrument = instruments[selected];
					bool showExpertPlus = instrument == "drums"
						|| instrument == "realDrums"
						|| instrument == "ghDrums";
					UpdateDifficulty(showExpertPlus);
				}
				tempInstruments.Add(player, instrument);
			} else if (state == State.DIFFICULTY) {
				tempDifficulties.Add(player, (Difficulty) selected);
				OnInstrumentSelection?.Invoke(player);
				IncreasePlayerIndex();
			} else if (state == State.VOCALS) {
				if (selected == 2) {
					foreach (var p in PlayerManager.players) {
						p.chosenInstrument = null;
					}

					playerIndex = -1;
					IncreasePlayerIndex();
				} else {
					foreach (var p in PlayerManager.players) {
						p.chosenInstrument = selected == 0 ? "vocals" : "harmVocals";
					}
					UpdateVocalDifficulties();
				}
			} else if (state == State.VOCALS_DIFFICULTY) {
				foreach (var p in PlayerManager.players) {
					p.chosenDifficulty = (Difficulty) selected;
				}

				playerIndex = -1;
				OnInstrumentSelection?.Invoke(player);
				IncreasePlayerIndex();
			}
		}

		private void WriteTempDiffAndInstrumentToPlayers() {
			foreach (KeyValuePair<PlayerManager.Player, string> pair in tempInstruments) {
				var player = pair.Key;
				var instrument = pair.Value;

				player.setlistInstruments.Add(instrument);
			}

			foreach (KeyValuePair<PlayerManager.Player, Difficulty> pair in tempDifficulties) {
				var player = pair.Key;
				var difficulty = pair.Value;

				player.setlistDifficulties.Add(difficulty);
			}
		}

		private void IncreasePlayerIndex() {
			// Next non-mic player
			playerIndex++;
			while (playerIndex < PlayerManager.players.Count
				&& PlayerManager.players[playerIndex].inputStrategy is MicInputStrategy) {

				playerIndex++;
			}

			if (playerIndex >= PlayerManager.players.Count) {
				// Set speed
				var speed = float.Parse(speedInput.text, CultureInfo.InvariantCulture);
				if (speed <= 0f) {
					speed = 1f;
				}

				WriteTempDiffAndInstrumentToPlayers();

				// Play song (or download then play)

				if (!isSetlistMode) {
					Play.speed = speed;
					StartSong();
                } else
                {
					Play.AddSongToSetlist(MainMenu.Instance.chosenSong, speed);
					MainMenu.Instance.ShowSongSelect();
                }
				
			} else {
				UpdateInstrument();
			}
		}

		private void StartSong()
		{
			Play.song = MainMenu.Instance.chosenSong;
			GameManager.Instance.LoadScene(SceneIndex.PLAY);
		}

		public static void StartSetlist()
        {
			Play.StartSetlist();
        }

		public void UpdateSetlistMode() {
			isSetlistMode = GameObject.Find("Setlist Enabled Checkbox").GetComponent<Toggle>().isOn;
        }

		private void UpdateInstrument() {
			// Header
			var player = PlayerManager.players[playerIndex];
			header.text = player.DisplayName;

			state = State.INSTRUMENT;

			// Get allowed instruments
			var allowedInstruments = player.inputStrategy.GetAllowedInstruments();
			optionCount = allowedInstruments.Length + 1;

			// Add to options
			string[] ops = new string[6];
			instruments = new string[allowedInstruments.Length];
			for (int i = 0; i < allowedInstruments.Length; i++) {
				instruments[i] = allowedInstruments[i];
				ops[i] = allowedInstruments[i] switch {
					"drums" => "Drums",
					"realDrums" => "Pro Drums",
					"guitar" => "Guitar",
					"realGuitar" => "Pro Guitar",
					"bass" => "Bass",
					"realBass" => "Pro Bass",
					"keys" => "Keys",
					"realKeys" => "Pro Keys",
					"vocals" => "Vocals",
					"harmVocals" => "Vocals (Harmony)",
					"ghDrums" => "Drums (5-lane)",
					_ => "Unknown"
				};
			}
			ops[allowedInstruments.Length] = "Sit Out";

			// Set text and sprites
			for (int i = 0; i < 6; i++) {
				options[i].SetText(ops[i]);
				options[i].SetSelected(false);

				if (i < instruments.Length) {
					var sprite = Addressables.LoadAssetAsync<Sprite>($"FontSprites[{instruments[i]}]").WaitForCompletion();
					options[i].SetImage(sprite);
				}
			}

			// Select
			selected = 0;
			options[0].SetSelected(true);
		}

		private void UpdateDifficulty(bool showExpertPlus) {
			state = State.DIFFICULTY;

			optionCount = 4;
			string[] ops = {
				"Easy",
				"Medium",
				"Hard",
				"Expert",
				null,
				null
			};

			if (showExpertPlus) {
				optionCount++;
				ops[4] = "Expert+";
			}

			for (int i = 0; i < 6; i++) {
				options[i].SetText(ops[i]);
				options[i].SetSelected(false);
			}

			selected = 3;
			options[3].SetSelected(true);
		}

		private void UpdateVocalOptions() {
			header.text = "Options for All Vocals";

			state = State.VOCALS;

			optionCount = 3;
			string[] ops = {
				"Solo",
				"Harmony",
				"Sit Out (All Vocals)",
				null,
				null,
				null
			};

			for (int i = 0; i < 6; i++) {
				options[i].SetText(ops[i]);
				options[i].SetSelected(false);

				if (i == 0) {
					var sprite = Addressables.LoadAssetAsync<Sprite>("FontSprites[vocals]").WaitForCompletion();
					options[i].SetImage(sprite);
				} else if (i == 1) {
					var sprite = Addressables.LoadAssetAsync<Sprite>("FontSprites[harmVocals]").WaitForCompletion();
					options[i].SetImage(sprite);
				}
			}

			selected = 0;
			options[0].SetSelected(true);
		}

		private void UpdateVocalDifficulties() {
			header.text = "Options for All Vocals";

			state = State.VOCALS_DIFFICULTY;

			optionCount = 5;
			string[] ops = {
				"Easy",
				"Medium",
				"Hard",
				"Expert",
				"Expert+",
				null
			};

			for (int i = 0; i < 6; i++) {
				options[i].SetText(ops[i]);
				options[i].SetSelected(false);
			}

			selected = 3;
			options[3].SetSelected(true);
		}
	}
}
