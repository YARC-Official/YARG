using System.Collections.Generic;
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

            _toggle.onValueChanged.AddListener(SetValue);
            _toggle.SetIsOnWithoutNotify(setting.Value);
        }

        protected override NavigationScheme GetNavigationScheme()
        {
            return new NavigationScheme(new List<NavigationScheme.Entry>
            {
                NavigateFinish,
                new(MenuAction.Up, "Menu.Common.Toggle", () =>
                {
                    SetValue(!Setting.Value);
                }),
                new(MenuAction.Down, "Menu.Common.Toggle", () =>
                {
                    SetValue(!Setting.Value);
                })
            }, false);
        }

        public void OnValueChange()
        {
            SetValue(_toggle.isOn);
        }

        private void SetValue(bool isOn)
        {
            Setting.Value = isOn;
            _toggle.SetIsOnWithoutNotify(Setting.Value);
        }

        private void OnDestroy()
        {
            if (_toggle != null)
            {
                _toggle.onValueChanged.RemoveListener(SetValue);
            }
        }
    }
}
