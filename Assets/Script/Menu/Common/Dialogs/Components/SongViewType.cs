using System;
using YARG.Core.Song;
using YARG.Menu.ListMenu;

namespace YARG.Menu.Dialogs.Components
{
    public class SongViewType : BaseViewType
    {
        private readonly SongEntry _songEntry;
        private readonly Action<SongEntry> _songSelected;

        public SongEntry SongEntry => _songEntry;

        public override BackgroundType Background => BackgroundType.Normal;

        public override string GetPrimaryText(bool what) => _songEntry?.Name ?? "Unknown Title";
        public override string GetSecondaryText(bool what) => _songEntry?.Artist ?? "Unknown Artist";

        public SongViewType(SongEntry songEntry, Action<SongEntry> songSelected)
        {
            _songEntry = songEntry;
            _songSelected = songSelected;
        }

        public void ViewClick()
        {
            _songSelected?.Invoke(_songEntry);
        }
    }
}