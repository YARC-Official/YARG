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
using YARG.Audio;
using YARG.Input;
using YARG.Serialization;

namespace YARG.UI
{
    using Enumerate = InputControlExtensions.Enumerate;

    public class AddPlayer : MonoBehaviour
    {
        private enum State
        {
            SelectDevice,
            SelectDeviceForMic,
            Configure,
            Bind,
            Resolve
        }

        private enum StrategyType
        {
            FiveFretGuitar,
            RealGuitar,
            FourLaneDrums,
            FiveLaneDrums,

            // IMPORTANT: Vocals must be last in the list (excluding the count),
            // types following it won't show up or be choosable
            Vocals,

            // Number of available strategies
            Count
        }

        [Flags]
        private enum AllowedControl
        {
            None = 0x00,

            // Control types
            Axis = 0x01,
            Button = 0x02,

            // Control attributes
            Noisy = 0x0100,
            Synthetic = 0x0200,

            All = Axis | Button | Noisy | Synthetic
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

        private State _state = State.SelectDevice;

        private InputDevice _selectedDevice = null;
        private IMicDevice _selectedMic = null;

        private bool _botMode = false;
        private string _playerName = null;
        private InputStrategy _inputStrategy = null;

        private ControlBinding _currentBindUpdate = null;
        private TextMeshProUGUI _currentBindText = null;
        private IDisposable _currentDeviceListener = null;
        private List<InputControl<float>> _bindGroupingList = new();
        private float _bindGroupingTimer = 0f;
        private AllowedControl _allowedControls = AllowedControl.All;

        private const float GROUP_TIME_THRESHOLD = 0.1f;

        private void OnEnable()
        {
            // Set navigation scheme
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Back, "Back", () => { MainMenu.Instance.ShowEditPlayers(); })
            }, true));

            playerNameField.text = null;

            StartSelectDevice();
        }

        private void OnDisable()
        {
            Navigator.Instance.PopScheme();

            playerNameField.text = null;

            _selectedDevice = null;
            _selectedMic = null;

            _botMode = false;
            _inputStrategy = null;
            _playerName = null;

            _currentBindUpdate = null;
            _currentBindText = null;
            _currentDeviceListener?.Dispose();
            _currentDeviceListener = null;
        }

        private void UpdateState(State newState)
        {
            _state = newState;
            subHeader.text = newState switch
            {
                State.SelectDevice       => "Step 1 - Select Device",
                State.SelectDeviceForMic => "Step 1, Part 2 - Select Navigation Device",
                State.Configure          => "Step 2 - Configure",
                State.Bind               => "Step 3 - Bind",
                State.Resolve =>
                    "More than one control was detected, please select the correct control from the list below.",
                _ => ""
            };
        }

        private void HideAll()
        {
            selectDeviceContainer.SetActive(false);
            configureContainer.SetActive(false);
            bindContainer.SetActive(false);
            bindHeaderContainer.SetActive(false);
        }

        private void Update()
        {
            switch (_state)
            {
                case State.Bind:
                    UpdateBind();
                    break;
            }
        }

        private void StartSelectDevice(bool micSelected = false)
        {
            HideAll();
            selectDeviceContainer.SetActive(true);
            UpdateState(micSelected ? State.SelectDeviceForMic : State.SelectDevice);

            // Destroy old devices
            foreach (Transform t in devicesContainer)
            {
                Destroy(t.gameObject);
            }

            if (!micSelected)
            {
                // Add bot button
                var botButton = Instantiate(deviceButtonPrefab, devicesContainer);
                botButton.GetComponentInChildren<TextMeshProUGUI>().text = "Create a <color=#0c7027><b>BOT</b></color>";
                botButton.GetComponentInChildren<Button>().onClick.AddListener(() =>
                {
                    _selectedDevice = null;
                    _selectedMic = null;
                    _botMode = true;
                    StartConfigure();
                });
            }
            else
            {
                // Allow skipping navigation device selection
                var noneButton = Instantiate(deviceButtonPrefab, devicesContainer);
                noneButton.GetComponentInChildren<TextMeshProUGUI>().text = "No device";
                noneButton.GetComponentInChildren<Button>().onClick.AddListener(() =>
                {
                    _selectedDevice = null;
                    StartConfigure();
                });
            }

            // Add devices
            foreach (var device in InputSystem.devices)
            {
                var button = Instantiate(deviceButtonPrefab, devicesContainer);
                button.GetComponentInChildren<TextMeshProUGUI>().text =
                    $"<b>{device.displayName}</b> ({device.deviceId})";
                button.GetComponentInChildren<Button>().onClick.AddListener(() =>
                {
                    _selectedDevice = device;
                    StartConfigure();
                });
            }

            if (micSelected)
            {
                return;
            }

            // Add mics
            var mics = GameManager.AudioManager.GetAllInputDevices();
            foreach (var mic in mics)
            {
                var button = Instantiate(deviceButtonPrefab, devicesContainer);
                var textBox = button.GetComponentInChildren<TextMeshProUGUI>();
                textBox.text = $"(MIC) <b>{mic.DisplayName}</b>";
                if (mic.IsDefault)
                {
                    textBox.text += " <i>(Default)</i>";
                }

                var capture = mic;
                button.GetComponentInChildren<Button>().onClick.AddListener(() =>
                {
                    _selectedMic = capture;
                    StartSelectDevice(micSelected: true);
                });
            }
        }

        private void StartConfigure()
        {
            HideAll();
            configureContainer.SetActive(true);
            UpdateState(State.Configure);

            var options = new List<string>();
            for (StrategyType strategy = 0; strategy < StrategyType.Count; strategy++)
            {
                // Don't display microphone as an option if no mic was selected and we're not in bot mode
                if (strategy == StrategyType.Vocals && _selectedMic == null && !_botMode) break;

                string text = strategy switch
                {
                    StrategyType.FiveFretGuitar => "Five Fret Guitar",
                    StrategyType.RealGuitar     => "Pro Guitar",
                    StrategyType.FourLaneDrums  => "Drums (Standard)",
                    StrategyType.FiveLaneDrums  => "Drums (5-lane)",
                    StrategyType.Vocals         => "Microphone",
                    _                           => throw new Exception("Invalid input strategy type!")
                };
                options.Add(text);
            }

            inputStrategyDropdown.ClearOptions();
            inputStrategyDropdown.AddOptions(options);

            inputStrategyDropdown.value =
                (int) (_selectedMic != null ? StrategyType.Vocals : StrategyType.FiveFretGuitar);
            inputStrategyDropdown.interactable = _selectedMic == null;
        }

        public void DoneConfigure()
        {
            _inputStrategy = (StrategyType) inputStrategyDropdown.value switch
            {
                StrategyType.FiveFretGuitar => new FiveFretInputStrategy(),
                StrategyType.RealGuitar     => new RealGuitarInputStrategy(),
                StrategyType.FourLaneDrums  => new DrumsInputStrategy(),
                StrategyType.FiveLaneDrums  => new GHDrumsInputStrategy(),
                StrategyType.Vocals         => new MicInputStrategy(),
                _                           => throw new Exception("Invalid input strategy type!")
            };

            _inputStrategy.InputDevice = _selectedDevice;
            _inputStrategy.MicDevice = _selectedMic;
            _inputStrategy.BotMode = _botMode;

            _playerName = playerNameField.text;

            // Try to load bindings
            if (_inputStrategy.InputDevice != null)
            {
                InputBindSerializer.LoadBindsFromSave(_inputStrategy);
            }

            StartBind();
        }

        private string GetMappingText(ControlBinding binding) =>
            $"<b>{binding.DisplayName}:</b> {_inputStrategy.GetMappingInputControl(binding.BindingKey)?.displayName ?? "None"}";

        private string GetDebounceText(long debounce)
            // Clear textbox if new threshold is below the minimum debounce amount
            =>
                debounce >= ControlBinding.DEBOUNCE_MINIMUM ? debounce.ToString() : null;

        private void StartBind()
        {
            // Set navigation scheme
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Confirm, "Done", () => { DoneBind(); })
            }, true));

            // Skip if binding is not needed
            if (_inputStrategy.Mappings.Count < 1 || _botMode || _selectedDevice == null)
            {
                DoneBind();
                return;
            }

            HideAll();
            bindContainer.SetActive(true);
            bindHeaderContainer.SetActive(true);
            UpdateState(State.Bind);

            // Destroy old bindings
            foreach (Transform t in bindingsContainer)
            {
                Destroy(t.gameObject);
            }

            // Add bindings
            foreach (var binding in _inputStrategy.Mappings.Values)
            {
                var button = Instantiate(bindingButtonPrefab, bindingsContainer);

                var text = button.GetComponentInChildren<TextMeshProUGUI>();
                text.text = GetMappingText(binding);

                button.GetComponentInChildren<Button>().onClick.AddListener(() =>
                {
                    if (_currentBindUpdate == null)
                    {
                        _currentBindUpdate = binding;
                        _currentBindText = text;
                        text.text = $"<b>{binding.DisplayName}:</b> Waiting for input... (Escape to cancel)";
                    }
                });

                var inputField = button.GetComponentInChildren<TMP_InputField>();
                inputField.text = GetDebounceText(binding.DebounceThreshold);
                inputField.onEndEdit.AddListener((text) =>
                {
                    // Default to existing threshold if none specified
                    if (!long.TryParse(text, out long debounce))
                    {
                        debounce = binding.DebounceThreshold;
                    }

                    binding.DebounceThreshold = debounce;
                    inputField.text = GetDebounceText(binding.DebounceThreshold);
                });
            }

            // Listen for device events
            _currentDeviceListener ??= InputSystem.onEvent.Call((eventPtr) =>
            {
                if (_currentBindUpdate == null)
                {
                    return;
                }

                // Only take state events
                if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
                {
                    return;
                }

                // Ignore if not from the selected device
                if (eventPtr.deviceId != _selectedDevice.deviceId)
                {
                    // Check if cancelling
                    if (eventPtr.deviceId == Keyboard.current.deviceId)
                    {
                        var esc = Keyboard.current.escapeKey;
                        if (esc.IsValueConsideredPressed(esc.ReadValueFromEvent(eventPtr)))
                        {
                            CancelBind();
                        }
                    }

                    return;
                }

                // Handle cancelling
                if (_selectedDevice is Keyboard keyboard)
                {
                    var esc = keyboard.escapeKey;
                    if (esc.IsValueConsideredPressed(esc.ReadValueFromEvent(eventPtr)))
                    {
                        CancelBind();
                        return;
                    }
                }

                // Find all active float-returning controls
                //       Only controls that have changed | Constantly-changing controls like accelerometers | Non-physical controls like stick up/down/left/right
                var flags = Enumerate.IgnoreControlsInCurrentState | Enumerate.IncludeNoisyControls |
                    Enumerate.IncludeSyntheticControls;
                var activeControls = from control in eventPtr.EnumerateControls(flags, _selectedDevice)
                    where ControlAllowedAndActive(control, eventPtr)
                    select control as InputControl<float>;

                if (activeControls != null)
                {
                    foreach (var ctrl in activeControls)
                    {
                        if (!_bindGroupingList.Contains(ctrl))
                        {
                            _bindGroupingList.Add(ctrl);
                        }
                    }

                    if (_bindGroupingTimer <= 0f)
                    {
                        _bindGroupingTimer = GROUP_TIME_THRESHOLD;
                    }
                }
            });
        }

        private void UpdateBind()
        {
            if (_bindGroupingTimer <= 0f)
            {
                // Timer is inactive
                return;
            }

            // Decrement timer
            _bindGroupingTimer -= Time.deltaTime;
            if (_bindGroupingTimer > 0f)
            {
                // Timer is still active
                return;
            }

            // Check number of controls
            int controlCount = _bindGroupingList.Count;
            if (controlCount < 1)
            {
                // No controls active
                return;
            }
            else if (controlCount > 1)
            {
                // More than one control active, prompt user to pick which one
                StartResolve(_bindGroupingList);
                return;
            }

            // Set mapping
            SetBind(_bindGroupingList[0]);
        }

        private bool IsControlAllowed(AllowedControl flag) => (_allowedControls & flag) != 0;

        private void AllowedControlChanged(bool enabled, AllowedControl flag)
        {
            if (enabled)
            {
                _allowedControls |= flag;
            }
            else
            {
                _allowedControls &= ~flag;
            }
        }

        public void ButtonAllowedChanged(bool enabled) => AllowedControlChanged(enabled, AllowedControl.Button);

        public void AxisAllowedChanged(bool enabled) => AllowedControlChanged(enabled, AllowedControl.Axis);

        public void NoisyAllowedChanged(bool enabled) => AllowedControlChanged(enabled, AllowedControl.Noisy);

        public void SyntheticAllowedChanged(bool enabled) => AllowedControlChanged(enabled, AllowedControl.Synthetic);

        private bool ControlAllowedAndActive(InputControl control, InputEventPtr eventPtr)
        {
            // AnyKeyControl is excluded as it would always be active
            if (control is not InputControl<float> floatControl || floatControl is AnyKeyControl)
            {
                return false;
            }

            // Ensure control is pressed
            if (!InputStrategy.IsControlPressed(floatControl, eventPtr))
            {
                return false;
            }

            // Check that the control is allowed
            if ((control.noisy && !IsControlAllowed(AllowedControl.Noisy)) ||
                (control.synthetic && !IsControlAllowed(AllowedControl.Synthetic)) ||
                (control is ButtonControl && !IsControlAllowed(AllowedControl.Button)) ||
                (control is AxisControl && !IsControlAllowed(AllowedControl.Axis)))
            {
                return false;
            }

            return true;
        }

        private void SetBind(InputControl<float> control)
        {
            _inputStrategy.SetMappingInputControl(_currentBindUpdate.BindingKey, control);
            CancelBind();
        }

        private void CancelBind()
        {
            _currentBindText.text = GetMappingText(_currentBindUpdate);
            _currentBindText = null;
            _currentBindUpdate = null;

            _bindGroupingList.Clear();
            _bindGroupingTimer = 0f;
        }

        public void DoneBind()
        {
            Navigator.Instance.PopScheme();

            // Stop event listener
            _currentDeviceListener?.Dispose();
            _currentDeviceListener = null;

            // Save bindings
            if (_inputStrategy.InputDevice != null)
            {
                InputBindSerializer.SaveBindsFromInputStrategy(_inputStrategy);
            }

            // Create and add player
            var player = new PlayerManager.Player()
            {
                inputStrategy = _inputStrategy
            };
            player.inputStrategy.Enable();
            PlayerManager.players.Add(player);

            // Set name
            if (!string.IsNullOrEmpty(_playerName))
            {
                player.name = _playerName;
            }
            else
            {
                player.TryPickRandomName();
            }

            MainMenu.Instance.ShowEditPlayers();
        }

        private void StartResolve(List<InputControl<float>> controls)
        {
            if (controls.Count < 2)
            {
                Debug.LogError("No control resolution required but resolution was started!");
                return;
            }

            // Stop event listener
            _currentDeviceListener?.Dispose();
            _currentDeviceListener = null;

            HideAll();
            bindContainer.SetActive(true);
            UpdateState(State.Resolve);

            // Destroy old bindings
            foreach (Transform t in bindingsContainer)
            {
                Destroy(t.gameObject);
            }

            // List controls
            foreach (var control in controls)
            {
                var button = Instantiate(deviceButtonPrefab, bindingsContainer);
                var text = button.GetComponentInChildren<TextMeshProUGUI>();
                text.text = control.displayName;

                button.GetComponentInChildren<Button>().onClick.AddListener(() =>
                {
                    if (_currentBindUpdate == null)
                    {
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