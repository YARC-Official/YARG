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
            public readonly string DisplayName;
            private readonly Action<NavigationContext> _handler;
            private readonly Action<NavigationContext> _onHoldOffHandler;

            public Entry(MenuAction action, string localizationKey, Action handler, Action onHoldOffHandler = null)
            {
                Action = action;
                DisplayName = Localize.Key(localizationKey);
                _handler = _ => handler?.Invoke();
                _onHoldOffHandler = _ => onHoldOffHandler?.Invoke();
            }

            public Entry(MenuAction action, string localizationKey, Action<NavigationContext> handler, Action<NavigationContext> onHoldOffHandler = null)
            {
                Action = action;
                DisplayName = Localize.Key(localizationKey);
                _handler = handler;
                _onHoldOffHandler = onHoldOffHandler;
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

        public NavigationScheme(List<Entry> entries, bool? allowsMusicPlayer, Action popCallback = null)
        {
            _entries = entries;

            AllowsMusicPlayer = allowsMusicPlayer;
            PopCallback = popCallback;
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
    }
}