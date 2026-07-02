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

        private readonly Color _completeColor = new(0.988f, 0.835f, 0.282f, 1f);
        private readonly Color _failColor     = new(0.953f, 0.169f, 0.216f, 1f);
        private readonly Color _progressColor = new(0.271f, 0.847f, 0.996f, 1f);

        private float _targetProgress;
        protected override void GameplayAwake()
        {
            _fill.fillAmount = 0f;
            _successCounter.color = Color.white;
            _totalCounter.color = Color.white;
        }

        public void SetUnisonInfo(int playersHit, int totalPlayers)
        {
            _successCounter.text = playersHit.ToString();
            _totalCounter.text = totalPlayers.ToString();
        }
        public void SetProgress(float progress)
        {
            if (progress >= 0)
            {
                _targetProgress = progress;
            }
            if (progress >= 1f)
            {
                _fill.color = _completeColor;
                _successCounter.color = _completeColor;
                _totalCounter.color = _completeColor;
            }
            else if (progress < 0)
            {
                _successCounter.color = _failColor;
                _totalCounter.color = _failColor;
                _fill.color = _failColor;
            }
            else
            {
                _successCounter.color = Color.white;
                _totalCounter.color = Color.white;
                _fill.color = _progressColor;
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