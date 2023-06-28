using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace YARG.UI.MusicLibrary.ViewTypes
{
    public class SortHeaderViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Category;

        public override string PrimaryText => $"<#00B6F5><b>{_primary}</b><#006488>";

        public override string SideText =>
            $"<#00B6F5><b>{_songCount}</b> <#006488>{(_songCount == 1 ? "SONG" : "SONGS")}";

        private readonly string _primary;
        private readonly int _songCount;

        public SortHeaderViewType(string primary, int songCount)
        {
            _primary = primary;
            _songCount = songCount;
        }

        public override async UniTask<Sprite> GetIcon()
        {
            return await Addressables.LoadAssetAsync<Sprite>("Icon/ChevronDown").ToUniTask();
        }

        public override void PrimaryButtonClick()
        {
            SongSelection.Instance.SelectNextSection();
        }
    }
}