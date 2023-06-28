using System;
using System.Collections.Generic;
using System.Linq;

namespace YARG.Input
{
    public class NavigationScheme
    {
        public readonly struct Entry
        {
            public readonly MenuAction Type;
            public readonly string DisplayName;
            public readonly Action Func;

            public Entry(MenuAction type, string displayName, Action func)
            {
                Type = type;
                DisplayName = displayName;
                Func = func;
            }
        }

        private List<Entry> _entries;
        public IReadOnlyList<Entry> Entries => _entries;

        public bool AllowsMusicPlayer { get; private set; }

        public NavigationScheme(List<Entry> entries, bool allowsMusicPlayer)
        {
            _entries = entries;
            AllowsMusicPlayer = allowsMusicPlayer;
        }

        public void InvokeFuncs(MenuAction type)
        {
            foreach (var entry in _entries.Where(i => i.Type == type))
            {
                entry.Func?.Invoke();
            }
        }
    }
}