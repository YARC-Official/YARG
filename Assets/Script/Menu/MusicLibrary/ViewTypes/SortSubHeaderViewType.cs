using System.Transactions;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Menu.Data;

namespace YARG.Menu.MusicLibrary
{
    public class SortSubHeaderViewType : SortHeaderViewType
    {
        public override BackgroundType Background => BackgroundType.Category;

        public SortSubHeaderViewType(string headerText, int songCount) : base(headerText, songCount)
        {
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(HeaderText, TextType.Secondary, selected);
        }

        public override async UniTask<Sprite> GetIcon()
        {
            return await UniTask.FromResult<Sprite>(null);
        }
    }
}