using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Player;
using YARG.Settings.Preview;

namespace YARG.Gameplay.Visuals
{
    public class StrikelinePositioner : MonoBehaviour
    {
        private const float MAX_ANIM_DELAY = 2f;
        private const float LOCAL_ANIM_OFFSET = 0.1f;

        private const float ANIM_FRET_ZOOM_DELAY         = 0.5f;
        private const float ANIM_BASE_TO_PEAK_INTERVAL   = 0.167f;
        private const float ANIM_PEAK_TO_VALLEY_INTERVAL = 0.083f;
        private const float ANIM_INIT_Z_OFFSET           = -1.2f;
        private const float ANIM_PEAK_Z_OFFSET           = 0.1f;

        private float       _zOffset;
        private float       _globalAnimDelay;
        private Coroutine   _coroutine;
        private GameManager _gameManager;

        private void Start()
        {
            _zOffset = transform.localPosition.z;

            _gameManager = FindObjectOfType<GameManager>();

            // Determine the necessary delay
            var timeToFirstNote = _gameManager.Chart.GetFirstNoteStartTime() + 2;
            var animLength = ANIM_BASE_TO_PEAK_INTERVAL + ANIM_PEAK_TO_VALLEY_INTERVAL;
            var latestStart = timeToFirstNote - animLength;
            _globalAnimDelay = Mathf.Clamp((float) latestStart, 0f, MAX_ANIM_DELAY);

            if (_gameManager != null && !_gameManager.IsPractice)
            {
                _coroutine = StartCoroutine(RaiseStrikeline(true));
            }

        }

        private IEnumerator RaiseStrikeline(bool isGameplayStart)
        {
            var transform = this.transform;
            transform.localPosition = transform.localPosition.WithZ(_zOffset + ANIM_INIT_Z_OFFSET);

            var basePlayer = GetComponentInParent<BasePlayer>();
            float delay = isGameplayStart
                ? basePlayer.transform.GetSiblingIndex() * LOCAL_ANIM_OFFSET + _globalAnimDelay + ANIM_FRET_ZOOM_DELAY
                : 0f;

            yield return DOTween.Sequence()
                .PrependInterval(delay)
                .Append(transform
                    .DOMoveZ(_zOffset + ANIM_PEAK_Z_OFFSET, ANIM_BASE_TO_PEAK_INTERVAL)
                    .SetEase(Ease.OutCirc))
                .Append(transform
                    .DOMoveZ(_zOffset, ANIM_PEAK_TO_VALLEY_INTERVAL)
                    .SetEase(Ease.InOutSine));
        }

        private void OnDestroy()
        {
            if (_coroutine != null)
            {
                StopCoroutine(_coroutine);
            }
        }
    }
}


