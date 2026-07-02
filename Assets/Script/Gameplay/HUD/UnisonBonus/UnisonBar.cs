using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Helpers.Extensions;

namespace YARG.Gameplay.HUD
{
    public class UnisonBar : GameplayBehaviour
    {
        [SerializeField]
        private Image _fill;

        [SerializeField]
        private TextMeshProUGUI _successCounter;

        [SerializeField]
        private TextMeshProUGUI _totalCounter;

        [SerializeField]
        private Color _completeColor;
        [SerializeField]
        private Color _failColor;
        [SerializeField]
        private Color _progressColor;

        private float _targetProgress;
        private bool  _isFail;
        protected override void GameplayAwake()
        {
            _fill.fillAmount = 0f;
            _successCounter.color = Color.white;
            _totalCounter.color = Color.white;
            _isFail = false;
        }

        public void SetUnisonInfo(int playersHit, int totalPlayers)
        {
            _successCounter.text = playersHit.ToString();
            _totalCounter.text = totalPlayers.ToString();
        }
        public void SetProgress(float progress)
        {
            _targetProgress = progress;
            if (_isFail)
            {
                return;
            }
            if (progress >= 1f)
            {
                _fill.color = _completeColor;
                _successCounter.color = _completeColor;
                _totalCounter.color = _completeColor;
            }
            else
            {
                _successCounter.color = Color.white;
                _totalCounter.color = Color.white;
                _fill.color = _progressColor;
            }
        }
        public void SetFailState(bool isFail)
        {
            _isFail = isFail;
            if (isFail)
            {
                _successCounter.color = _failColor;
                _totalCounter.color = _failColor;
                _fill.color = _failColor;
            }
            else
            {
                SetProgress(_targetProgress);
            }
        }

        private void Update()
        {
            if (Mathf.Approximately(_fill.fillAmount, _targetProgress))
            {
                return;
            }
            // Lerp to new progress
            _fill.fillAmount = DOVirtual.EasedValue(_fill.fillAmount, _targetProgress, Time.deltaTime * 3, Ease.OutCubic);
        }
    }
}