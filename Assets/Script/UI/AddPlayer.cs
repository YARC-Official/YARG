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

		private (InputDevice, int)? selectedDevice;
		private bool botMode;
		private string playerName;
		private InputStrategy inputStrategy;

		private string currentBindUpdate;

		private void OnEnable() {
			selectedDevice = null;
			botMode = false;
			inputStrategy = null;
			playerName = null;

			currentBindUpdate = null;

			playerNameField.text = null;

			UpdateSelectDevice();
		}

		private void OnDisable() {
			currentBindUpdate = null;
		}

		private void Update() {
			if (state == State.BIND && currentBindUpdate != null) {

				// Cancel
				if (Keyboard.current.escapeKey.wasPressedThisFrame) {
					currentBindUpdate = null;
					UpdateBind();
					return;
				}

				if (inputStrategy.InputDevice is not MidiDevice) {
					foreach (var control in selectedDevice?.Item1.allControls) {
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
						inputStrategy.SetMappingInputControl(currentBindUpdate, control);
						currentBindUpdate = null;

						// Refresh
						UpdateBind();
						break;
					}
				}
			}
		}

		private void UpdateSubHeader() {
			subHeader.text = state switch {
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

		private void UpdateSelectDevice() {
			HideAll();
			selectDeviceContainer.SetActive(true);

			state = State.SELECT_DEVICE;
			UpdateSubHeader();

			// Destroy old devices
			foreach (Transform t in devicesContainer) {
				Destroy(t.gameObject);
			}

			// Add bot button
			var botGo = Instantiate(deviceButtonPrefab, devicesContainer);
			botGo.GetComponentInChildren<TextMeshProUGUI>().text = "Create a <color=#0c7027><b>BOT</b></color>";
			botGo.GetComponentInChildren<Button>().onClick.AddListener(() => {
				selectedDevice = (null, -1);
				botMode = true;
				UpdateConfigure();
			});

			// Add devices
			foreach (var device in InputSystem.devices) {
				var go = Instantiate(deviceButtonPrefab, devicesContainer);
				go.GetComponentInChildren<TextMeshProUGUI>().text = $"<b>{device.displayName}</b> ({device.deviceId})";
				go.GetComponentInChildren<Button>().onClick.AddListener(() => {
					selectedDevice = (device, -1);
					UpdateConfigure();
				});
			}

			// Add mics
			for (int i = 0; i < Microphone.devices.Length; i++) {
				var go = Instantiate(deviceButtonPrefab, devicesContainer);
				go.GetComponentInChildren<TextMeshProUGUI>().text = $"(MIC) <b>{Microphone.devices[i]}</b>";

				int capture = i;
				go.GetComponentInChildren<Button>().onClick.AddListener(() => {
					selectedDevice = (null, capture);
					UpdateConfigure();
				});
			}
		}

		private void UpdateConfigure() {
			HideAll();
			configureContainer.SetActive(true);

			state = State.CONFIGURE;
			UpdateSubHeader();

			if (selectedDevice?.Item2 != -1) {
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
				_ => throw new System.Exception("Unreachable.")
			};

			if (selectedDevice?.Item1 == null) {
				inputStrategy.InputDevice = null;
				inputStrategy.microphoneIndex = selectedDevice?.Item2 ?? -1;
			} else {
				inputStrategy.InputDevice = selectedDevice?.Item1;
				inputStrategy.microphoneIndex = -1;
			}

			inputStrategy.botMode = botMode;

			playerName = playerNameField.text;

			if (inputStrategy.InputDevice is MidiDevice midiDevice) {
				midiDevice.onWillNoteOn += OnNote;
			}

			// Try to load bindings
			if (inputStrategy.InputDevice != null) {
				InputBindSerializer.LoadBindsFromSave(inputStrategy);
			}

			UpdateBind();
		}

		private void UpdateBind() {
			if (inputStrategy.GetMappingNames().Length <= 0 || botMode) {
				DoneBind();
				return;
			}

			HideAll();
			bindContainer.SetActive(true);

			state = State.BIND;
			UpdateSubHeader();

			// Destroy old bindings
			foreach (Transform t in bindingsContainer) {
				Destroy(t.gameObject);
			}

			// Add bindings
			foreach (var binding in inputStrategy.GetMappingNames()) {
				var go = Instantiate(deviceButtonPrefab, bindingsContainer);

				var text = go.GetComponentInChildren<TextMeshProUGUI>();
				text.text = $"<b>{binding}:</b> {inputStrategy.GetMappingInputControl(binding)?.displayName ?? "None"}";

				go.GetComponentInChildren<Button>().onClick.AddListener(() => {
					if (currentBindUpdate == null) {
						currentBindUpdate = binding;
						text.text = $"<b>{binding}:</b> Waiting for input... (Escape to cancel)";
					}
				});
			}
		}

		private void OnNote(MidiNoteControl control, float v) {
			if (currentBindUpdate == null) {
				return;
			}

			// Set mapping and stop waiting
			inputStrategy.SetMappingInputControl(currentBindUpdate, control);
			currentBindUpdate = null;

			// Refresh
			UpdateBind();
		}

		public void DoneBind() {
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