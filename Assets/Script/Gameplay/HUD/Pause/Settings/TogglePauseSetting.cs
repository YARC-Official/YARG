using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Menu.Navigation;
using YARG.Settings.Types;

namespace YARG.Gameplay.HUD
{
    public class TogglePauseSetting : BasePauseSetting<ToggleSetting>
    {
        [Space]
        [SerializeField]
        private Toggle _toggle;

        public override void Initialize(string settingName, ToggleSetting setting)
        {
            base.Initialize(settingName, setting);

            _toggle.SetIsOnWithoutNotify(setting.Value);
        }

        protected override NavigationScheme GetNavigationScheme()
        {
            return new NavigationScheme(new()
            {
                NavigateFinish,
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Toggle", () =>
                {
                    Setting.Value = !Setting.Value;
                    RefreshVisual();
                }),
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Toggle", () =>
                {
                    Setting.Value = !Setting.Value;
                    RefreshVisual();
                })
            }, true);
        }

        public void OnValueChange()
        {
            Setting.Value = _toggle.isOn;
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            _toggle.SetIsOnWithoutNotify(Setting.Value);
        }
    }
}
