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
                var phase = ActionType.Performed;
                float val = button.ReadValueFromEvent(eventPtr);

                if (val < 0.5f)
                {
                    phase = ActionType.Cancelled;
                }

                // if player gamemode == some instrument
                // create correct input

                var gameInput = new GameInput((int)action, eventPtr.time, phase);
                OnGameInput?.Invoke(player, gameInput);
            }
        }
    }
}