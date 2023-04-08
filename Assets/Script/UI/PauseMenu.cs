using System;
using UnityEngine;
using YARG.Input;
using YARG.PlayMode;

namespace YARG.UI {
	public class PauseMenu : MonoBehaviour {
		[SerializeField]
		private GenericOption[] options;

		[SerializeField]
		private GameObject settingsContainer;

		private int playerIndex;

		private int optionCount;
		private int selected;

		private void Start() {
			foreach (var option in options) {
				option.MouseHoverEvent += HoverOption;
				option.MouseClickEvent += ClickOption;
			}

			UpdateText();
		}

		private void OnEnable() {
			// Note that player navigation is updated in AbstractTrack

			// Bind input events
			foreach (var player in PlayerManager.players) {
				player.inputStrategy.GenericNavigationEvent += OnGenericNavigation;
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
			// Don't need to bound the top. The bottom should stop and not roll over.
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
			if (selected == 0) {
				// Resume
				Play.Instance.Paused = false;
			} else if (selected == 1) {
				// Settings
				settingsContainer.SetActive(!settingsContainer.activeSelf);
			} else if (selected == 2) {
				// Quit
				Play.Instance.Exit();
			}
		}

		private void UpdateText() {
			// Add to options
			optionCount = 3;
			string[] ops = {
				"Resume",
				"Settings",
				"Quit",
				null
			};

			// Set text and sprites
			for (int i = 0; i < 4; i++) {
				options[i].SetText(ops[i]);
				options[i].SetSelected(false);
			}

			// Select
			selected = 0;
			options[0].SetSelected(true);
		}
	}
}