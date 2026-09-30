using YARG.Core.Song;
using YARG.Menu.ListMenu;

namespace YARG.Menu.Dialogs.Components
{
    public class SongViewType : BaseViewType
    {
        private readonly SongEntry _songEntry;
        private readonly LibrarySearchDialog _searchDialog;

        public SongEntry SongEntry => _songEntry;

        public override BackgroundType Background => BackgroundType.Normal;

        public override string GetPrimaryText(bool what) => _songEntry?.Name ?? "Unknown Title";
        public override string GetSecondaryText(bool what) => _songEntry?.Artist ?? "Unknown Artist";

        public SongViewType(SongEntry songEntry)
        {
            _songEntry = songEntry;
        }

        public void ViewClick()
        {

        }
    }
}