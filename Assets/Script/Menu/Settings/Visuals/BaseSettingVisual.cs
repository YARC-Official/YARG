using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Settings;
using YARG.Settings.Types;

namespace YARG.Menu.Settings.Visuals
{
    public abstract class BaseSettingVisual : MonoBehaviour
    {
        private const float PULSE_DURATION = 1.5f;
        private const float PULSE_BRIGHTNESS = 0.2f;

        protected static readonly NavigationScheme.Entry NavigateFinish = new(MenuAction.Red, "Menu.Common.Confirm", () =>
        {
            Navigator.Instance.PopScheme();
        });

        [SerializeField]
        private TextMeshProUGUI _settingLabel;

        [SerializeField]
        private GameObject _evenBackground;

        private Image _evenBackgroundImage;

        public bool IsPresetSetting { get; private set; }
        public bool HasDescription { get; private set; }
        public string UnlocalizedName { get; private set; }

        protected virtual void Awake()
        {
            _evenBackgroundImage = _evenBackground.GetComponent<Image>();
        }

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

        public virtual void AssignIndex(int index)
        {
            _evenBackground.SetActive(index % 2 == 0);
        }

        protected abstract void AssignSettingFromVariable(ISettingType reference);

        protected virtual void OnSettingInit()
        {
            RefreshVisual();
        }

        protected abstract void RefreshVisual();

        public abstract NavigationScheme GetNavigationScheme();

        public void Pulse()
        {
            var wasActive = _evenBackground.activeSelf;
            var initialColor = _evenBackgroundImage.color;
            var pulseColor = Color.Lerp(initialColor, Color.white, PULSE_BRIGHTNESS);
            _evenBackground.SetActive(true);
            _evenBackgroundImage.DOKill();
            _evenBackgroundImage.DOColor(pulseColor, PULSE_DURATION / 4f)
                .SetEase(Ease.InOutSine)
                .SetLoops(4, LoopType.Yoyo)
                .OnKill(() =>
                {
                    _evenBackgroundImage.color = initialColor;
                    _evenBackground.SetActive(wasActive);
                });
        }
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
