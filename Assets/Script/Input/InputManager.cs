using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using YARG.Core.Input;
using YARG.Player;

namespace YARG.Input
{
    using Enumerate = InputControlExtensions.Enumerate;

    public class InputManager : MonoBehaviour
    {
        public const Enumerate DEFAULT_CONTROL_ENUMERATION_FLAGS =
            Enumerate.IgnoreControlsInCurrentState | // Only controls that have changed
            Enumerate.IncludeNoisyControls |         // Constantly-changing controls like accelerometers
            Enumerate.IncludeSyntheticControls;      // Non-physical controls like stick up/down/left/right

        public delegate void GameInputEvent(YargPlayer player, GameInput input);

        public static event GameInputEvent OnGameInput;

        public static double InputUpdateTime { get; private set; }

        // Time reference for when inputs started being tracked
        public static double InputTimeOffset { get; set; }

        // Input events are timestamped directly in the constructor, so we can use them to get the current time
        public static double CurrentInputTime => new InputEvent(StateEvent.Type, 0, InputDevice.InvalidDeviceId).time;

        private IDisposable _onEventListener;

        private void Start()
        {
            _onEventListener?.Dispose();
            // InputSystem.onEvent is *not* a C# event, it's a property which is intended to be used with observables
            // In order to unsubscribe from it you *must* keep track of the IDisposable returned at the end
            _onEventListener = InputSystem.onEvent.Call(OnEvent);

            InputSystem.onAfterUpdate += OnAfterUpdate;
        }

        private void OnDestroy()
        {
            _onEventListener?.Dispose();
            _onEventListener = null;

            InputSystem.onAfterUpdate -= OnAfterUpdate;
        }

        public static double GetRelativeTime(double timeFromInputSystem)
        {
            return timeFromInputSystem - InputTimeOffset;
        }

        private void OnAfterUpdate()
        {
            InputUpdateTime = CurrentInputTime - InputTimeOffset;
        }

        private void OnEvent(InputEventPtr eventPtr)
        {
            // Only take state events
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
            {
                return;
            }

            var device = InputSystem.GetDeviceById(eventPtr.deviceId);
            foreach (var player in PlayerContainer.Players)
            {
                // TODO: Bindings don't have anything subscribed to their input event yet
                var profileBinds = player.Bindings;

                var deviceBinds = profileBinds.TryGetBindsForDevice(device);
                if (deviceBinds == null) continue;
                deviceBinds.ProcessInputEvent(eventPtr);
            }
        }
    }
}