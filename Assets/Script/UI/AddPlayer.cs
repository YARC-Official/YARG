using System;
using Minis;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;
using YARG.Input;
using YARG.Serialization;

namespace YARG.UI {
	public class AddPlayer : MonoBehaviour {
		private enum State {
			SELECT_DEVICE,
			CONFIGURE,
			BIND
		}

		[SerializeField]
		private GameObject deviceButtonPrefab;

		[Space]
		[SerializeField]
		private TextMeshProUGUI subHeader;
		[SerializeField]
		private Transform devicesContainer;
		[SerializeField]
		private TMP_Dropdown inputStrategyDropdown;
		[SerializeField]
		private Transform bindingsContainer;
		[SerializeField]
		private TMP_InputField playerNameField;

		[Space]
		[SerializeField]
		private GameObject selectDeviceContainer;
		[SerializeField]
		private GameObject configureContainer;
		[SerializeField]
		private GameObject bindContainer;

		private State state = State.SELECT_DEVICE;

		private (InputDevice device, int micIndex)? selectedDevice = null;
		private bool botMode = false;
		private string playerName = null;
		private InputStrategy inputStrategy = null;

		private string currentBindUpdate = null;
		private TextMeshProUGUI currentBindText = null;

		private void OnEnable() {
			playerNameField.text = null;

			StartSelectDevice();
		}

		private void OnDisable() {
			playerNameField.text = null;

			selectedDevice = null;
			botMode = false;
			inputStrategy = null;
			playerName = null;

			currentBindUpdate = null;
			currentBindText = null;
		}

		private void Update() {
			switch (state) {
				case State.BIND:
					UpdateBind();
					break;
				default:
					break;
			}
		}

		private void UpdateState(State newState) {
			state = newState;
			subHeader.text = newState switch {
				State.SELECT_DEVICE => "Step 1 - Select Device",
				State.CONFIGURE => "Step 2 - Configure",
				State.BIND => "Step 3 - Bind",
				_ => ""
			};
		}

		private void HideAll() {
			selectDeviceContainer.SetActive(false);
			configureContainer.SetActive(false);
			bindContainer.SetActive(false);
		}

		private void StartSelectDevice() {
			HideAll();
			selectDeviceContainer.SetActive(true);
			UpdateState(State.SELECT_DEVICE);

			// Destroy old devices
			foreach (Transform t in devicesContainer) {
				Destroy(t.gameObject);
			}

			// Add bot button
			var botButton = Instantiate(deviceButtonPrefab, devicesContainer);
			botButton.GetComponentInChildren<TextMeshProUGUI>().text = "Create a <color=#0c7027><b>BOT</b></color>";
			botButton.GetComponentInChildren<Button>().onClick.AddListener(() => {
				selectedDevice = (null, InputStrategy.INVALID_MIC_INDEX);
				botMode = true;
				StartConfigure();
			});

			// Add devices
			foreach (var device in InputSystem.devices) {
				var button = Instantiate(deviceButtonPrefab, devicesContainer);
				button.GetComponentInChildren<TextMeshProUGUI>().text = $"<b>{device.displayName}</b> ({device.deviceId})";
				button.GetComponentInChildren<Button>().onClick.AddListener(() => {
					selectedDevice = (device, InputStrategy.INVALID_MIC_INDEX);
					StartConfigure();
				});
			}

			// Add mics
			for (int i = 0; i < Microphone.devices.Length; i++) {
				var button = Instantiate(deviceButtonPrefab, devicesContainer);
				button.GetComponentInChildren<TextMeshProUGUI>().text = $"(MIC) <b>{Microphone.devices[i]}</b>";

				int capture = i;
				button.GetComponentInChildren<Button>().onClick.AddListener(() => {
					selectedDevice = (null, capture);
					StartConfigure();
				});
			}
		}

		private void StartConfigure() {
			HideAll();
			configureContainer.SetActive(true);
			UpdateState(State.CONFIGURE);

			if (selectedDevice?.micIndex != InputStrategy.INVALID_MIC_INDEX) {
				// Set to MIC if the selected device is a MIC
				inputStrategyDropdown.value = 1;
			} else {
				inputStrategyDropdown.value = 0;
			}
		}

		public void DoneConfigure() {
			inputStrategy = inputStrategyDropdown.value switch {
				0 => new FiveFretInputStrategy(),
				1 => new MicInputStrategy(),
				2 => new RealGuitarInputStrategy(),
				3 => new DrumsInputStrategy(),
				4 => new GHDrumsInputStrategy(),
				_ => throw new Exception("Invalid input strategy type!")
			};

			if (selectedDevice?.device == null) {
				inputStrategy.InputDevice = null;
				inputStrategy.microphoneIndex = selectedDevice?.micIndex ?? InputStrategy.INVALID_MIC_INDEX;
			} else {
				inputStrategy.InputDevice = selectedDevice?.device;
				inputStrategy.microphoneIndex = InputStrategy.INVALID_MIC_INDEX;
			}

			inputStrategy.botMode = botMode;

			playerName = playerNameField.text;

			// Try to load bindings
			if (inputStrategy.InputDevice != null) {
				InputBindSerializer.LoadBindsFromSave(inputStrategy);
			}

			StartBind();
		}

		private string GetMappingText(string binding)
			=> $"<b>{binding}:</b> {inputStrategy.GetMappingInputControl(binding)?.displayName ?? "None"}";

		private void StartBind() {
			if (inputStrategy.GetMappingNames().Length < 1 || botMode) {
				DoneBind();
				return;
			}

			HideAll();
			bindContainer.SetActive(true);
			UpdateState(State.BIND);

			// Destroy old bindings
			foreach (Transform t in bindingsContainer) {
				Destroy(t.gameObject);
			}

			// Add bindings
			foreach (var binding in inputStrategy.GetMappingNames()) {
				var button = Instantiate(deviceButtonPrefab, bindingsContainer);

				var text = button.GetComponentInChildren<TextMeshProUGUI>();
				text.text = GetMappingText(binding);

				button.GetComponentInChildren<Button>().onClick.AddListener(() => {
					if (currentBindUpdate == null) {
						currentBindUpdate = binding;
						currentBindText = text;
						text.text = $"<b>{binding}:</b> Waiting for input... (Escape to cancel)";
					}
				});
			}

			// Temp for MIDI
			if (inputStrategy.InputDevice is MidiDevice midiDevice) {
				midiDevice.onWillNoteOn += OnNote;
			}
		}

		// Workaround to avoid very short note events not registering correctly
		private void OnNote(MidiNoteControl control, float velocity) {
			if (currentBindUpdate == null) {
				return;
			}

			// Set mapping and stop waiting
			inputStrategy.SetMappingInputControl(currentBindUpdate, control);
			currentBindUpdate = null;

			// Refresh
			DoneBind();
		}

		private void UpdateBind() {
			if (currentBindUpdate == null) {
				return;
			}

			// Cancel
			if (Keyboard.current.escapeKey.wasPressedThisFrame) {
				currentBindText.text = GetMappingText(currentBindUpdate);
				currentBindText = null;
				currentBindUpdate = null;
				return;
			}

			if (inputStrategy.InputDevice is not MidiDevice) { // Temp for MIDI
				foreach (var control in selectedDevice?.device.allControls) {
					// Skip "any key" (as that would always be detected)
					if (control is AnyKeyControl) {
						continue;
					}

					if (control is not ButtonControl buttonControl || !buttonControl.wasPressedThisFrame) {
						continue;
					}

					// Set mapping and update text
					inputStrategy.SetMappingInputControl(currentBindUpdate, control);
					currentBindText.text = GetMappingText(currentBindUpdate);

					// Stop waiting
					currentBindUpdate = null;
					currentBindText = null;
					break;
				}
			}
		}

		public void DoneBind() {
			// Temp for MIDI
			if (inputStrategy.InputDevice is MidiDevice midiDevice) {
				midiDevice.onWillNoteOn -= OnNote;
			}

			// Save bindings
			if (inputStrategy.InputDevice != null) {
				InputBindSerializer.SaveBindsFromInputStrategy(inputStrategy);
			}

			// Create and add player
			var player = new PlayerManager.Player() {
				inputStrategy = inputStrategy
			};
			PlayerManager.players.Add(player);

			// Set name
			if (!string.IsNullOrEmpty(playerName)) {
				player.name = playerName;
			}

			MainMenu.Instance.ShowEditPlayers();
		}
	}
}