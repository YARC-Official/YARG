using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using YARG.Core.Input;
using YARG.Player;

namespace YARG.Input
{
    public sealed class DefaultKeyboardMenuBindings : IDisposable
    {
        private static readonly (MenuAction action, Key key)[] DefaultBindings =
        {
            (MenuAction.Green,  Key.Digit1),
            (MenuAction.Red,    Key.Escape),
            (MenuAction.Red,    Key.Digit2),
            (MenuAction.Green,  Key.Enter),
            (MenuAction.Yellow, Key.Digit3),
            (MenuAction.Blue,   Key.Digit4),
            (MenuAction.Orange, Key.Digit5),
            (MenuAction.Start,  Key.Space),
            (MenuAction.Select, Key.Backspace),
            (MenuAction.Up,     Key.UpArrow),
            (MenuAction.Down,   Key.DownArrow),
            (MenuAction.Left,   Key.LeftArrow),
            (MenuAction.Right,  Key.RightArrow),
        };

        private readonly InputAction[] _inputActions = new InputAction[DefaultBindings.Length];

        public DefaultKeyboardMenuBindings()
        {
            SetupBinds();
            Enable();
        }

        private void SetupBinds()
        {
            for (int i = 0; i < DefaultBindings.Length; i++)
            {
                var (menuAction, key) = DefaultBindings[i];
                var keyPath = key.ToString().Replace("Digit", "");

                var action = new InputAction(
                    name: $"Menu_{menuAction}_{key}",
                    type: InputActionType.Button,
                    binding: $"<Keyboard>/{keyPath}"
                );

                action.performed += _ => InputManager.OnMenuAction(menuAction, true);
                action.canceled += _ => InputManager.OnMenuAction(menuAction, false);
                _inputActions[i] = action;
            }
        }

        public void Dispose()
        {
            foreach (var action in _inputActions)
            {
                action.Disable();
                action.Dispose();
            }
        }

        public void Enable()
        {
            foreach (var action in _inputActions)
            {
                action.Enable();
            }
        }

        public void Disable()
        {
            foreach (var action in _inputActions)
            {
                action.Disable();
            }
        }
    }
}