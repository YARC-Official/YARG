using UnityEngine;
using UnityEngine.Localization.Components;
using YARG.Helpers;
using YARG.Settings;
using YARG.Settings.Types;

namespace YARG.Menu.Settings.Visuals
{
    public abstract class AbstractSettingVisual<T> : MonoBehaviour, ISettingVisual where T : ISettingType
    {
        [SerializeField]
        private LocalizeStringEvent _settingLabel;

        public string SettingName { get; private set; }

        protected T Setting { get; private set; }

        public void SetSetting(string name)
        {
            SettingName = name;

            _settingLabel.StringReference = LocaleHelper.StringReference("Settings", name);

            Setting = (T) SettingsManager.GetSettingByName(name);

            OnSettingInit();
        }

        protected abstract void OnSettingInit();
        public abstract void RefreshVisual();
    }
}