using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Menu.Navigation;
using YARG.Settings.Types;

namespace YARG.Menu.Settings.Visuals
{
    public class ToggleSettingVisual : BaseSettingVisual<ToggleSetting>
    {
        [SerializeField]
        private Toggle _toggle;

        protected override void OnSettingInit()
        {
            RefreshVisual();
        }

        public override void RefreshVisual()
        {
            _toggle.isOn = Setting.Data;
        }

        public override NavigationScheme GetNavigationScheme()
        {
            return new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Green, "Toggle", () =>
                {
                    _toggle.isOn = !_toggle.isOn;
                }),
                new NavigationScheme.Entry(MenuAction.Down, "On", () =>
                {
                    _toggle.isOn = true;
                }),
                new NavigationScheme.Entry(MenuAction.Up, "Off", () =>
                {
                    _toggle.isOn = false;
                }),
                NavigateFinish
            }, true);
        }

        public void OnToggleChange()
        {
            Setting.Data = _toggle.isOn;
            RefreshVisual();
        }
    }
}