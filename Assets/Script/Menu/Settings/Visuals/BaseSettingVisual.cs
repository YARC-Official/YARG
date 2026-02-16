using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
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

        private Image _evenBackgroundImage;
        private CancellationTokenSource _flashCts;

        public bool IsPresetSetting { get; private set; }
        public bool HasDescription { get; private set; }
        public string UnlocalizedName { get; private set; }

        protected virtual void Awake()
        {
            if (_evenBackground != null)
            {
                _evenBackgroundImage = _evenBackground.GetComponent<Image>();
            }
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

        public void Flash()
        {
            // Cancel the previous pulse so they don't overlap if spammed
            _flashCts?.Cancel();
            _flashCts?.Dispose();
            _flashCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            FlashAdvancedRevealAsync(_flashCts.Token).Forget();
        }

        private async UniTaskVoid FlashAdvancedRevealAsync(CancellationToken token)
        {
            if (_evenBackgroundImage == null)
            {
                return;
            }

            var wasActive = _evenBackground.activeSelf;
            _evenBackground.SetActive(true);

            // Calculate target color based on current color
            var flashColor = Color.Lerp(_evenBackgroundImage.color, Color.white, ADVANCED_FLASH_BRIGHTNESS);

            try
            {
                // Pulse handles its own baseColor inference and restoration
                await PulseColorAsync(
                    _evenBackgroundImage,
                    flashColor,
                    pulseCount: 2,
                    totalDuration: ADVANCED_FLASH_DURATION,
                    token: token
                );
            }
            catch (OperationCanceledException)
            {
                // Silent catch for cancellation (user triggered new flash or object destroyed)
            }
            finally
            {
                // Only handle the GameObject state here; PulseColorAsync handles the color restoration
                if (!wasActive && _evenBackground != null)
                {
                    _evenBackground.SetActive(false);
                }
            }
        }

        /// <summary>
        /// A generic helper to pulse an Image color in and out.
        /// Infers the base color from the image and restores it when finished.
        /// </summary>
        private async UniTask PulseColorAsync(Image image, Color pulseColor, int pulseCount, float totalDuration, CancellationToken token)
        {
            var initialColor = image.color;
            float phaseDuration = (totalDuration / pulseCount) / 2f;
            try
            {
                for (var i = 0; i < pulseCount; i++)
                {
                    // Pulse In
                    await LerpColorAsync(image, initialColor, pulseColor, phaseDuration, token);
                    // Pulse Out
                    await LerpColorAsync(image, pulseColor, initialColor, phaseDuration, token);
                }
            }
            finally
            {
                // Ensure color is restored even if cancelled
                if (image != null)
                {
                    image.color = initialColor;
                }
            }
        }

        private async UniTask LerpColorAsync(Image image, Color from, Color to, float duration, CancellationToken token)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                image.color = Color.Lerp(from, to, elapsed / duration);

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            image.color = to;
        }

        protected virtual void OnDestroy()
        {
            _flashCts?.Cancel();
            _flashCts?.Dispose();
            _flashCts = null;
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
