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

        public delegate void GameInputEvent<TAction>(YargPlayer player, GameInput<TAction> action) where TAction : Enum;

        public static event GameInputEvent<Enum> OnGameInput;

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

                // If device paired to current player's profile in some way...
                // just example code don't treat this as exact.
                if (InputMappings.GetMapForProfile(player.Profile).Contains(device))
                {
                    foreach (var control in eventPtr.EnumerateChangedControls())
                    {
                        if(control in mappingsForPlayer)
                        {
                            FireGameInput(player, eventPtr, control);
                        }
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

                var gameInput = new GuitarInput(action, (float)eventPtr.time, phase);
                OnGameInput?.Invoke(player, gameInput);
            }
        }
    }
}