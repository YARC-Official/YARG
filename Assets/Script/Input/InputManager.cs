using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using YARG.Core.Input;
using YARG.Player;

namespace YARG.Input
{
    public class InputManager : MonoBehaviour
    {

        public delegate void GameInputEvent(YargPlayer player, GameInput input);

        public static event GameInputEvent OnGameInput;
        
        private double _inputStartTime; // Time reference for when inputs started being tracked

        private void Start()
        {
            InputSystem.onEvent -= OnEvent;

            InputSystem.onEvent += OnEvent;
            Debug.Log("Subscribed to InputSystem event");
        }

        private void OnDestroy()
        {
            InputSystem.onEvent -= OnEvent;
        }

        private void OnEvent(InputEventPtr eventPtr, InputDevice device)
        {
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

                double time = eventPtr.time - _inputStartTime;
                var gameInput = GameInput.Create(time, action, pressed);
                OnGameInput?.Invoke(player, gameInput);
            }
        }
    }
}