using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UIElements;
using YARG.Input;
using YARG.Utils;

namespace YARG.UI {
	public partial class MainMenu : MonoBehaviour {
		private List<InputDevice> inputDevices;

		private int inputWaitingIndex = -1;
		private int inputWaitingPlayerIndex = -1;
		private string inputWaitingMapping = null;

		private void SetupEditPlayers() {
			var root = editPlayersDocument.rootVisualElement;

			var radioGroup = root.Q<RadioButtonGroup>("InputStrategyRadio");
			var botMode = root.Q<Toggle>("BotMode");
			var trackSpeed = root.Q<FloatField>("TrackSpeed");
			var inputDeviceDropdown = root.Q<DropdownField>("InputDevice");
			var inputStrategyPanel = root.Q<VisualElement>("InputStrategy");
			var settingsList = root.Q<ListView>("SettingsList");
			var settingsPanel = root.Q<VisualElement>("Settings");

			inputStrategyPanel.SetOpacity(0f);
			settingsPanel.SetOpacity(0f);

			root.Q<Button>("BackButton").clicked += ShowMainMenu;

			// Initialize player list

			var playerList = root.Q<ListView>("PlayersList");

			playerList.makeItem = () => new Label();
			playerList.bindItem = (elem, i) => {
				var player = PlayerManager.players[i];

				Label item = (Label) elem;
				item.text = player.name;

				if (player.inputStrategy.botMode) {
					item.text += " (BOT)";
				}
			};

			playerList.itemsSource = PlayerManager.players;
			playerList.RefreshItems();

			playerList.selectedIndicesChanged += _ => {
				inputWaitingIndex = -1;
				inputWaitingPlayerIndex = -1;
				inputWaitingMapping = null;

				// Show/hide settings and input strat panel
				if (playerList.selectedIndex == -1) {
					inputStrategyPanel.SetOpacity(0f);
					settingsPanel.SetOpacity(0f);
					return;
				} else {
					inputStrategyPanel.SetOpacity(1f);
					settingsPanel.SetOpacity(1f);
				}

				var player = PlayerManager.players[playerList.selectedIndex];

				// Update input device dropdown
				var device = player.inputStrategy.inputDevice;
				if (!inputDevices.Contains(device)) {
					player.inputStrategy.inputDevice = null;
					inputDeviceDropdown.index = -1;
				} else {
					inputDeviceDropdown.index = inputDevices.IndexOf(device) + 1;
				}

				// Update radio group
				if (player.inputStrategy is FiveFretInputStrategy) {
					radioGroup.value = 0;
				} else {
					radioGroup.value = -1;
				}

				// Update other input settings
				botMode.value = player.inputStrategy.botMode;
				trackSpeed.value = player.trackSpeed;

				UpdateSettingsList(settingsList, playerList.selectedIndex);
			};

			// Initialize player list buttons

			root.Q<Button>("AddPlayerButton").clicked += () => {
				PlayerManager.players.Add(new PlayerManager.Player {
					name = $"New Player {PlayerManager.nextPlayerIndex++}",
					inputStrategy = new FiveFretInputStrategy(null, false)
				});
				playerList.RefreshItems();

				// Select the new player
				playerList.selectedIndex = PlayerManager.players.Count - 1;
			};
			root.Q<Button>("RemovePlayerButton").clicked += () => {
				if (playerList.selectedIndex != -1) {
					PlayerManager.players.RemoveAt(playerList.selectedIndex);
					playerList.RefreshItems();

					// Force deselect
					playerList.selectedIndex = -1;
				}
			};

			// Initialize input strategies

			UpdateDeviceList(inputDeviceDropdown);

			inputDeviceDropdown.RegisterValueChangedCallback(e => {
				if (inputDeviceDropdown != e.target) {
					return;
				}

				if (inputDeviceDropdown.index == 0) {
					UpdateDeviceList(inputDeviceDropdown);
					inputDeviceDropdown.index = -1;
					return;
				}

				if (playerList.selectedIndex == -1 || inputDeviceDropdown.index == -1) {
					return;
				}

				PlayerManager.players[playerList.selectedIndex].inputStrategy.inputDevice =
					inputDevices[inputDeviceDropdown.index - 1];
			});

			botMode.RegisterValueChangedCallback(e => {
				if (botMode != e.target) {
					return;
				}

				if (playerList.selectedIndex == -1) {
					return;
				}

				var player = PlayerManager.players[playerList.selectedIndex];

				player.inputStrategy.botMode = e.newValue;
				playerList.RefreshItem(playerList.selectedIndex);
			});

			trackSpeed.RegisterValueChangedCallback(e => {
				if (trackSpeed != e.target) {
					return;
				}

				if (playerList.selectedIndex == -1) {
					return;
				}

				var player = PlayerManager.players[playerList.selectedIndex];
				player.trackSpeed = trackSpeed.value;
			});

			radioGroup.RegisterValueChangedCallback(e => {
				if (radioGroup != e.target) {
					return;
				}

				if (playerList.selectedIndex == -1) {
					return;
				}

				var player = PlayerManager.players[playerList.selectedIndex];
				switch (e.newValue) {
					case 0:
						player.inputStrategy = new FiveFretInputStrategy(null, false);
						break;
				}
			});
		}

		private void UpdateDeviceList(DropdownField dropdownField) {
			var choices = new List<string>() {
				"Refresh..."
			};

			inputDevices = new();
			foreach (var device in InputSystem.devices) {
				inputDevices.Add(device);
				choices.Add(device.name);
			}

			dropdownField.choices = choices;
		}

		private void UpdateSettingsList(ListView settingsList, int playerId) {
			var player = PlayerManager.players[playerId];

			settingsList.makeItem = () => new Button();
			settingsList.bindItem = (elem, i) => {
				var mapping = player.inputStrategy.GetMappingNames()[i];
				var inputDisplayName = player.inputStrategy
					.GetMappingInputControl(mapping)?.displayName
					?? "None";

				Button button = (Button) elem;
				button.text = $"{mapping}: {inputDisplayName}";

				// Remove old events
				button.clickable = null;

				// Add new events
				button.clicked += () => {
					settingsList.RefreshItem(inputWaitingIndex);

					inputWaitingIndex = i;
					inputWaitingPlayerIndex = playerId;
					inputWaitingMapping = mapping;

					button.text = "Waiting for input...";
				};
			};

			settingsList.itemsSource = player.inputStrategy.GetMappingNames();
			settingsList.RefreshItems();
		}

		private void UpdateInputWaiting() {
			if (inputWaitingIndex == -1) {
				return;
			}

			var player = PlayerManager.players[inputWaitingPlayerIndex];
			var device = player.inputStrategy.inputDevice;

			if (device == null) {
				return;
			}

			foreach (var control in device.allControls) {
				// Skip "any key" (as that would always be detected)
				if (control is AnyKeyControl) {
					continue;
				}

				if (control is not ButtonControl buttonControl) {
					continue;
				}

				if (!buttonControl.wasPressedThisFrame) {
					continue;
				}

				// Set mapping and stop waiting
				player.inputStrategy.SetMappingInputControl(inputWaitingMapping, control);
				editPlayersDocument.rootVisualElement.Q<ListView>("SettingsList").RefreshItem(inputWaitingIndex);
				inputWaitingIndex = -1;
				inputWaitingPlayerIndex = -1;
				inputWaitingMapping = null;
				break;
			}
		}
	}
}