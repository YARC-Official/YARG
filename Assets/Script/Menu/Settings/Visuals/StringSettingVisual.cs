using System.Globalization;
using TMPro;
using UnityEngine;
using YARG.Menu.Navigation;
using YARG.Settings.Types;

namespace YARG.Menu.Settings.Visuals
{
    public class StringSettingVisual : BaseSettingVisual<StringSetting>
    {
        [SerializeField]
        private TMP_InputField _inputField;

        public override NavigationScheme GetNavigationScheme() => NavigationScheme.EmptyWithMusicPlayer;

        public override void RefreshVisual()
        {
            _inputField.text = Setting.Value.ToString(CultureInfo.InvariantCulture);
        }
    }
}