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
        protected static readonly NavigationScheme.Entry NavigateFinish = new(MenuAction.Red, "Confirm", () =>
        {
            Navigator.Instance.PopScheme();
        });

        [SerializeField]
        private LocalizeStringEvent _settingLabel;

        public bool IsPresetSetting { get; private set; }
        public string UnlocalizedName { get; private set; }

        public void AssignSetting(string settingName)
        {
            IsPresetSetting = false;
            UnlocalizedName = settingName;

            _settingLabel.StringReference = LocaleHelper.StringReference(
                "Settings", $"Setting.{settingName}");

            AssignSettingFromVariable(SettingsManager.GetSettingByName(settingName));

            OnSettingInit();
        }

        public void AssignPresetSetting(string unlocalizedName, ISettingType reference)
        {
            IsPresetSetting = true;
            UnlocalizedName = unlocalizedName;

            _settingLabel.StringReference = LocaleHelper.StringReference(
                "Settings", $"PresetSetting.{unlocalizedName}");

            AssignSettingFromVariable(reference);

            OnSettingInit();
        }

        protected abstract void AssignSettingFromVariable(ISettingType reference);

        protected virtual void OnSettingInit()
        {
            RefreshVisual();
        }

        protected abstract void RefreshVisual();

        public abstract NavigationScheme GetNavigationScheme();
    }

    public abstract class BaseSettingVisual<T> : BaseSettingVisual where T : ISettingType
    {
        protected T Setting { get; private set; }

        protected sealed override void AssignSettingFromVariable(ISettingType reference)
        {
            Setting = (T) reference;
        }
    }
}