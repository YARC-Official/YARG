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
	public class PauseMenu : MonoBehaviour {
		[SerializeField]
		private GenericOption[] options;
		[SerializeField]
		private TextMeshProUGUI header;

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
			// Bind singal event
			if (GameManager.client != null) {
                return;
			}
		}

		private void OnDisable() {
			// Unbind events
			if (GameManager.client != null) {
                return;
			}

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

		private void Update() {
			GameManager.client?.CheckForSignals();

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
			// options[selected].SetSelected(false);

			selected = Array.IndexOf(options, option);

			// Slighty different than with the keyboard.
			// Don't need to bound the top. The bottom should stop and not roll over.
			if (selected >= optionCount) {
				selected = optionCount - 1;
			}

			// Select new one
			// options[selected].SetSelected(true);
		}

		private void ClickOption(GenericOption option) {
			Next();
		}

		public void Next() {
			var player = PlayerManager.players[playerIndex];
		}

        private void UpdateText() {
            // Header
            var player = PlayerManager.players[playerIndex];
            header.text = player.DisplayName;

            // Add to options
            optionCount = 3;
            string[] ops = { 
                "Resume",
				"Quit",
                null 
                };
            optionCount = ops.Length;

            // Set text and sprites
            for (int i = 0; i < 3; i++) {
                options[i].SetText(ops[i]);
                options[i].SetSelected(false);
            }

            // Select
            selected = 1;
            options[1].SetSelected(true);
        }
	}
}