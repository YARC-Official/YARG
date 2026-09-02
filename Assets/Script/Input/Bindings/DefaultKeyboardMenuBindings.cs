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
            (MenuAction.Search, Key.Tab),
            (MenuAction.SelectArtist, Key.F1),
            (MenuAction.Up,     Key.UpArrow),
            (MenuAction.Down,   Key.DownArrow),
            (MenuAction.Left,   Key.LeftArrow),
            (MenuAction.Right,  Key.RightArrow),
        };

        private readonly InputAction[] _inputActions = new InputAction[DefaultBindings.Length];

        private static bool HasConnectedKeyboardProfile => PlayerContainer.HasConnectedKeyboardProfile();

        public DefaultKeyboardMenuBindings()
        {
            SetupBinds();
        }

        private void SetupBinds()
        {
            for (int i = 0; i < DefaultBindings.Length; i++)
            {
                var (menuAction, key) = DefaultBindings[i];
                var action = GetInputAction(menuAction, key);
                action.performed += _ =>
                {
                    if (HasConnectedKeyboardProfile)
                    {
                        return;
                    }

                    InputManager.OnMenuAction(menuAction, true);
                };
                action.canceled += _ =>
                {
                    if (HasConnectedKeyboardProfile)
                    {
                        return;
                    }

                    InputManager.OnMenuAction(menuAction, false);
                };
                action.Enable();
                _inputActions[i] = action;
            }
        }

        private static InputAction GetInputAction(MenuAction menuAction, Key key)
        {
            var keyPath = key.ToString().Replace("Digit", "");
            return new InputAction(
                name: $"Menu_{menuAction}_{key}",
                type: InputActionType.Button,
                binding: $"<Keyboard>/{keyPath}"
            );
        }

        public void Dispose()
        {
            foreach (var action in _inputActions)
            {
                action.Disable();
                action.Dispose();
            }
        }
    }
}