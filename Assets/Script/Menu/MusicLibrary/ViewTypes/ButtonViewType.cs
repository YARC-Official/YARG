using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace YARG.Menu.MusicLibrary
{
    public class ButtonViewType : ViewType
    {
        private static readonly Dictionary<int, Sprite> SPRITES = new();
        public override BackgroundType Background => BackgroundType.Button;
        public override string StableId => _stableId;

        public readonly int ID;

        private readonly string _text;
        private readonly Action _buttonAction;
        private readonly Sprite _sprite;
        private readonly string _stableId;
        private readonly string _buttonHelpText;

        public ButtonViewType(string text, string iconPath, Action buttonAction, int id = -1, string buttonHelpText = "")
        {
            _text = text;
            _buttonAction = buttonAction;
            ID = id;
            _stableId = $"Button:{id}:{text}";
            _buttonHelpText = buttonHelpText;

            if (!SPRITES.TryGetValue(id, out _sprite))
            {
                SPRITES.Add(id, _sprite = Addressables.LoadAssetAsync<Sprite>(iconPath).WaitForCompletion());
            }
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(_text, TextType.Bright, selected);
        }

        public override string GetSideText(bool selected)
        {
            return _buttonHelpText;
        }

#nullable enable
        public override Sprite? GetIcon()
#nullable disable
        {
            return _sprite;
        }

        public override void PrimaryButtonClick()
        {
            _buttonAction.Invoke();
        }
    }
}
