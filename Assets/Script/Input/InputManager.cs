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

        public static double BeforeUpdateTime { get; private set; }

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

            InputSystem.onBeforeUpdate += () =>
            {
                var timestampEvent = new InputEvent(StateEvent.Type, 0, InputDevice.InvalidDeviceId);
                BeforeUpdateTime = timestampEvent.time - InputTimeOffset;
            };
        }

        private void OnDestroy()
        {
            _onEventListener?.Dispose();
            _onEventListener = null;
        }

        public static double GetRelativeTime(double timeFromInputSystem)
        {
            return timeFromInputSystem - InputTimeOffset;
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
                foreach (var control in eventPtr.EnumerateChangedControls())
                {
                    FireGameInput(player, eventPtr, control);
                }

                return;

                // TODO: Bindings don't have anything subscribed to their input event yet
                var profileBinds = player.Bindings;

                var deviceBinds = profileBinds.TryGetBindsForDevice(device);
                if (deviceBinds == null) continue;

                var gameModeBinds = deviceBinds.GetBindingsForGameMode(player.Profile.InstrumentType);
                gameModeBinds.ProcessInputEvent(eventPtr);
            }
        }

        private void FireGameInput(YargPlayer player, InputEventPtr eventPtr, InputControl control)
        {
            if (control is ButtonControl button)
            {
                float value = button.ReadValueFromEvent(eventPtr);

                GuitarAction action;
                switch (button.name)
                {
                    case "greenFret":
                        action = GuitarAction.Green;
                        break;
                    case "redFret":
                        action = GuitarAction.Red;
                        break;
                    case "yellowFret":
                        action = GuitarAction.Yellow;
                        break;
                    case "blueFret":
                        action = GuitarAction.Blue;
                        break;
                    case "orangeFret":
                        action = GuitarAction.Orange;
                        break;
                    case "strumDown":
                        action = GuitarAction.StrumDown;
                        break;
                    case "strumUp":
                        action = GuitarAction.StrumUp;
                        break;
                    case "selectButton":
                        action = GuitarAction.StarPower;
                        break;
                    default:
                        return;
                }

                double time = eventPtr.time - InputTimeOffset;
                var gameInput = new GameInput(time, (int) action, button.IsValueConsideredPressed(value));
                OnGameInput?.Invoke(player, gameInput);
            }
        }
    }
}