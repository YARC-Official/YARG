using System;
using System.Collections.Generic;
using Cysharp.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Menu.Persistent;
using YARG.Player;
using YARG.Settings;

namespace YARG.Input
{
    using Enumerate = InputControlExtensions.Enumerate;

    public delegate void MenuInputEvent(YargPlayer player, ref GameInput input);

    public static class InputManager
    {
        public const Enumerate DEFAULT_CONTROL_ENUMERATION_FLAGS =
            Enumerate.IgnoreControlsInCurrentState | // Only controls that have changed
            Enumerate.IncludeNoisyControls |         // Constantly-changing controls like accelerometers
            Enumerate.IncludeSyntheticControls;      // Non-physical controls like stick up/down/left/right

        public static event Action<InputDevice> DeviceAdded;
        public static event Action<InputDevice> DeviceRemoved;

        public static event MenuInputEvent MenuInput;

        private static double _latestInputTime;

        /// <summary>
        /// The time to be used for gameplay input updates.
        /// </summary>
        /// <remarks>
        /// This time is the later time between the time marked in <see cref="InputSystem.onBeforeUpdate"/>
        /// and the time of the most recent input event, such that any input events that happen after an
        /// update starts are factored into input updates.
        /// </remarks>
        public static double InputUpdateTime { get; private set; }

        /// <summary>
        /// The instantaneous current time of the input system.
        /// </summary>
        public static double CurrentInputTime => InputState.currentTime;

        private static HashSet<InputDevice> _seenDevices = new();
        private static HashSet<InputDevice> _disabledDevices = new();

        // We do this song and dance of tracking focus changes manually rather than setting
        // InputSettings.backgroundBehavior to IgnoreFocus, so that input is still (largely) disabled when unfocused
        // but devices are not removed only to be re-added when coming back into focus
        private static bool _gameFocused;
        private static bool _focusChanged;
        private static HashSet<InputDevice> _backgroundDisabledDevices = new();

        public static void Initialize()
        {
            InputSystem.pollingFrequency = SettingsManager.Settings.InputPollingFrequency.Value;

            InputSystem.onEvent += OnEvent;

            InputSystem.onBeforeUpdate += OnBeforeUpdate;
            InputSystem.onAfterUpdate += OnAfterUpdate;

            _gameFocused = Application.isFocused;
            Application.focusChanged += OnFocusChange;
            InputSystem.onDeviceChange += OnDeviceChange;

            // Notify of all current devices
            ToastManager.ToastInformation("Devices found: " + (Microphone.devices.Length + InputSystem.devices.Count));
            foreach (var device in InputSystem.devices)
            {
                if (!device.enabled)
                {
                    _disabledDevices.Add(device);
                    continue;
                }

                DeviceAdded?.Invoke(device);
            }

            // Register formatter for device descriptions, we want them to output as JSON in logs
            Utf16ValueStringBuilder.RegisterTryFormat(
                (InputDeviceDescription value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                {
                    string json = value.ToJson();
                    charsWritten = json.Length;
                    return json.AsSpan().TryCopyTo(destination);
                }
            );
        }

        public static void Destroy()
        {
            InputSystem.onEvent -= OnEvent;

            InputSystem.onBeforeUpdate -= OnBeforeUpdate;
            InputSystem.onAfterUpdate -= OnAfterUpdate;

            InputSystem.onDeviceChange -= OnDeviceChange;
        }

        public static void RegisterPlayer(YargPlayer player)
        {
            player.MenuInput += OnMenuInput;
        }

        public static void UnregisterPlayer(YargPlayer player)
        {
            player.MenuInput -= OnMenuInput;
        }

        private static void OnMenuInput(YargPlayer player, ref GameInput input)
        {
            MenuInput?.Invoke(player, ref input);
        }

        private static void OnBeforeUpdate()
        {
            InputUpdateTime = CurrentInputTime;
        }

        private static void OnAfterUpdate()
        {
            InputUpdateTime = CurrentInputTime;

            if (InputUpdateTime < _latestInputTime)
            {
                YargLogger.LogFormatError(
                    "The last input event for this update is in the future! After-update time: {0}, last input time: {1}",
                    InputUpdateTime, _latestInputTime
                );
            }

            // Update bindings using the input update time
            using (var players = PlayerContainer.PlayerEnumerator)
            {
                while (players.MoveNext())
                {
                    players.Current.Bindings.UpdateBindingsForFrame(InputUpdateTime);
                }
            }

            // Remove any devices that happened to be actually disabled
            // right as the game focus changed
            if (_focusChanged && _gameFocused)
            {
                foreach (var device in _backgroundDisabledDevices)
                {
                    YargLogger.LogFormatDebug("Device disabled: {0}", device.displayName);
                    DeviceRemoved?.Invoke(device);
                }
            }

            _focusChanged = false;
        }

        // For input time handling/debugging
        private static void OnEvent(InputEventPtr eventPtr, InputDevice device)
        {
            double currentTime = CurrentInputTime;

            // Only check state events
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
            {
                return;
            }

            // Keep track of the latest input event
            if (eventPtr.time > _latestInputTime)
            {
                _latestInputTime = eventPtr.time;
            }

            // Rare edge-case, but the input system very much allows this
            if (eventPtr.time > currentTime)
            {
                YargLogger.LogFormatError(
                    "An input event is in the future!\nCurrent time: {0}, event time: {1}, device: {2}",
                    currentTime, eventPtr.time, device);
            }

// Leaving these for posterity
#if false
            // This check is handled by the engine
            // It can still happen on occasion despite the fixes we've made to prevent it
            if (eventPtr.time < InputUpdateTime)
                YargLogger.LogFormatError("An input event caused time to go backwards!\nInput update time: {0}, event time: {1}, current time: {2}, device: {3}",
                    InputUpdateTime, eventPtr.time, currentTime, device);

            // This check happens much too often for it to be of any use
            if (eventPtr.time < _afterUpdateTime)
                YargLogger.LogFormatWarning("An input event was missed in the previous update!\nPrevious update time: {0}, event time: {1}, current time: {2}, device: {3}",
                    _afterUpdateTime, eventPtr.time, currentTime, device);

            // The engine also has this check
            // I've only seen this happen when:
            // - starting the game
            // - re-focusing the window
            // - transitioning to the score screen
            if (_previousEventTimes.TryGetValue(device, out double previousTime))
            {
                if (eventPtr.time < previousTime)
                    YargLogger.LogFormatWarning("An input event is out of order!\nPrevious event time: {0}, incoming event time: {1}, current time: {2}, device: {3}",
                        previousTime, eventPtr.time, currentTime, device);
            }
            _previousEventTimes[device] = eventPtr.time;
#endif
        }

        private static void OnFocusChange(bool focused)
        {
            _gameFocused = focused;
            _focusChanged = true;
        }

        private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            switch (change)
            {
                case InputDeviceChange.Added:
                {
                    if (SettingsManager.Settings.InputDeviceLogging.Value)
                    {
                        YargLogger.LogFormatInfo("Device added: {0}\nDescription:\n{1}\n", device.displayName, device.description);
                    }

                    // Don't toast if the device disabled itself
                    if (device.enabled)
                    {
                        ToastManager.ToastMessage($"Device added: {device.displayName}");
                        DeviceAdded?.Invoke(device);
                    }
                    else
                    {
                        _disabledDevices.Add(device);
                    }

                    _seenDevices.Add(device);
                    break;
                }
                case InputDeviceChange.Removed:
                {
                    YargLogger.LogFormatDebug("Device removed: {0}", device.displayName);

                    // Don't toast for disabled devices
                    if (!_disabledDevices.Remove(device))
                    {
                        ToastManager.ToastMessage($"Device removed: {device.displayName}");
                        DeviceRemoved?.Invoke(device);
                    }

                    _seenDevices.Remove(device);
                    break;
                }
                // case InputDeviceChange.Reconnected: // Fired alongside Added, not needed
                case InputDeviceChange.Enabled:
                {
                    // Devices are enabled when gaining window focus,
                    // but we don't want to add devices when this happens
                    if (_focusChanged || Application.isFocused != _gameFocused)
                    {
                        _focusChanged = true;
                        // Keep track of which devices were re-enabled, so any devices that actually get
                        // disabled can be removed after coming back into focus in OnAfterUpdate
                        _backgroundDisabledDevices.Remove(device);
                        return;
                    }

                    YargLogger.LogFormatDebug("Device enabled: {0}", device.displayName);
                    if (_disabledDevices.Remove(device))
                    {
                        // Toast as a newly-added device, for simplicity to the user
                        ToastManager.ToastMessage($"Device added: {device.displayName}");
                        DeviceAdded?.Invoke(device);
                    }

                    _seenDevices.Add(device);
                    break;
                }
                // case InputDeviceChange.Disconnected: // Fired alongside Removed, not needed
                case InputDeviceChange.Disabled:
                {
                    // Devices are disabled when losing window focus,
                    // but we don't want to remove devices when this happens
                    if (_focusChanged || Application.isFocused != _gameFocused)
                    {
                        _focusChanged = true;
                        // Keep track of disabled devices so any devices that actually get
                        // disabled can be removed after coming back into focus in OnAfterUpdate
                        _backgroundDisabledDevices.Add(device);
                        return;
                    }

                    YargLogger.LogFormatDebug("Device disabled: {0}", device.displayName);

                    // Only toast if the device was disabled *after* its addition
                    if (!_disabledDevices.Contains(device) && _seenDevices.Contains(device))
                    {
                        // Toast as a removed device, for simplicity to the user
                        ToastManager.ToastMessage($"Device removed: {device.displayName}");
                        DeviceRemoved?.Invoke(device);
                    }

                    _seenDevices.Add(device);
                    _disabledDevices.Add(device);
                    break;
                }
            }
        }
    }
}