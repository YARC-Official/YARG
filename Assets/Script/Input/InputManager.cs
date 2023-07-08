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
    public class InputManager : MonoBehaviour
    {

        public delegate void GameInputEvent(YargPlayer player, GameInput input);

        public static event GameInputEvent OnGameInput;

        // Time reference for when inputs started being tracked
        public static double InputTimeOffset { get; private set; }

        // Input events are timestamped directly in the constructor, so we can use them to get the current time
        public static double CurrentInputTime => new InputEvent(StateEvent.Type, 0, InputDevice.InvalidDeviceId).time;

        public static double CurrentRelativeInputTime => CurrentInputTime - InputTimeOffset;

        private IDisposable _eventListener;

        private void Start()
        {
            _eventListener?.Dispose();
            // InputSystem.onEvent is *not* a C# event, it's a property which is intended to be used with observables
            // In order to unsubscribe from it you *must* keep track of the IDisposable returned at the end
            _eventListener = InputSystem.onEvent.Call(OnEvent);
        }

        private void OnDestroy()
        {
            _eventListener?.Dispose();
            _eventListener = null;
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
            for (int i = 0; i < GlobalVariables.Instance.Players.Count; i++)
            {
                var player = GlobalVariables.Instance.Players[i];

                var profileBinds = BindsContainer.GetBindsForProfile(player.Profile);

                // Profile does not have this device mapped to anything.
                if (!profileBinds.ContainsDevice(device))
                {
                    continue;
                }

                foreach (var control in eventPtr.EnumerateChangedControls())
                {
                    if(profileBinds.GetBindsForDevice(device).ContainsControl(control))
                    {
                        FireGameInput(player, eventPtr, control);
                    }
                }
            }
        }

        private void FireGameInput(YargPlayer player, InputEventPtr eventPtr, InputControl control)
        {
            // Fetch action from the action mappings according to the player's profile and device.
            // just use green for example
            var action = GuitarAction.Green;

            // Some invalid action
            if(action == null)
                return;

            if (control is ButtonControl button)
            {
                float value = button.ReadValueFromEvent(eventPtr);
                bool pressed = button.IsValueConsideredPressed(value);

                // if player gamemode == some instrument
                // create correct input

                double time = eventPtr.time - InputTimeOffset;
                var gameInput = GameInput.Create(time, action, pressed);
                OnGameInput?.Invoke(player, gameInput);
            }
        }
    }
}