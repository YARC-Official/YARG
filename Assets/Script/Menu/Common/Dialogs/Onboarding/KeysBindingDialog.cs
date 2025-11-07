using Cysharp.Threading.Tasks;
using UnityEngine.UI;
using YARG.Core;

namespace YARG.Menu.Dialogs
{
    public class KeysBindingDialog : FriendlyBindingDialog
    {
        private CurrentMode _currentMode;

        protected override (string initial, string complete) BindingMessages { get; set; } = (
            "When a key is highlighted, press the corresponding key on your keyboard.\n\nClick the Start button when you're ready to begin.",
            "Binding complete.\n\nYou will still need to manually set bindings for star power activation, touch effects, and menu navigation."
        );

        public override void Initialize()
        {
            base.Initialize();

            _currentMode = CurrentMode.ProKeys;
            Title.text = "Pro Keys Binding";
        }

        private void SetFiveLaneKeys()
        {
            _currentMode = CurrentMode.FiveLaneKeys;
            Title.text = "Five Lane Keys Binding";
            Message.text = "Now binding Five Lane Keys.\n\n" + BindingMessages.initial;
        }

        protected override void CheckForModeSwitch(string key, GameMode mode)
        {
            if (_currentMode == CurrentMode.ProKeys && !key.StartsWith("ProKeys.Key"))
            {
                SetFiveLaneKeys();
            }
        }

        protected override Image GetHighlightByName(GameMode mode, string bindingName)
        {
            if (_currentMode == CurrentMode.ProKeys)
            {
                if (bindingName.StartsWith("ProKeys.Key"))
                {
                    var key = _keyHighlights[int.Parse(bindingName[11..]) - 1];
                    return key;
                }

                return null;
            }

            if (_currentMode == CurrentMode.FiveLaneKeys)
            {
                return bindingName switch
                {
                    "ProKeys.OpenNote"  => _keyHighlights[9],
                    "ProKeys.GreenKey"  => _keyHighlights[11],
                    "ProKeys.RedKey"    => _keyHighlights[12],
                    "ProKeys.YellowKey" => _keyHighlights[14],
                    "ProKeys.BlueKey"   => _keyHighlights[16],
                    "ProKeys.OrangeKey" => _keyHighlights[17],
                    _                   => null
                };
            }

            return null;
        }

        private enum CurrentMode
        {
            ProKeys,
            FiveLaneKeys
        }
    }
}