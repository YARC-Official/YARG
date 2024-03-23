using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace YARG.Menu.MusicLibrary
{
    public class SortHeaderViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Category;

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

        public override async UniTask<Sprite> GetIcon()
        {
            return await Addressables.LoadAssetAsync<Sprite>("MusicLibraryIcons[Down]").ToUniTask();
        }
    }
}