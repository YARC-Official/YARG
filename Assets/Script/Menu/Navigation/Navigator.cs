using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Menu.Persistent;
using YARG.Player;

namespace YARG.Menu.Navigation
{
    public enum MenuAction
    {
        //         // Guitar | Drums      |
        Select,    // Green  | Green      |
        Back,      // Red    | Red        |
        Shortcut1, // Yellow | Yellow Cym |
        Shortcut2, // Blue   | Blue Cym   |
        Shortcut3, // Orange | Kick       |
        Up,        // Strum  | Yellow     |
        Down,      // Strum  | Blue       |
        Pause,     // Pause  |            |
        More,      // Select | Green Cym  |
    }

    public readonly struct NavigationContext
    {
        /// <summary>
        /// The type of navigation.
        /// </summary>
        public readonly MenuAction Action;

        /// <summary>
        /// The <see cref="Player"/> that this event was invoked from. Can be null.
        /// </summary>
        public readonly YargPlayer Player;

        public NavigationContext(MenuAction action, YargPlayer player)
        {
            Action = action;
            Player = player;
        }

        public bool IsSameAs(NavigationContext other)
        {
            return other.Action == Action && other.Player == Player;
        }
    }

    public class Navigator : MonoSingleton<Navigator>
    {
        private const float INPUT_REPEAT_TIME = 0.035f;
        private const float INPUT_REPEAT_COOLDOWN = 0.5f;

        private class HoldContext
        {
            public readonly NavigationContext Context;
            public float Timer = INPUT_REPEAT_COOLDOWN;

            public HoldContext(NavigationContext context)
            {
                Context = context;
            }
        }

        public event Action<NavigationContext> NavigationEvent;

        private readonly List<HoldContext> _heldInputs = new();
        private readonly Stack<NavigationScheme> _schemeStack = new();

        private void Start()
        {
            UpdateHelpBar();
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
                    InvokeNavigationEvent(heldInput.Context);
                }
            }

            // TODO: Keyboard inputs for menus
            // UpdateKeyboardInput();
        }

        public void CallNavigationEvent(MenuAction action, YargPlayer player)
        {
            InvokeNavigationEvent(new NavigationContext(action, player));
        }

        public void StartNavigationHold(MenuAction action, YargPlayer player)
        {
            var ctx = new NavigationContext(action, player);

            // Skip if the input is already being held
            if (_heldInputs.Any(i => i.Context.IsSameAs(ctx)))
            {
                return;
            }

            InvokeNavigationEvent(ctx);
            _heldInputs.Add(new HoldContext(ctx));
        }

        public void EndNavigationHold(MenuAction action, YargPlayer binding)
        {
            var ctx = new NavigationContext(action, binding);

            _heldInputs.RemoveAll(i => i.Context.IsSameAs(ctx));
        }

        public bool IsHeld(MenuAction action)
        {
            return _heldInputs.Any(i => i.Context.Action == action);
        }

        private void InvokeNavigationEvent(NavigationContext ctx)
        {
            NavigationEvent?.Invoke(ctx);

            if (_schemeStack.Count > 0)
            {
                _schemeStack.Peek().InvokeFuncs(ctx.Action);
            }
        }

        public void PushScheme(NavigationScheme scheme)
        {
            _schemeStack.Push(scheme);
            UpdateHelpBar();
        }

        public void PopScheme()
        {
            _schemeStack.Pop();
            UpdateHelpBar();
        }

        public void PopAllSchemes()
        {
            _schemeStack.Clear();
            UpdateHelpBar();
        }

        public void ForceHideMusicPlayer()
        {
            HelpBar.Instance.MusicPlayer.gameObject.SetActive(false);
        }

        private void UpdateHelpBar()
        {
            if (_schemeStack.Count <= 0)
            {
                HelpBar.Instance.gameObject.SetActive(false);
            }
            else
            {
                HelpBar.Instance.gameObject.SetActive(true);
                HelpBar.Instance.SetInfoFromScheme(_schemeStack.Peek());
            }
        }
    }
}