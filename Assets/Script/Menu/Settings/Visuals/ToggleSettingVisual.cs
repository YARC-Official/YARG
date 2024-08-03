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

        protected override void RefreshVisual()
        {
            _toggle.SetIsOnWithoutNotify(Setting.Value);
        }

        public override NavigationScheme GetNavigationScheme()
        {
            return new NavigationScheme(new()
            {
                NavigateFinish,
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.On", () =>
                {
                    _toggle.isOn = true;
                }),
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Off", () =>
                {
                    _toggle.isOn = false;
                })
            }, true);
        }

        public void OnToggleChange()
        {
            Setting.Value = _toggle.isOn;
            RefreshVisual();
        }
    }
}