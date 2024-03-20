using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace YARG.Menu.MusicLibrary
{
    public class ButtonViewType : ViewType
    {
        public override BackgroundType Background => _secondaryStyle
            ? BackgroundType.Normal
            : BackgroundType.Category;

        public int Id { get; }

        private readonly string _text;
        private readonly string _iconPath;
        private readonly Action _buttonAction;
        private readonly bool   _secondaryStyle;

        public ButtonViewType(string text, string iconPath, Action buttonAction, int id = -1,
            bool secondaryStyle = false)
        {
            _text = text;
            _iconPath = iconPath;
            _buttonAction = buttonAction;
            _secondaryStyle = secondaryStyle;

            Id = id;
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(_text, TextType.Bright, selected);
        }

        public override async UniTask<Sprite> GetIcon()
        {
            return await Addressables.LoadAssetAsync<Sprite>(_iconPath).ToUniTask();
        }

        public override void PrimaryButtonClick()
        {
            _buttonAction?.Invoke();
        }

        public override void IconClick()
        {
            PrimaryButtonClick();
        }
    }
}