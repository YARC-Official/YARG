using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace YARG.UI.MusicLibrary.ViewTypes
{
    public class ButtonViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Category;

        public override string PrimaryText => $"<color=white>{_primary}</color>";

        private string _primary;
        private string _iconPath;
        private Action _buttonAction;

        public ButtonViewType(string primary, string iconPath, Action buttonAction)
        {
            _primary = primary;
            _iconPath = iconPath;
            _buttonAction = buttonAction;
        }

        public override async UniTask<Sprite> GetIcon()
        {
            return await Addressables.LoadAssetAsync<Sprite>(_iconPath).ToUniTask();
        }

        public override void PrimaryButtonClick()
        {
            _buttonAction?.Invoke();
        }
    }
}