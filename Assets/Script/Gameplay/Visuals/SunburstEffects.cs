using DG.Tweening;
using UnityEngine;
using YARG.Playback;
using YARG.Settings;

namespace YARG.Gameplay.Visuals
{
    public class SunburstEffects : GameplayBehaviour
    {
        [SerializeField]
        private GameObject _sunburstEffect;
        [SerializeField]
        private GameObject _lightEffect;

        [Space]
        [SerializeField]
        private Color _grooveSunburstColor;
        [SerializeField]
        private Color _grooveLightColor;

        [Space]
        [SerializeField]
        private Color _starpowerSunburstColor;
        [SerializeField]
        private Color _starpowerLightColor;

        private Tweener  _sunburstPulseTween;
        private Sequence _multiplierIncreaseSequence;
        private Sequence _multiplierDecreaseSequence;
        private Sequence _grooveStartSequence;
        private Sequence _starpowerStartSequence;
        private Sequence _sunburstDisableSequence;
        private Vector3  _originalScale;
        private int      _previousMultiplier;

        private Material _sunburstMaterial;
        private Color    _grooveSunburstMaterialColor;
        private Color    _grooveSunburstLightColor;
        private Light    _light;
        private float    _lightIntensity;

        private bool _groove;
        private bool _starpower;

        private const float TRANSITION_DURATION = 0.433f;

        protected override void GameplayAwake()
        {
            GameManager.BeatEventHandler.Visual.Subscribe(PulseSunburst, BeatEventType.StrongBeat);

            // Get the components we'll need to manipulate later
            _sunburstMaterial = _sunburstEffect.GetComponent<SpriteRenderer>().material;
            _light = _lightEffect.GetComponent<Light>();

            // Save original state so we can return to it later if necessary
            _originalScale = _sunburstEffect.transform.localScale;
            _grooveSunburstMaterialColor = _sunburstMaterial.color;
            _grooveSunburstLightColor = _light.color;
            _lightIntensity = _light.intensity;

            // Set up tweens for later use
            _sunburstPulseTween = _sunburstEffect.transform.DOScale(_originalScale * 0.85f, 0.25f).SetAutoKill(false)
                .SetEase(Ease.OutSine).Pause();

            _multiplierIncreaseSequence = DOTween.Sequence(_sunburstEffect).SetAutoKill(false).Pause();
            _multiplierIncreaseSequence.Append(_sunburstEffect.transform.DOScale(0.045f, TRANSITION_DURATION).SetEase(Ease.InSine)).
                Join(_light.DOIntensity(0.0f, TRANSITION_DURATION).SetEase(Ease.InCubic)).
                AppendCallback(SetGrooveSunburst);

            _multiplierDecreaseSequence = DOTween.Sequence(_sunburstEffect).SetAutoKill(false).Pause();
            _multiplierDecreaseSequence.Append(_light.DOColor(Color.red, 0.000001f)).
                Join(_sunburstMaterial.DOColor(Color.red, 0.000001f)).
                Append(_sunburstEffect.transform.DOScale(0.045f, TRANSITION_DURATION).SetEase(Ease.InSine)).
                Join(_light.DOIntensity(0.0f, TRANSITION_DURATION).SetEase(Ease.InCubic)).
                AppendCallback(SetGrooveSunburst);

            _grooveStartSequence = DOTween.Sequence(_sunburstEffect).SetAutoKill(false).Pause();
            _grooveStartSequence.Append(_sunburstEffect.transform.DOScale(_originalScale, TRANSITION_DURATION)).
                Join(_sunburstMaterial.DOColor(_grooveSunburstColor, TRANSITION_DURATION)).
                Join(_light.DOColor(_grooveLightColor, TRANSITION_DURATION)).
                Join(_light.DOIntensity(_lightIntensity, TRANSITION_DURATION));

            _starpowerStartSequence = DOTween.Sequence(_sunburstEffect).SetAutoKill(false).Pause();
            _starpowerStartSequence.Append(_sunburstEffect.transform.DOScale(_originalScale, TRANSITION_DURATION)).
                Join(_sunburstMaterial.DOColor(_starpowerSunburstColor, TRANSITION_DURATION)).
                Join(_light.DOColor(_starpowerLightColor, TRANSITION_DURATION)).
                Join(_light.DOIntensity(_lightIntensity, TRANSITION_DURATION));

            _sunburstDisableSequence = DOTween.Sequence(_sunburstEffect).SetAutoKill(false).Pause();
            _sunburstDisableSequence.Append(_sunburstEffect.transform.DOScale(_originalScale * 0.4f, TRANSITION_DURATION))
                .Join(_light.DOIntensity(0.0f, TRANSITION_DURATION)).
                AppendCallback(DisableSunburst);

            // Disable the sunburst effect in case it is already on
            _sunburstEffect.SetActive(false);
            _lightEffect.SetActive(false);
        }

        public void SetSunburstEffects(bool groove, bool starpower, int multiplier)
        {
            starpower &= SettingsManager.Settings.StarPowerHighwayFx.Value != StarPowerHighwayFxMode.Off;

            // Handle going in and out of starpower
            if (starpower != _starpower)
            {
                if (starpower)
                {
                    ActivateStarpowerSunburst();
                }
                else if (groove)
                {
                    ActivateGrooveSunburst();
                }
                else
                {
                    _sunburstDisableSequence.Restart();
                }

                _groove = groove;
                _starpower = starpower;
                _previousMultiplier = multiplier;
                return;
            }

            _groove = groove;
            _starpower = starpower;

            // Handle multiplier changes not connected to starpower activation
            if (multiplier > _previousMultiplier && multiplier > 1)
            {
                if (!starpower && groove)
                {
                    ActivateGrooveSunburst();
                    _grooveStartSequence.Restart();
                }
                else if (!starpower)
                {
                    ActivateGrooveSunburst();
                    _multiplierIncreaseSequence.Restart();
                }
            }

            // If groove is set, that means the decrease was due to starpower expiring, so we don't want
            // to run the sequence even though the multiplier has decreased.
            if (!groove && multiplier < _previousMultiplier && _previousMultiplier > 1)
            {
                // If we're in starpower, don't do anything
                if (!starpower)
                {
                    ActivateGrooveSunburst(true);
                    _multiplierDecreaseSequence.Restart();
                }
            }

            _previousMultiplier = multiplier;
        }

        private void Update()
        {
            _sunburstEffect.transform.Rotate(0f, 0f, Time.deltaTime * -25f);
        }

        private void PulseSunburst()
        {
            if (!_groove && !_starpower)
            {
                return;
            }

            if (_sunburstEffect.activeInHierarchy)
            {
                // Snap to full size and tween to smaller size
                _sunburstEffect.transform.localScale = _originalScale;
                _sunburstPulseTween?.Restart();
            }
        }

        private void SetGrooveSunburst()
        {
            _sunburstEffect.transform.localScale = _originalScale;
            _sunburstMaterial.color = _grooveSunburstMaterialColor;
            _light.color = _grooveSunburstLightColor;
            _light.intensity = _lightIntensity;
            _sunburstEffect.SetActive(_groove);
            _lightEffect.SetActive(_groove);
        }

        private void SetStarpowerSunburst()
        {
            _sunburstEffect.transform.localScale = _originalScale;
            _sunburstMaterial.color = _starpowerSunburstColor;
            _light.color = _starpowerLightColor;
            _light.intensity = _lightIntensity;
            _sunburstEffect.SetActive(_starpower);
            _lightEffect.SetActive(_starpower);
        }

        private void ActivateGrooveSunburst(bool forceLight = false)
        {
            // Ensure that the disable tween isn't still running
            if (_sunburstDisableSequence.IsPlaying())
            {
                _sunburstDisableSequence.Complete(false);
            }

            // If _starpower is set that means we are coming out of starpower, so we just want to run the sequence
            if (_starpower)
            {
                _grooveStartSequence.Restart();
                return;
            }

            if (_groove)
            {
                _sunburstEffect.transform.localScale = _originalScale * 0.5f;
                _sunburstMaterial.color = _grooveSunburstColor;
                _light.intensity = 0f;
                _light.color = _grooveSunburstLightColor;
            }
            else
            {
                _sunburstMaterial.color = Color.white;
                _light.color = Color.white;
                _light.intensity = 0.75f;
                _sunburstEffect.transform.localScale = _originalScale * 0.65f;
            }
            _sunburstEffect.SetActive(true);
            _lightEffect.SetActive(_groove || forceLight);
            // This one doesn't start the sequence because there are multiple sequence options that could be run
        }

        private void ActivateStarpowerSunburst()
        {
            if (_groove)
            {
                // If we're in groove, we don't want to reset scale and such
                _starpowerStartSequence.Restart();
                return;
            }

            // Ensure that the disable tween isn't still running
            if (_sunburstDisableSequence.IsPlaying())
            {
                _sunburstDisableSequence.Complete(false);
            }

            // We need to make sure that we're set up for starpower before we start the sequence
            _sunburstEffect.transform.localScale = _originalScale * 0.5f;
            _sunburstMaterial.color = _starpowerSunburstColor;
            _light.color = _starpowerLightColor;
            _light.intensity = 0f;
            _sunburstEffect.SetActive(true);
            _lightEffect.SetActive(true);
            _starpowerStartSequence.Restart();
        }

        private void DisableSunburst()
        {
            _sunburstEffect.SetActive(false);
            _lightEffect.SetActive(false);
        }

        protected override void GameplayDestroy()
        {
            _sunburstPulseTween?.Kill();
            _multiplierIncreaseSequence?.Kill();
            _multiplierDecreaseSequence?.Kill();
            _grooveStartSequence?.Kill();
            _starpowerStartSequence?.Kill();
            _sunburstDisableSequence?.Kill();
            GameManager.BeatEventHandler.Visual.Unsubscribe(PulseSunburst);
        }
    }
}
