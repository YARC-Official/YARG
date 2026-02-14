using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Input;
using YARG.Localization;

namespace YARG.Menu.Navigation
{
    public class NavigationScheme
    {
        public readonly struct Entry
        {
            public static readonly Entry NavigateUp = new(MenuAction.Up, "Menu.Common.Up", context =>
            {
                NavigationGroup.CurrentNavigationGroup.SelectPrevious(context.IsRepeat);
            });

            public static readonly Entry NavigateDown = new(MenuAction.Down, "Menu.Common.Down", context =>
            {
                NavigationGroup.CurrentNavigationGroup.SelectNext(context.IsRepeat);
            });

            public static readonly Entry NavigateSelect = new(MenuAction.Green, "Menu.Common.Confirm", () =>
            {
                NavigationGroup.CurrentNavigationGroup.ConfirmSelection();
            });

            public readonly MenuAction Action;
            public readonly string LocalizationKey;
            public readonly bool Hide;

            private readonly Action<NavigationContext> _handler;
            private readonly Action<NavigationContext> _onHoldHandler;
            private readonly Action<NavigationContext> _onHoldOffHandler;

            public readonly float HoldSeconds;
            public bool HasHoldHandler => _onHoldHandler != null && HoldSeconds > 0f;

            public string DisplayName => Localize.Key(LocalizationKey);

            public Entry(MenuAction action, string localizationKey, Action handler, Action onHoldOffHandler = null, bool hide = false)
            {
                Action = action;
                LocalizationKey = localizationKey;
                _handler = _ => handler?.Invoke();
                _onHoldHandler = null;
                _onHoldOffHandler = _ => onHoldOffHandler?.Invoke();
                HoldSeconds = 0f;
                Hide = hide;
            }

            public Entry(MenuAction action, string localizationKey, Action<NavigationContext> handler, Action<NavigationContext> onHoldOffHandler = null, bool hide = false)
            {
                Action = action;
                LocalizationKey = localizationKey;
                _handler = handler;
                _onHoldHandler = null;
                _onHoldOffHandler = onHoldOffHandler;
                HoldSeconds = 0f;
                Hide = hide;
            }

            public Entry(MenuAction action, string localizationKey, Action<NavigationContext> handler,
                float holdSeconds, Action<NavigationContext> onHoldHandler,
                Action<NavigationContext> onHoldOffHandler = null, bool hide = false)
            {
                Action = action;
                LocalizationKey = localizationKey;
                _handler = handler;
                _onHoldHandler = onHoldHandler;
                _onHoldOffHandler = onHoldOffHandler;
                HoldSeconds = holdSeconds;
                Hide = hide;
            }

            public void Invoke() => Invoke(new(Action, null));

            public void Invoke(NavigationContext context)
            {
                _handler?.Invoke(context);
            }

            public void InvokeHoldOffHandler() => InvokeHoldOffHandler(new(Action, null));

            public void InvokeHoldOffHandler(NavigationContext context)
            {
                _onHoldOffHandler?.Invoke(context);
            }

            public void InvokeHoldHandler() => InvokeHoldHandler(new(Action, null));

            public void InvokeHoldHandler(NavigationContext context)
            {
                _onHoldHandler?.Invoke(context);
            }
        }

        public static readonly NavigationScheme Empty = new(new(), null);
        public static readonly NavigationScheme EmptyWithoutMusicPlayer = new(new(), false);
        public static readonly NavigationScheme EmptyWithMusicPlayer = new(new(), true);

        private readonly List<Entry> _entries;
        public IReadOnlyList<Entry> Entries => _entries;

        /// <summary>
        /// Whether or not the music player is allowed.
        /// Null means to preserve the existing state of the music player.
        /// </summary>
        public bool? AllowsMusicPlayer { get; }

        public Action PopCallback;

        public bool SuppressHelpBar;

        public NavigationScheme(List<Entry> entries, bool? allowsMusicPlayer, Action popCallback = null)
        {
            _entries = entries;

            AllowsMusicPlayer = allowsMusicPlayer;
            PopCallback = popCallback;
        }

        public NavigationScheme(List<Entry> entries, bool? allowsMusicPlayer, bool suppressHelpBar)
        {
            _entries = entries;

            AllowsMusicPlayer = allowsMusicPlayer;
            PopCallback = null;
            SuppressHelpBar = suppressHelpBar;
        }

        public void InvokeFuncs(NavigationContext context)
        {
            foreach (var entry in _entries.Where(i => i.Action == context.Action))
            {
                entry.Invoke(context);
            }
        }

        public void InvokeHoldOffFuncs(NavigationContext context)
        {
            foreach (var entry in _entries.Where(i => i.Action == context.Action))
            {
                entry.InvokeHoldOffHandler(context);
            }
        }

        public void InvokeHoldFuncs(NavigationContext context)
        {
            foreach (var entry in _entries.Where(i => i.Action == context.Action && i.HasHoldHandler))
            {
                entry.InvokeHoldHandler(context);
            }
        }

        public bool TryGetHoldSeconds(MenuAction action, out float holdSeconds)
        {
            holdSeconds = float.MaxValue;
            bool found = false;

            foreach (var entry in _entries.Where(i => i.Action == action && i.HasHoldHandler))
            {
                holdSeconds = Math.Min(holdSeconds, entry.HoldSeconds);
                found = true;
            }

            return found;
        }
    }
}
