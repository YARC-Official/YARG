using UnityEngine;
using UnityEngine.UI;
using YARG.Settings.Types;

namespace YARG.Menu.Settings.Visuals
{
    public class ToggleSettingVisual : AbstractSettingVisual<ToggleSetting>
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

        public void OnToggleChange()
        {
            Setting.Data = _toggle.isOn;
            RefreshVisual();
        }
    }
}