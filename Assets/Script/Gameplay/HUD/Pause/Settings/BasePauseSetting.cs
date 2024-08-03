using TMPro;
using UnityEngine;
using YARG.Core.Input;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Settings.Types;

namespace YARG.Gameplay.HUD
{
    public abstract class BasePauseSetting<T> : NavigatableBehaviour where T : ISettingType
    {
        protected readonly NavigationScheme.Entry NavigateFinish = new(MenuAction.Red, "Menu.Common.Confirm", () =>
        {
            Navigator.Instance.PopScheme();
        });

        [SerializeField]
        private GameObject _activeBackground;
        [SerializeField]
        private TextMeshProUGUI _text;

        private bool _focused;

        protected T Setting;

        public virtual void Initialize(string settingName, T setting)
        {
            Setting = setting;
            _text.text = Localize.Key("Settings.Setting", settingName, "PauseName");
        }

        protected abstract NavigationScheme GetNavigationScheme();

        public override void Confirm()
        {
            var scheme = GetNavigationScheme();
            scheme.PopCallback = () =>
            {
                _focused = false;
                _activeBackground.SetActive(false);
            };

            Navigator.Instance.PushScheme(scheme);

            _focused = true;
            _activeBackground.SetActive(true);
        }

        protected override void OnSelectionChanged(bool selected)
        {
            base.OnSelectionChanged(selected);
            OnDisable();
        }

        private void OnDisable()
        {
            // If the visual's nav scheme is still in the stack, make sure to pop it.
            if (_focused)
            {
                Navigator.Instance.PopScheme();
            }
        }
    }
}