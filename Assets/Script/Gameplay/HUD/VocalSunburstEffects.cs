using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using YARG.Helpers.Extensions;
using YARG.Settings;

namespace YARG.Gameplay.HUD
{
    public class VocalSunburstEffects : MonoBehaviour
    {
        private const float TRANSITION_DURATION = 0.433f;

        private readonly Vector3 _originalScale = new(1, 1, 1);

        [SerializeField]
        private GameObject _sunburstEffect;
        [SerializeField]
        private Image _sunburstGroove;
        [SerializeField]
        private Image _sunburstStarPower;

        private Sequence _sunburstPulseTween;
        private Sequence _sunburstEnableSequence;
        private Sequence _grooveToSpSequence;
        private Sequence _sunburstDisableSequence;

        private bool _groove;
        private bool _starpower;
        private int  _previousMultiplier;

        private void Awake()
        {
            _sunburstEffect.transform.localScale = _originalScale;
            _sunburstPulseTween = DOTween.Sequence(_sunburstEffect).SetAutoKill(false)
                .SetLink(_sunburstEffect.gameObject).Pause();
            _sunburstPulseTween.Append(
                _sunburstEffect.transform.DOScale(1f, 0.0001f))
                .Append(_sunburstEffect.transform.DOScale(0.85f, 0.25f));

            _grooveToSpSequence = DOTween.Sequence(_sunburstEffect).SetAutoKill(false)
                .SetLink(_sunburstEffect.gameObject).Pause();
            _grooveToSpSequence
                .Append(_sunburstGroove.DOFade(0f, TRANSITION_DURATION))
                .Join(_sunburstStarPower.DOFade(1f, TRANSITION_DURATION));

            _sunburstDisableSequence = DOTween.Sequence(_sunburstEffect).SetAutoKill(false)
                .SetLink(_sunburstEffect.gameObject).Pause();
            _sunburstDisableSequence
                .Append(_sunburstEffect.transform.DOScale(1f, 0.00001f))
                .Append(_sunburstEffect.transform.DOScale(0.4f, TRANSITION_DURATION))
                .AppendCallback(DisableSunburst);

            _sunburstEffect.transform.localScale = Vector3.zero;

            _sunburstEnableSequence = DOTween.Sequence(_sunburstEffect).SetAutoKill(false)
                .SetLink(_sunburstEffect.gameObject).Pause();
            _sunburstEnableSequence
                .Append(_sunburstEffect.transform.DOScale(_originalScale, TRANSITION_DURATION));

            // Hide it initially
            _sunburstGroove.color = _sunburstGroove.color.WithAlpha(0f);
            _sunburstStarPower.color = _sunburstStarPower.color.WithAlpha(0f);
        }

        public void SetSunburstEffects(bool groove, bool starpower, int multiplier)
        {
            starpower &= SettingsManager.Settings.StarPowerHighwayFx.Value != StarPowerHighwayFxMode.Off;

            bool wasGroove = _groove; // Track previous state to know exactly when it changes

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
            if (multiplier > _previousMultiplier)
            {
                // We just hit groove this frame
                if (!starpower && groove && !wasGroove)
                {
                    ActivateGrooveSunburst();
                }
            }
            else if (multiplier < _previousMultiplier)
            {
                // We just lost groove this frame
                if (!starpower && !groove && wasGroove)
                {
                    _sunburstDisableSequence.Restart();
                }
            }

            _previousMultiplier = multiplier;
        }

        private void ActivateGrooveSunburst()
        {
            // Ensure that the disable tween isn't still running
            if (_sunburstDisableSequence.IsPlaying())
            {
                _sunburstDisableSequence.Complete(false);
            }

            // If _starpower is set that means we are coming out of starpower, so we just want to run the sequence
            if (_starpower)
            {
                _grooveToSpSequence.Complete(false);
                _grooveToSpSequence.PlayBackwards();
                return;
            }

            _sunburstEffect.transform.localScale = _originalScale * 0.4f;
            _sunburstGroove.color = _sunburstGroove.color.WithAlpha(1f);
            _sunburstStarPower.color = _sunburstStarPower.color.WithAlpha(0f);
            _sunburstEnableSequence.Restart();
        }

        private void Update()
        {
            if (!_groove && !_starpower)
            {
                return;
            }

            _sunburstEffect.transform.Rotate(0f, 0f, Time.deltaTime * -25f);
        }

        public void PulseSunburst()
        {
            if (!_groove && !_starpower)
            {
                return;
            }

            if (_sunburstEffect.gameObject.activeInHierarchy)
            {
                _sunburstPulseTween?.Restart();
            }
        }

        private void ActivateStarpowerSunburst()
        {
            if (_groove)
            {
                // If we're in groove, we don't want to reset scale and such
                _grooveToSpSequence.Restart();
                return;
            }

            // Ensure that any tweens that hide the sunburst are not still running
            if (_sunburstDisableSequence.IsPlaying())
            {
                _sunburstDisableSequence.Complete(false);
            }

            // We need to make sure that we're set up for starpower before we start the sequence
            _sunburstEffect.transform.localScale = _originalScale * 0.4f;
            _sunburstGroove.color = _sunburstGroove.color.WithAlpha(0f);
            _sunburstStarPower.color = _sunburstStarPower.color.WithAlpha(1f);
            _sunburstEnableSequence.Restart();
        }

        private void DisableSunburst()
        {
            _sunburstEffect.transform.localScale = Vector3.zero;
        }
    }
}