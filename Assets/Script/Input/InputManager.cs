using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using YARG.Core.Input;
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

        private static double _beforeUpdateTime;
        private static double _afterUpdateTime;
        private static double _latestInputTime;

        /// <summary>
        /// The current time as of when the input system finished updating.
        /// </summary>
        /// <seealso cref="InputSystem.onAfterUpdate"/>
        public static double GameUpdateTime => _afterUpdateTime;

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

        private static IDisposable _onEventListener;

        public static void Initialize()
        {
            // High polling rate
            // TODO: Allow configuring this?
            InputSystem.pollingFrequency = 500f;

            _onEventListener?.Dispose();
            // InputSystem.onEvent is *not* a C# event, it's a property which is intended to be used with observables
            // In order to unsubscribe from it you *must* keep track of the IDisposable returned at the end
            _onEventListener = InputSystem.onEvent.Call(OnEvent);

            InputSystem.onBeforeUpdate += OnBeforeUpdate;
            InputSystem.onAfterUpdate += OnAfterUpdate;

            InputSystem.onDeviceChange += OnDeviceChange;

            // Notify of all current devices
            ToastManager.ToastInformation("Devices found: " + (Microphone.devices.Length + InputSystem.devices.Count));
            foreach (var device in InputSystem.devices)
            {
                DeviceAdded?.Invoke(device);
            }
        }

        public static void Destroy()
        {
            _onEventListener?.Dispose();
            _onEventListener = null;

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
            // Update bindings first, so that any inputs generated there
            // (e.g. button debounce) occur before the update time
            foreach (var player in PlayerContainer.Players)
            {
                var profileBinds = player.Bindings;
                profileBinds.UpdateBindingsForFrame();
            }

            _beforeUpdateTime = CurrentInputTime;
        }

        private static void OnAfterUpdate()
        {
            _afterUpdateTime = CurrentInputTime;
            InputUpdateTime = Math.Max(_beforeUpdateTime, _latestInputTime);
        }

        private static void OnEvent(InputEventPtr eventPtr)
        {
            // Only take state events
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
                return;

            // Keep track of the latest input event
            if (eventPtr.time > _latestInputTime)
                _latestInputTime = eventPtr.time;

            var device = InputSystem.GetDeviceById(eventPtr.deviceId);
            foreach (var player in PlayerContainer.Players)
            {
                var profileBinds = player.Bindings;
                if (!profileBinds.ContainsDevice(device))
                    continue;

                profileBinds.ProcessInputEvent(eventPtr);
                break;
            }
        }

        private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            switch (change)
            {
                case InputDeviceChange.Added:
                // case InputDeviceChange.Enabled: // Devices are enabled/disabled when gaining/losing window focus
                // case InputDeviceChange.Reconnected: // Fired alongside Added, not needed
                    ToastManager.ToastMessage($"Device added: {device.displayName}");
                    if (SettingsManager.Settings.InputDeviceLogging.Data)
                        Debug.Log($"Device added: {device.displayName}\nDescription:\n{device.description.ToJson()}\n");
                    DeviceAdded?.Invoke(device);
                    break;

                case InputDeviceChange.Removed:
                // case InputDeviceChange.Disabled: // Devices are enabled/disabled when gaining/losing window focus
                // case InputDeviceChange.Disconnected: // Fired alongside Removed, not needed
                    ToastManager.ToastMessage($"Device removed: {device.displayName}");
                    DeviceRemoved?.Invoke(device);
                    break;
            }
        }
    }
}