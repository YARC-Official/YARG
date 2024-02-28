using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Menu.Data;

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
            var count = TextColorer.FormatString(
                _songCount.ToString("N0"),
                MenuData.Colors.PrimaryText,
                500);

            var songs = TextColorer.FormatString(
                _songCount == 1 ? "SONG" : "SONGS",
                MenuData.Colors.PrimaryText.WithAlpha(0.5f),
                500);

            return $"{count} {songs}";
        }

        public override async UniTask<Sprite> GetIcon()
        {
            return await Addressables.LoadAssetAsync<Sprite>("MusicLibraryIcons[Down]").ToUniTask();
        }
    }
}