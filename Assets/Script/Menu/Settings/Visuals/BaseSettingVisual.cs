using TMPro;
using UnityEngine;
using YARG.Core.Input;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Settings;
using YARG.Settings.Types;

namespace YARG.Menu.Settings.Visuals
{
    public abstract class BaseSettingVisual : MonoBehaviour
    {
        protected static readonly NavigationScheme.Entry NavigateFinish = new(MenuAction.Red, "Menu.Common.Confirm", () =>
        {
            Navigator.Instance.PopScheme();
        });

        [SerializeField]
        private TextMeshProUGUI _settingLabel;

        public bool IsPresetSetting { get; private set; }
        public bool HasDescription { get; private set; }
        public string UnlocalizedName { get; private set; }

        public void AssignSetting(string settingName, bool hasDescription)
        {
            IsPresetSetting = false;
            HasDescription = hasDescription;
            UnlocalizedName = settingName;

            _settingLabel.text = Localize.Key("Settings.Setting", settingName, "Name");

            AssignSettingFromVariable(SettingsManager.GetSettingByName(settingName));

            OnSettingInit();
        }

        public void AssignPresetSetting(string unlocalizedName, bool hasDescription, ISettingType reference)
        {
            IsPresetSetting = true;
            HasDescription = hasDescription;
            UnlocalizedName = unlocalizedName;

            _settingLabel.text = Localize.Key("Settings.PresetSetting", unlocalizedName, "Name");

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