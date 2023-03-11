using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Data;
using YARG.Input;
using YARG.PlayMode;

namespace YARG.UI {
	public class DifficultySelect : MonoBehaviour {
		private enum State {
			INSTRUMENT,
			NORMAL_OR_PRO,
			DIFFICULTY
		}

		[SerializeField]
		private GenericOption[] options;
		[SerializeField]
		private TextMeshProUGUI header;
		[SerializeField]
		private Sprite[] instrumentSprites;
		[SerializeField]
		private TMP_InputField speedInput;

		private int playerIndex;
		private State state;

		private int optionCount;
		private int selected;

		private void OnEnable() {
			playerIndex = 0;

			UpdateInstrument();

			// Bind events
			if (GameManager.client != null) {
				GameManager.client.SignalEvent += SignalRecieved;
			}

			// Bind input events
			foreach (var player in PlayerManager.players) {
				player.inputStrategy.GenericNavigationEvent += OnGenericNavigation;
			}
		}

		private void OnDisable() {
			// Unbind events
			if (GameManager.client != null) {
				GameManager.client.SignalEvent -= SignalRecieved;
			}

			// Unbind input events
			foreach (var player in PlayerManager.players) {
				player.inputStrategy.GenericNavigationEvent -= OnGenericNavigation;
			}
		}

		private void Update() {
			GameManager.client?.CheckForSignals();

			// Enter

			if (Keyboard.current.enterKey.wasPressedThisFrame) {
				Next();
			}

			// Arrows

			if (Keyboard.current.upArrowKey.wasPressedThisFrame) {
				MoveOption(-1);
			}

			if (Keyboard.current.downArrowKey.wasPressedThisFrame) {
				MoveOption(1);
			}

			// Scroll wheel

			var scroll = Mouse.current.scroll.ReadValue().y;
			if (scroll > 0f) {
				MoveOption(-1);
			} else if (scroll < 0f) {
				MoveOption(1);
			}

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

		private void Next() {
			var player = PlayerManager.players[playerIndex];

			if (state == State.INSTRUMENT) {
				player.chosenInstrument = selected switch {
					0 => "guitar",
					1 => "bass",
					2 => "keys",
					3 => "drums",
					4 => "vocals",
					_ => throw new System.Exception("Unreachable.")
				};

				if (player.chosenInstrument != "keys" && player.chosenInstrument != "vocals") {
					UpdateNormalOrPro();
				} else {
					UpdateDifficulty();
				}
			} else if (state == State.NORMAL_OR_PRO) {
				if (selected == 1) {
					player.chosenInstrument = player.chosenInstrument switch {
						"guitar" => "realGuitar",
						"bass" => "realBass",
						"keys" => "realKeys",
						"drums" => "realDrums",
						_ => throw new System.Exception("Unreachable.")
					};
				}
				UpdateDifficulty();
			} else if (state == State.DIFFICULTY) {
				player.chosenDifficulty = (Difficulty) selected;
				playerIndex++;

				if (playerIndex >= PlayerManager.players.Count) {
					// Set speed
					Play.speed = float.Parse(speedInput.text, CultureInfo.InvariantCulture);
					if (Play.speed <= 0f) {
						Play.speed = 1f;
					}

					// Play song (or download then play)
					if (GameManager.client != null) {
						GameManager.client.RequestDownload(MainMenu.Instance.chosenSong.folder.FullName);
					} else {
						Play.song = MainMenu.Instance.chosenSong;
						GameManager.Instance.LoadScene(SceneIndex.PLAY);
					}
				} else {
					UpdateInstrument();
				}
			}
		}

		private void SignalRecieved(string signal) {
			if (signal.StartsWith("DownloadDone,")) {
				Play.song = MainMenu.Instance.chosenSong.Duplicate();

				// Replace song folder
				Play.song.realFolderRemote = Play.song.folder;
				Play.song.folder = new(Path.Combine(GameManager.client.remotePath, signal[13..]));

				GameManager.Instance.LoadScene(SceneIndex.PLAY);
			}
		}

		private void UpdateInstrument() {
			header.text = PlayerManager.players[playerIndex].name;

			state = State.INSTRUMENT;

			optionCount = 5;
			string[] ops = {
				"Guitar",
				"Bass",
				"Keys",
				"Drums",
				"Vocals",
				null
			};

			for (int i = 0; i < 6; i++) {
				options[i].SetText(ops[i]);
				options[i].SetImage(instrumentSprites[i]);
				options[i].SetSelected(false);
			}

			selected = 0;
			options[0].SetSelected(true);
		}

		private void UpdateNormalOrPro() {
			state = State.NORMAL_OR_PRO;

			optionCount = 2;
			string[] ops = {
				"Normal",
				"Pro",
				null,
				null,
				null,
				null
			};

			for (int i = 0; i < 6; i++) {
				options[i].SetText(ops[i]);
				options[i].SetSelected(false);
			}

			selected = 0;
			options[0].SetSelected(true);
		}

		private void UpdateDifficulty() {
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

			for (int i = 0; i < 6; i++) {
				options[i].SetText(ops[i]);
				options[i].SetSelected(false);
			}

			selected = 3;
			options[3].SetSelected(true);
		}
	}
}