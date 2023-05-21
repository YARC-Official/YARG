using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
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

		[SerializeField]
		private GenericOption[] options;
		[SerializeField]
		private TextMeshProUGUI header;
		[SerializeField]
		private TMP_InputField speedInput;
		[SerializeField]
		private Toggle brutalModeCheckbox;

		private int playerIndex;
		private string[] instruments;
		private Difficulty[] difficulties;
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
			// Set navigation scheme
			Navigator.Instance.PushScheme(new NavigationScheme(new() {
				new NavigationScheme.Entry(MenuAction.Up, "Up", () => {
					MoveOption(-1);
				}),
				new NavigationScheme.Entry(MenuAction.Down, "Down", () => {
					MoveOption(1);
				}),
				new NavigationScheme.Entry(MenuAction.Confirm, "Confirm", () => {
					Next();
				}),
				new NavigationScheme.Entry(MenuAction.Back, "Back", () => {
					MainMenu.Instance.ShowSongSelect();
				})
			}, false));

			// See if there are any mics
			bool anyMics = false;
			foreach (var player in PlayerManager.players) {
				if (player.inputStrategy is MicInputStrategy) {
					anyMics = true;
				}
			}

			// Update options
			playerIndex = 0;
			if (anyMics) {
				UpdateVocalOptions();
				brutalModeCheckbox.interactable = false;
			} else {
				UpdateInstrument();
			}
		}

		private void OnDestroy() {
			Navigator.Instance.PopScheme();

			foreach (var option in options) {
				option.MouseHoverEvent -= HoverOption;
				option.MouseClickEvent -= ClickOption;
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
					UpdateDifficulty(player.chosenInstrument, showExpertPlus);
				}
			} else if (state == State.DIFFICULTY) {
				player.chosenDifficulty = difficulties[selected];
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
			if (playerIndex != -1) {
				if (brutalModeCheckbox.isOn) {
					PlayerManager.players[playerIndex].brutalMode = true;
				} else {
					PlayerManager.players[playerIndex].brutalMode = false;
				}
			} else {
				brutalModeCheckbox.interactable = true;
			}
			brutalModeCheckbox.isOn = false;

			// Next non-mic player
			playerIndex++;
			while (playerIndex < PlayerManager.players.Count
				&& PlayerManager.players[playerIndex].inputStrategy is MicInputStrategy) {

				playerIndex++;
			}

			if (playerIndex >= PlayerManager.players.Count) {
				Play.speed = float.Parse(speedInput.text, CultureInfo.InvariantCulture);
				if (Play.speed <= 0f) {
					Play.speed = 1f;
				}

				// Play song
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
			var allInstruments = (Instrument[]) Enum.GetValues(typeof(Instrument));

			// Get available instruments
			var availableInstruments = allInstruments
				.Where(instrument => GameManager.Instance.SelectedSong.HasInstrument(instrument)).ToList();

			Debug.Log(GameManager.Instance.SelectedSong.AvailableParts);

			// Force add pro drums and five lane
			if (availableInstruments.Contains(Instrument.DRUMS)) {
				availableInstruments.Add(Instrument.GH_DRUMS);

				// Add real drums if not present
				if (!availableInstruments.Contains(Instrument.REAL_DRUMS)) {
					availableInstruments.Add(Instrument.REAL_DRUMS);
				}
			} else if (availableInstruments.Contains(Instrument.GH_DRUMS)) {
				availableInstruments.Add(Instrument.DRUMS);
				availableInstruments.Add(Instrument.REAL_DRUMS);
			}

			// Filter out to only allowed instruments
			availableInstruments.RemoveAll(i => !player.inputStrategy.GetAllowedInstruments().Contains(i));

			optionCount = availableInstruments.Count + 1;

			// Add to options
			var ops = new string[availableInstruments.Count + 1];
			instruments = new string[availableInstruments.Count];
			for (int i = 0; i < instruments.Length; i++) {
				instruments[i] = availableInstruments[i].ToStringName();
				ops[i] = availableInstruments[i].ToLocalizedName();
			}
			ops[^1] = "Sit Out";

			// Set text and sprites
			for (int i = 0; i < 6; i++) {
				options[i].SetText("");
				options[i].SetSelected(false);

				if (i < ops.Length) {
					options[i].SetText(ops[i]);
				}

				if (i < instruments.Length) {
					var sprite = Addressables.LoadAssetAsync<Sprite>($"FontSprites[{instruments[i]}]").WaitForCompletion();
					options[i].SetImage(sprite);
				}
			}

			// Select
			selected = 0;
			options[0].SetSelected(true);
		}

		private void UpdateDifficulty(string chosenInstrument, bool showExpertPlus) {
			state = State.DIFFICULTY;

			// Get the correct instrument
			var instrument = InstrumentHelper.FromStringName(chosenInstrument);
			if (instrument == Instrument.REAL_DRUMS || instrument == Instrument.GH_DRUMS) {
				instrument = Instrument.DRUMS;
			}

			// Get the available difficulties
			var availableDifficulties = new List<Difficulty>();
			for (int i = 0; i < (int) Difficulty.EXPERT_PLUS; i++) {
				if (!GameManager.Instance.SelectedSong.HasPart(instrument, (Difficulty) i)) {
					continue;
				}
				availableDifficulties.Add((Difficulty) i);
			}

			if (showExpertPlus) {
				availableDifficulties.Add(Difficulty.EXPERT_PLUS);
			}

			optionCount = availableDifficulties.Count;

			difficulties = new Difficulty[optionCount];
			var ops = new string[optionCount];

			for (int i = 0; i < optionCount; i++) {
				ops[i] = availableDifficulties[i] switch {
					Difficulty.EASY => "Easy",
					Difficulty.MEDIUM => "Medium",
					Difficulty.HARD => "Hard",
					Difficulty.EXPERT => "Expert",
					Difficulty.EXPERT_PLUS => "Expert+",
					_ => "Unknown"
				};
				difficulties[i] = availableDifficulties[i];
			}

			for (int i = 0; i < 6; i++) {
				options[i].SetText("");
				options[i].SetSelected(false);

				if (i < ops.Length) {
					options[i].SetText(ops[i]);
				}
			}

			selected = optionCount - 1;
			options[optionCount - 1].SetSelected(true);
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
