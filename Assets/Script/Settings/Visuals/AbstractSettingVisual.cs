using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using YARG.Settings.Types;

namespace YARG.Settings.Visuals
{
    public abstract class AbstractSettingVisual<T> : MonoBehaviour, ISettingVisual where T : ISettingType
    {
        [SerializeField]
        private LocalizeStringEvent settingText;

        public string SettingName { get; private set; }

        protected T Setting { get; private set; }

        public void SetSetting(string name)
        {
            SettingName = name;

            settingText.StringReference = new LocalizedString
            {
                TableReference = "Settings", TableEntryReference = name
            };

            Setting = (T) SettingsManager.GetSettingByName(name);

            OnSettingInit();
        }

        protected abstract void OnSettingInit();
        public abstract void RefreshVisual();
    }
}