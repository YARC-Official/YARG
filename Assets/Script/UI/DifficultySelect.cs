using System;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
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
		}

		private void OnDestroy() {
			foreach (var option in options) {
				option.MouseHoverEvent -= HoverOption;
				option.MouseClickEvent -= ClickOption;
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
			if (state == State.INSTRUMENT) {
				if (selected >= instruments.Length) {
					player.chosenInstrument = null;
					IncreasePlayerIndex();
				} else {
					player.chosenInstrument = instruments[selected];
					bool showExpertPlus = player.chosenInstrument == "drums"
						|| player.chosenInstrument == "realDrums"
						|| player.chosenInstrument == "ghDrums";
					UpdateDifficulty(showExpertPlus);
				}
			} else if (state == State.DIFFICULTY) {
				player.chosenDifficulty = (Difficulty) selected;
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

		private void IncreasePlayerIndex() {
			// Next non-mic player
			playerIndex++;
			while (playerIndex < PlayerManager.players.Count
				&& PlayerManager.players[playerIndex].inputStrategy is MicInputStrategy) {

				playerIndex++;
			}

			if (playerIndex >= PlayerManager.players.Count) {
				// Set speed
				Play.speed = float.Parse(speedInput.text, CultureInfo.InvariantCulture);
				if (Play.speed <= 0f) {
					Play.speed = 1f;
				}

				// Play song (or download then play)
				Play.song = MainMenu.Instance.chosenSong;
				GameManager.Instance.LoadScene(SceneIndex.PLAY);
			} else {
				UpdateInstrument();
			}
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
					"rhythm" => "Rhythm Guitar",
					"guitar_coop" => "Co-op Guitar",
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
