using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using YARG.Core.Input;
using YARG.Menu.Navigation;
using YARG.Menu.Settings.Visuals;
using YARG.Settings.Types;

namespace YARG
{
    public class DMXInformationPanelSettingVisual : BaseSettingVisual<DMXInformationPanelSetting>
    {
        [SerializeField]
        private TMPro.TMP_Text _textField;

        protected override void RefreshVisual()
        {
            _textField.text = Setting.Value.ToString(CultureInfo.InvariantCulture);
        }

        public override NavigationScheme GetNavigationScheme()
        {
            return new NavigationScheme(new()
            {
                NavigateFinish,
                new NavigationScheme.Entry(MenuAction.Up, "Increase", () =>
                {
                    Setting.Value++;
                    RefreshVisual();
                }),
                new NavigationScheme.Entry(MenuAction.Down, "Decrease", () =>
                {
                    Setting.Value--;
                    RefreshVisual();
                })
            }, true);
        }

    }
}
