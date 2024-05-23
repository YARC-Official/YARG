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
        private readonly int _songCount;

        public SortHeaderViewType(string headerText, int songCount)
        {
            HeaderText = headerText;
            _songCount = songCount;
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(HeaderText, TextType.Bright, selected);
        }

        public override string GetSideText(bool selected)
        {
            return CreateSongCountString(_songCount);
        }

        private static readonly Sprite _downIcon = Addressables.LoadAssetAsync<Sprite>("MusicLibraryIcons[Down]").WaitForCompletion();
#nullable enable
        public override Sprite? GetIcon()
#nullable disable
        {
            return _downIcon;
        }
    }
}