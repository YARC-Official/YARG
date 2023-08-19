using UnityEngine;
using UnityEngine.Localization.Components;
using YARG.Core.Input;
using YARG.Helpers;
using YARG.Menu.Navigation;
using YARG.Settings;
using YARG.Settings.Types;

namespace YARG.Menu.Settings.Visuals
{
    public abstract class BaseSettingVisual : MonoBehaviour
    {
        protected static readonly NavigationScheme.Entry NavigateFinish = new(MenuAction.Red, "Finish", () =>
        {
            Navigator.Instance.PopScheme();
        });

        [SerializeField]
        private LocalizeStringEvent _settingLabel;

        public string SettingName { get; private set; }

        public void AssignSetting(string name)
        {
            SettingName = name;
            _settingLabel.StringReference = LocaleHelper.StringReference("Settings", name);

            AssignSettingToVariable(name);

            OnSettingInit();
        }

        protected abstract void AssignSettingToVariable(string name);

        protected abstract void OnSettingInit();

        public abstract void RefreshVisual();

        public abstract NavigationScheme GetNavigationScheme();
    }

    public abstract class BaseSettingVisual<T> : BaseSettingVisual where T : ISettingType
    {
        protected T Setting { get; private set; }

        protected sealed override void AssignSettingToVariable(string name)
        {
            Setting = (T) SettingsManager.GetSettingByName(name);
        }
    }
}