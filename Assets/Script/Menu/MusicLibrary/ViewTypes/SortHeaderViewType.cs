using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace YARG.Menu.MusicLibrary
{
    public class SortHeaderViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Category;

        public override bool UseWiderPrimaryText => true;

        public readonly string HeaderText;
        public readonly string ShortcutName;
        private readonly int _songCount;

        public SortHeaderViewType(string headerText, int songCount, string shortcutName)
        {
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

#nullable enable
        public override Sprite? GetIcon()
#nullable disable
        {
            return Addressables.LoadAssetAsync<Sprite>("MusicLibraryIcons[Down]").WaitForCompletion();
        }
    }
}