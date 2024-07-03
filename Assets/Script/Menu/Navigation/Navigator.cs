using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using YARG.Core.Input;
using YARG.Input;
using YARG.Menu.Persistent;
using YARG.Player;

namespace YARG.Menu.Navigation
{
    public readonly struct NavigationContext
    {
        /// <summary>
        /// The type of navigation.
        /// </summary>
        public readonly MenuAction Action;

        /// <summary>
        /// The <see cref="YargPlayer"/> that this event was invoked from.
        /// </summary>
        public readonly YargPlayer Player;

        /// <summary>
        /// Whether or not this action is a repeat.
        /// </summary>
        public readonly bool IsRepeat;

        public NavigationContext(MenuAction action, YargPlayer player, bool repeat = false)
        {
            Action = action;
            Player = player;
            IsRepeat = repeat;
        }

        public bool IsSameAs(NavigationContext other)
        {
            return other.Action == Action && other.Player == Player;
        }

        public NavigationContext AsRepeat()
        {
            return new NavigationContext(Action, Player, true);
        }
    }

    [DefaultExecutionOrder(-10)]
    public class Navigator : MonoSingleton<Navigator>
    {
        private const float INPUT_REPEAT_TIME = 0.035f;
        private const float INPUT_REPEAT_COOLDOWN = 0.5f;

        private static readonly HashSet<MenuAction> RepeatActions = new()
        {
            MenuAction.Up,
            MenuAction.Down,
            MenuAction.Left,
            MenuAction.Right,
        };

        private static Keyboard menuKeyboard;

        private static Dictionary<Key, MenuAction> KeyboardMenuActions = new()
        {
            { Key.UpArrow, MenuAction.Up },
            { Key.DownArrow, MenuAction.Down },
            { Key.LeftArrow, MenuAction.Left },
            { Key.RightArrow, MenuAction.Right },
            { Key.Enter, MenuAction.Green },
            { Key.Escape, MenuAction.Red },
        };

        public class HoldContext
        {
            public readonly NavigationContext Context;
            public float Timer = INPUT_REPEAT_COOLDOWN;

            public HoldContext(NavigationContext context)
            {
                Context = context;
            }
        }

        public bool MusicPlayerActive => HelpBar.Instance.MusicPlayer.isActiveAndEnabled;

        public bool DisableMenuInputs { get; set; }

        public event Action<NavigationContext> NavigationEvent;

        private readonly List<HoldContext> _heldInputs = new();
        private readonly Stack<NavigationScheme> _schemeStack = new();

        private void Start()
        {
            InputManager.MenuInput += ProcessInput;
            UpdateHelpBar().Forget();
        }

        private void Update()
        {
            // Update held inputs
            foreach (var heldInput in _heldInputs)
            {
                heldInput.Timer -= Time.unscaledDeltaTime;

                if (heldInput.Timer <= 0f)
                {
                    heldInput.Timer = INPUT_REPEAT_TIME;
                    InvokeNavigationEvent(heldInput.Context.AsRepeat());
                }
            }

            // Process keyboard inputs for menus
            UpdateKeyboardInput();
        }

        private void UpdateKeyboardInput()
        {
            // Get current keyboard
            Keyboard keyboard = Keyboard.current;

            // If there is no keyboard, bail
            if (keyboard == null) { return; }

            // Loop through mapped keys
            foreach (var map in KeyboardMenuActions)
            {
                // Get the key control
                KeyControl key = keyboard[map.Key];

                // Just pressed or released?
                if (key.wasPressedThisFrame)
                {
                    ProcessKeyboardAction(map.Value, true);
                }
                else if (key.wasReleasedThisFrame)
                {
                    ProcessKeyboardAction(map.Value, false);
                }
            }
        }

        private void ProcessInput(YargPlayer player, ref GameInput input)
        {
            var action = (MenuAction) input.Action;

            // Swap up and down for lefty flip
            if (player.Profile.LeftyFlip)
            {
                action = action switch
                {
                    MenuAction.Up    => MenuAction.Down,
                    MenuAction.Down  => MenuAction.Up,
                    MenuAction.Left  => MenuAction.Right,
                    MenuAction.Right => MenuAction.Left,
                    _                => action
                };
            }

            var context = new NavigationContext(action, player);

            if (input.Button)
            {
                StartNavigationHold(context);
            }
            else
            {
                EndNavigationHold(context);
            }
        }

        private void ProcessKeyboardAction(MenuAction action, bool down)
        {
            // Attempt to get the first player
            YargPlayer player = PlayerContainer.Players.FirstOrDefault();

            // If no player found, ignore the action
            if (player == null) { return; }

            // Create the context
            var context = new NavigationContext(action, player);

            // Process
            if (down)
            {
                StartNavigationHold(context);
            }
            else
            {
                EndNavigationHold(context);
            }
        }

        private void StartNavigationHold(NavigationContext context)
        {
            // Skip if the input is already being held
            if (_heldInputs.Any(i => i.Context.IsSameAs(context)))
            {
                return;
            }

            InvokeNavigationEvent(context);

            if (RepeatActions.Contains(context.Action))
            {
                _heldInputs.Add(new HoldContext(context));
            }
        }

        private void EndNavigationHold(NavigationContext context)
        {
            _heldInputs.RemoveAll(i => i.Context.IsSameAs(context));
            InvokeHoldOffEvent(context);
        }

        public bool IsHeld(MenuAction action)
        {
            return _heldInputs.Any(i => i.Context.Action == action);
        }

        private void InvokeNavigationEvent(NavigationContext ctx)
        {
            if (DisableMenuInputs)
            {
                return;
            }

            NavigationEvent?.Invoke(ctx);

            if (_schemeStack.Count > 0)
            {
                _schemeStack.Peek().InvokeFuncs(ctx);
            }
        }

        private void InvokeHoldOffEvent(NavigationContext ctx)
        {
            if (DisableMenuInputs)
            {
                return;
            }

            if (_schemeStack.Count > 0)
            {
                _schemeStack.Peek().InvokeHoldOffFuncs(ctx);
            }
        }

        public void PushScheme(NavigationScheme scheme)
        {
            _schemeStack.Push(scheme);
            UpdateHelpBar().Forget();
        }

        public void PopScheme()
        {
            var scheme = _schemeStack.Pop();
            scheme.PopCallback?.Invoke();
            UpdateHelpBar().Forget();
        }

        public void PopAllSchemes()
        {
            // Pop all one by one so we can call each callback (instead of clearing)
            while (_schemeStack.Count >= 1)
            {
                var scheme = _schemeStack.Pop();
                scheme.PopCallback?.Invoke();
            }

            UpdateHelpBar().Forget();
        }

        private async UniTask UpdateHelpBar()
        {
            // Wait one frame to update, in case another one gets pushed.
            // This prevents the music player from resetting across schemes.
            await UniTask.WaitForEndOfFrame(this);

            if (_schemeStack.Count <= 0)
            {
                HelpBar.Instance.Reset();
            }
            else
            {
                HelpBar.Instance.SetInfoFromScheme(_schemeStack.Peek());
            }
        }
    }
}