using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;
using YARG.Input;
using YARG.Serialization;

namespace YARG.UI {
	using Enumerate = InputControlExtensions.Enumerate;

	public class AddPlayer : MonoBehaviour {
		private enum State {
			SELECT_DEVICE,
			CONFIGURE,
			BIND,
			RESOLVE
		}

		[Flags]
		private enum AllowedControl {
			NONE = 0x00,

			// Control types
			AXIS = 0x01,
			BUTTON = 0x02,

			// Control attributes
			NOISY = 0x0100,
			SYNTHETIC = 0x0200,

			ALL = AXIS | BUTTON | NOISY | SYNTHETIC
		}

		[SerializeField]
		private GameObject deviceButtonPrefab;
		[SerializeField]
		private GameObject bindingButtonPrefab;

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
		[SerializeField]
		private GameObject bindHeaderContainer;

		private State state = State.SELECT_DEVICE;

		private (InputDevice device, int micIndex)? selectedDevice = null;
		private bool botMode = false;
		private string playerName = null;
		private InputStrategy inputStrategy = null;

		private ControlBinding currentBindUpdate = null;
		private TextMeshProUGUI currentBindText = null;
		private IDisposable currentDeviceListener = null;
		private List<InputControl<float>> bindGroupingList = new();
		private float bindGroupingTimer = 0f;
		private AllowedControl allowedControls = AllowedControl.ALL;

		private const float GROUP_TIME_THRESHOLD = 0.1f;

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
			currentDeviceListener?.Dispose();
			currentDeviceListener = null;
		}

		private void UpdateState(State newState) {
			state = newState;
			subHeader.text = newState switch {
				State.SELECT_DEVICE => "Step 1 - Select Device",
				State.CONFIGURE => "Step 2 - Configure",
				State.BIND => "Step 3 - Bind",
				State.RESOLVE => "More than one control was detected, please select the correct control from the list below.",
				_ => ""
			};
		}

		private void HideAll() {
			selectDeviceContainer.SetActive(false);
			configureContainer.SetActive(false);
			bindContainer.SetActive(false);
			bindHeaderContainer.SetActive(false);
		}

		private void Update() {
			switch (state) {
				case State.BIND: UpdateBind(); break;
			}
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

		private string GetMappingText(ControlBinding binding)
			=> $"<b>{binding.DisplayName}:</b> {inputStrategy.GetMappingInputControl(binding.BindingKey)?.displayName ?? "None"}";

		private string GetDebounceText(long debounce)
			// Clear textbox if new threshold is below the minimum debounce amount
			=> debounce >= ControlBinding.DEBOUNCE_MINIMUM ? debounce.ToString() : null;

		private void StartBind() {
			if (inputStrategy.Mappings.Count < 1 || botMode) {
				DoneBind();
				return;
			}

			var device = selectedDevice?.device;
			if (device == null) {
				Debug.Assert(false, "No device selected when binding!");
				return;
			}

			HideAll();
			bindContainer.SetActive(true);
			bindHeaderContainer.SetActive(true);
			UpdateState(State.BIND);

			// Destroy old bindings
			foreach (Transform t in bindingsContainer) {
				Destroy(t.gameObject);
			}

			// Add bindings
			foreach (var binding in inputStrategy.Mappings.Values) {
				var button = Instantiate(bindingButtonPrefab, bindingsContainer);

				var text = button.GetComponentInChildren<TextMeshProUGUI>();
				text.text = GetMappingText(binding);

				button.GetComponentInChildren<Button>().onClick.AddListener(() => {
					if (currentBindUpdate == null) {
						currentBindUpdate = binding;
						currentBindText = text;
						text.text = $"<b>{binding.DisplayName}:</b> Waiting for input... (Escape to cancel)";
					}
				});

				var inputField = button.GetComponentInChildren<TMP_InputField>();
				inputField.text = GetDebounceText(binding.DebounceThreshold);
				inputField.onEndEdit.AddListener((text) => {
					// Default to existing threshold if none specified
					if (!long.TryParse(text, out long debounce)) {
						debounce = binding.DebounceThreshold;
					}

					binding.DebounceThreshold = debounce;
					inputField.text = GetDebounceText(binding.DebounceThreshold);
				});
			}

			// Listen for device events
			currentDeviceListener ??= InputSystem.onEvent.Call((eventPtr) => {
				if (currentBindUpdate == null) {
					return;
				}

				// Only take state events
				if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) {
					return;
				}

				// Ignore if not from the selected device
				if (eventPtr.deviceId != device.deviceId) {
					// Check if cancelling
					if (eventPtr.deviceId == Keyboard.current.deviceId) {
						var esc = Keyboard.current.escapeKey;
						if (esc.IsValueConsideredPressed(esc.ReadValueFromEvent(eventPtr))) {
							CancelBind();
						}
					}
					return;
				}

				// Handle cancelling
				if (device is Keyboard keyboard) {
					var esc = keyboard.escapeKey;
					if (esc.IsValueConsideredPressed(esc.ReadValueFromEvent(eventPtr))) {
						CancelBind();
						return;
					}
				}

				// Find all active float-returning controls
				//       Only controls that have changed | Constantly-changing controls like accelerometers | Non-physical controls like stick up/down/left/right
				var flags = Enumerate.IgnoreControlsInCurrentState | Enumerate.IncludeNoisyControls | Enumerate.IncludeSyntheticControls;
				var activeControls = from control in eventPtr.EnumerateControls(flags, device)
					where ControlAllowedAndActive(control, eventPtr)
					select control as InputControl<float>;

				if (activeControls != null) {
					foreach (var ctrl in activeControls) {
						if (!bindGroupingList.Contains(ctrl)) {
							bindGroupingList.Add(ctrl);
						}
					}

					if (bindGroupingTimer <= 0f) {
						bindGroupingTimer = GROUP_TIME_THRESHOLD;
					}
				}
			});
		}

		private void UpdateBind() {
			if (bindGroupingTimer <= 0f) {
				// Timer is inactive
				return;
			}

			// Decrement timer
			bindGroupingTimer -= Time.deltaTime;
			if (bindGroupingTimer > 0f) {
				// Timer is still active
				return;
			}

			// Check number of controls
			int controlCount = bindGroupingList.Count;
			if (controlCount < 1) {
				// No controls active
				return;
			} else if (controlCount > 1) {
				// More than one control active, prompt user to pick which one
				StartResolve(bindGroupingList);
				return;
			}

			// Set mapping
			SetBind(bindGroupingList[0]);
		}

		private bool IsControlAllowed(AllowedControl flag)
			=> (allowedControls & flag) != 0;

		private void AllowedControlChanged(bool enabled, AllowedControl flag) {
			if (enabled) {
				allowedControls |= flag;
			} else {
				allowedControls &= ~flag;
			}
		}

		public void ButtonAllowedChanged(bool enabled)
			=> AllowedControlChanged(enabled, AllowedControl.BUTTON);

		public void AxisAllowedChanged(bool enabled)
			=> AllowedControlChanged(enabled, AllowedControl.AXIS);

		public void NoisyAllowedChanged(bool enabled)
			=> AllowedControlChanged(enabled, AllowedControl.NOISY);

		public void SyntheticAllowedChanged(bool enabled)
			=> AllowedControlChanged(enabled, AllowedControl.SYNTHETIC);

		private bool ControlAllowedAndActive(InputControl control, InputEventPtr eventPtr) {
			// AnyKeyControl is excluded as it would always be active
			if (control is not InputControl<float> floatControl || floatControl is AnyKeyControl) {
				return false;
			}

			// Ensure control is pressed
			if (!InputStrategy.IsControlPressed(floatControl, eventPtr)) {
				return false;
			}

			// Check that the control is allowed
			if ((control.noisy && !IsControlAllowed(AllowedControl.NOISY)) ||
				(control.synthetic && !IsControlAllowed(AllowedControl.SYNTHETIC)) ||
				(control is ButtonControl && !IsControlAllowed(AllowedControl.BUTTON)) ||
				(control is AxisControl && !IsControlAllowed(AllowedControl.AXIS))) {
				return false;
			}

			return true;
		}

		private void SetBind(InputControl<float> control) {
			inputStrategy.SetMappingInputControl(currentBindUpdate.BindingKey, control);
			CancelBind();
		}

		private void CancelBind() {
			currentBindText.text = GetMappingText(currentBindUpdate);
			currentBindText = null;
			currentBindUpdate = null;

			bindGroupingList.Clear();
			bindGroupingTimer = 0f;
		}

		public void DoneBind() {
			// Stop event listener
			currentDeviceListener?.Dispose();
			currentDeviceListener = null;

			// Save bindings
			if (inputStrategy.InputDevice != null) {
				InputBindSerializer.SaveBindsFromInputStrategy(inputStrategy);
			}

			// Create and add player
			var player = new PlayerManager.Player() {
				inputStrategy = inputStrategy
			};
			player.inputStrategy.Enable();
			PlayerManager.players.Add(player);

			// Set name
			if (!string.IsNullOrEmpty(playerName)) {
				player.name = playerName;
			}

			MainMenu.Instance.ShowEditPlayers();
		}

		private void StartResolve(List<InputControl<float>> controls) {
			if (controls.Count < 2) {
				Debug.LogError("No control resolution required but resolution was started!");
				return;
			}

			// Stop event listener
			currentDeviceListener?.Dispose();
			currentDeviceListener = null;

			HideAll();
			bindContainer.SetActive(true);
			UpdateState(State.RESOLVE);

			// Destroy old bindings
			foreach (Transform t in bindingsContainer) {
				Destroy(t.gameObject);
			}

			// List controls
			foreach (var control in controls) {
				var button = Instantiate(deviceButtonPrefab, bindingsContainer);
				var text = button.GetComponentInChildren<TextMeshProUGUI>();
				text.text = control.displayName;

				button.GetComponentInChildren<Button>().onClick.AddListener(() => {
					if (currentBindUpdate == null) {
						return;
					}

					// Set mapping
					SetBind(control);
					StartBind();
				});
			}
		}
	}
}