using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Song;

namespace YARG.Menu.MusicLibrary
{
    public class SortHeaderViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Category;

        public override bool UseWiderPrimaryText => true;

        private readonly MusicLibraryMenu _musicLibrary;

        public readonly string HeaderText;
        public readonly string ShortcutName;
        private readonly int _songCount;

        public SortHeaderViewType(string headerText, int songCount, string shortcutName, MusicLibraryMenu musicLibrary)
        {
            _musicLibrary = musicLibrary;

            HeaderText = headerText;
            _songCount = songCount;

            ShortcutName = shortcutName;
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(HeaderText, TextType.Bright, selected);
        }

        public override string GetSideText(bool selected)
        {
            return CreateSongCountString(_songCount);
        }

        public override void PrimaryButtonClick()
        {
            _musicLibrary.ToggleCollapseOfSortHeader(this);
        }

#nullable enable
        public override Sprite? GetIcon()
#nullable disable
        {
            bool collapsed = SongContainer.IsHeaderCollapsed(HeaderText);
            var key = collapsed ? "MusicLibraryIcons[Right]" : "MusicLibraryIcons[Down]";
            return Addressables.LoadAssetAsync<Sprite>(key).WaitForCompletion();
        }
    }
}