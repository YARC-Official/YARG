using DG.Tweening;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Settings;

namespace YARG.Gameplay.Visuals
{
    public class SunburstEffects : GameplayBehaviour
    {
        [SerializeField]
        private GameObject _grooveSunburstEffect;
        [SerializeField]
        private GameObject _grooveLightEffect;

        [Space]
        [SerializeField]
        private GameObject _starpowerSunburstEffect;
        [SerializeField]
        private GameObject _starpowerLightEffect;

        private Tweener _grooveTween;
        private Tweener _starpowerTween;
        private Vector3 _originalScale;

        protected override void GameplayAwake()
        {
            GameManager.BeatEventHandler.Subscribe(PulseSunburst);
            _originalScale = _grooveSunburstEffect.transform.localScale;
        }

        public void SetSunburstEffects(bool groove, bool starpower)
        {
            starpower &= SettingsManager.Settings.StarPowerHighwayFx.Value != StarPowerHighwayFxMode.Off;

            _grooveSunburstEffect.SetActive(groove && !starpower);
            _grooveLightEffect.SetActive(groove && !starpower);

            _starpowerSunburstEffect.SetActive(starpower);
            _starpowerLightEffect.SetActive(starpower);

            _grooveTween ??= _grooveSunburstEffect.transform.DOScale(_originalScale * 0.85f, 0.25f).SetAutoKill(false)
                .SetEase(Ease.OutSine).Pause();

            _starpowerTween ??= _starpowerSunburstEffect.transform.DOScale(_originalScale * 0.85f, 0.25f).SetAutoKill(false)
                .SetEase(Ease.InSine).Pause();
        }

        private void Update()
        {
            _grooveSunburstEffect.transform.Rotate(0f, 0f, Time.deltaTime * -25f);
            _starpowerSunburstEffect.transform.Rotate(0f, 0f, Time.deltaTime * -25f);
        }

        private void PulseSunburst(Beatline beatline)
        {
            if (_grooveSunburstEffect.activeInHierarchy)
            {
                // Snap to full size and tween to smaller size
                _grooveSunburstEffect.transform.localScale = _originalScale;
                _grooveTween?.Restart();
            }

            if (_starpowerSunburstEffect.activeInHierarchy)
            {
                // Snap to full size and tween to smaller size
                _starpowerSunburstEffect.transform.localScale = _originalScale;
                _starpowerTween?.Restart();
            }
        }

        protected override void GameplayDestroy()
        {
            _grooveTween?.Kill();
            _starpowerTween?.Kill();
            GameManager.BeatEventHandler.Unsubscribe(PulseSunburst);
        }
    }
}
