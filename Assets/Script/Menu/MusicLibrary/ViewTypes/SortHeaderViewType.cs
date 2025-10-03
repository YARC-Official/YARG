using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace YARG.Menu.MusicLibrary
{
    public class SortHeaderViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Category;

        public override bool UseWiderPrimaryText => true;

        public readonly  string HeaderText;
        public readonly  string ShortcutName;
        public readonly  bool   Collapsed;
        private readonly int    _songCount;
        private readonly Action _onClicked;

        public SortHeaderViewType(string headerText, int songCount, string shortcutName, bool collapsed = false, Action onClicked = null)
        {
            HeaderText = headerText;
            _songCount = songCount;
            ShortcutName = shortcutName;
            Collapsed = collapsed;
            _onClicked = onClicked;
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(HeaderText, TextType.Bright, selected);
        }

        public override string GetSideText(bool selected)
        {
            return CreateSongCountString(_songCount);
        }

#nullable enable
        public override Sprite? GetIcon()
#nullable disable
        {
            string assetKey = Collapsed ? "MusicLibraryIcons[Right]" : "MusicLibraryIcons[Down]";
            return Addressables.LoadAssetAsync<Sprite>(assetKey).WaitForCompletion();
        }

        public override void PrimaryButtonClick()
        {
            _onClicked?.Invoke();
        }
    }
}