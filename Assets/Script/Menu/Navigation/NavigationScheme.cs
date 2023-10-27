using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Input;

namespace YARG.Menu.Navigation
{
    public class NavigationScheme
    {
        public readonly struct Entry
        {
            public static readonly Entry NavigateUp = new(MenuAction.Up, "Up", (ctx) =>
            {
                NavigationGroup.CurrentNavigationGroup.SelectPrevious(ctx);
            });

            public static readonly Entry NavigateDown = new(MenuAction.Down, "Down", (ctx) =>
            {
                NavigationGroup.CurrentNavigationGroup.SelectNext(ctx);
            });

            public static readonly Entry NavigateSelect = new(MenuAction.Green, "Confirm", () =>
            {
                NavigationGroup.CurrentNavigationGroup.ConfirmSelection();
            });

            public readonly MenuAction Action;
            public readonly string DisplayName;
            private readonly Action<NavigationContext> _handler;

            public Entry(MenuAction action, string displayName, Action handler)
            {
                Action = action;
                DisplayName = displayName;
                _handler = _ => handler?.Invoke();
            }

            public Entry(MenuAction action, string displayName, Action<NavigationContext> handler)
            {
                Action = action;
                DisplayName = displayName;
                _handler = handler;
            }

            public void Invoke() => Invoke(new(Action, null));

            public void Invoke(NavigationContext context)
            {
                _handler?.Invoke(context);
            }
        }

        public static readonly NavigationScheme Empty = new(new(), false);
        public static readonly NavigationScheme EmptyWithMusicPlayer = new(new(), true);

        private readonly List<Entry> _entries;
        public IReadOnlyList<Entry> Entries => _entries;

        public bool AllowsMusicPlayer { get; private set; }

        public Action PopCallback;

        public NavigationScheme(List<Entry> entries, bool allowsMusicPlayer, Action popCallback = null)
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
    }
}