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

        private Color _disabledColorDefault;

        public override void RefreshVisual()
        {
            _toggle.SetIsOnWithoutNotify(Setting.Value);
        }

        /// <summary>
        /// Keeps read-only toggles legible: the checkmark remains visible while the
        /// grey base stays opaque.
        /// </summary>
        public override void SetEditable(bool editable, bool dim = true)
        {
            var colors = _toggle.colors;
            _disabledColorDefault = colors.disabledColor;

            colors.disabledColor = editable
                ? _disabledColorDefault
                : new Color(_disabledColorDefault.r, _disabledColorDefault.g,
                    _disabledColorDefault.b, 1f);
            _toggle.colors = colors;

            base.SetEditable(editable, dim);

            _toggle.graphic.color = editable
                ? Color.white
                : new Color(0.5f, 0.5f, 0.5f, 1f);
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
