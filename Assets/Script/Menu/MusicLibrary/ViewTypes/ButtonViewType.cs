using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace YARG.Menu.MusicLibrary
{
    public class ButtonViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Category;

        public int Id { get; }

        private readonly string _primary;
        private readonly string _iconPath;
        private readonly Action _buttonAction;

        public ButtonViewType(string primary, string iconPath, Action buttonAction, int id = -1)
        {
            _primary = primary;
            _iconPath = iconPath;
            _buttonAction = buttonAction;

            Id = id;
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(_primary, TextType.Bright, selected);
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