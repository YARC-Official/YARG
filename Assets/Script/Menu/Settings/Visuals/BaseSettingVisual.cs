using System.Collections;
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
        private const float ADVANCED_FLASH_DURATION = 1.5f;
        private const float ADVANCED_FLASH_BRIGHTNESS = 0.1f;

        protected static readonly NavigationScheme.Entry NavigateFinish = new(MenuAction.Red, "Menu.Common.Confirm", () =>
        {
            Navigator.Instance.PopScheme();
        });

        [SerializeField]
        private TextMeshProUGUI _settingLabel;

        [SerializeField]
        private GameObject _evenBackground;

        private Coroutine _advancedFlash;

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

        public virtual void AssignIndex(int index)
        {
            if (index % 2 == 0)
            {
                _evenBackground.SetActive(true);
            }
            else
            {
                _evenBackground.SetActive(false);
            }
        }

        public void FlashAdvancedReveal()
        {
            if (_advancedFlash != null)
            {
                StopCoroutine(_advancedFlash);
            }

            _advancedFlash = StartCoroutine(FlashAdvancedRevealCoroutine());
        }

        private IEnumerator FlashAdvancedRevealCoroutine()
        {
            var wasActive = _evenBackground.activeSelf;
            _evenBackground.SetActive(true);

            var evenBackgroundImage = _evenBackground.GetComponent<Image>();
            var baseColor = evenBackgroundImage.color;
            var flashColor = Color.Lerp(baseColor, Color.white, ADVANCED_FLASH_BRIGHTNESS);
            const int pulseCount = 2;
            float pulseDuration = ADVANCED_FLASH_DURATION / pulseCount;

            for (var pulseIndex = 0; pulseIndex < pulseCount; pulseIndex++)
            {
                float pulseElapsed = 0f;
                while (pulseElapsed < pulseDuration)
                {
                    pulseElapsed += Time.unscaledDeltaTime;
                    float pulseProgress = Mathf.Clamp01(pulseElapsed / pulseDuration);
                    float inOutProgress = Mathf.Sin(pulseProgress * Mathf.PI);
                    evenBackgroundImage.color = Color.Lerp(baseColor, flashColor, inOutProgress);

                    yield return null;
                }
            }

            evenBackgroundImage.color = baseColor;
            if (!wasActive)
            {
                _evenBackground.SetActive(false);
            }

            _advancedFlash = null;
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
