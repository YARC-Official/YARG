using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Gameplay.HUD
{
    public class UnisonBar : BaseUnisonObject
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
        private int _successCount;

        private float _targetProgress;

        private bool IsFailed => ParticipantFailState.ContainsValue(true);

        private void Update()
        {
            if (Mathf.Approximately(_fill.fillAmount, _targetProgress))
            {
                return;
            }

            // Lerp to new progress
            _fill.fillAmount =
                DOVirtual.EasedValue(_fill.fillAmount, _targetProgress, Time.deltaTime * 15, Ease.OutCubic);

            if (IsFailed)
            {
                return;
            }

            if (_fill.fillAmount >= 0.99f)
            {
                _fill.fillAmount = 1f;
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

        protected override void GameplayAwake()
        {
            _fill.fillAmount = 0f;
            _successCounter.color = Color.white;
            _totalCounter.color = Color.white;
            _successCount = 0;
        }

        public override void SetParticipants(List<int> participants)
        {
            base.SetParticipants(participants);
            _totalCounter.text = participants.Count.ToString();
        }

        public override void SetProgress(int engineId, float progress)
        {
            if (ParticipantFailState[engineId])
            {
                return;
            }

            if (progress >= 1f && ParticipantProgress[engineId] < 1f)
            {
                _successCount++;
                _successCounter.text = _successCount.ToString();
            }

            ParticipantProgress[engineId] = progress;

            _targetProgress = ParticipantProgress.Values.Average();
        }

        public override void FailUnison(int engineId)
        {
            base.FailUnison(engineId);
            _successCounter.color = _failColor;
            _totalCounter.color = _failColor;
            _fill.color = _failColor;
        }

        public override void ResetState()
        {
            _successCounter.text = "0";
            _targetProgress = 0f;
            _successCount = 0;
            _fill.fillAmount = 0f;
            _successCounter.color = Color.white;
            _totalCounter.color = Color.white;
        }
    }
}