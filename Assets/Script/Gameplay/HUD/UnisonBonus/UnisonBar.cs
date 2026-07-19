using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core;

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

        private int _hitNotes;
        private int _totalNotes;

        private Tweener _seekTween;

        private const float TWEEN_LENGTH = 0.1f;

        private float Progress => YargMath.InverseLerpF(0f, _totalNotes, _hitNotes);

        protected override void GameplayAwake()
        {
            _seekTween = _fill.DOFillAmount(Progress, TWEEN_LENGTH)
                .SetEase(Ease.OutCubic)
                .OnComplete(OnTweenComplete)
                .Pause()
                .SetAutoKill(false)
                .SetLink(gameObject);
        }

        private void SetTweenEnd()
        {
            _seekTween.ChangeEndValue(Progress, true);
            _seekTween.Play();
        }

        private void OnTweenComplete()
        {
            if (_hitNotes < _totalNotes)
            {
                return;
            }
            _fill.color = _completeColor;
            _successCounter.color = _completeColor;
            _totalCounter.color = _completeColor;
        }

        public override void AddParticipant(int participantId, int totalNotes)
        {
            base.AddParticipant(participantId, totalNotes);
            _totalNotes += totalNotes;
            _totalCounter.text = ParticipantCount.ToString();
        }

        public override void SetNotesHit(int engineId, int notesHit)
        {
            if (ParticipantFailState[engineId])
            {
                return;
            }

            var delta = notesHit - ParticipantNotesHit[engineId];

            if (delta == 0)
            {
                // Not sure how this would happen, but in case it does...
                return;
            }

            if (notesHit == ParticipantTotalNotes[engineId] && notesHit > ParticipantNotesHit[engineId])
            {
                _successCount++;
                _successCounter.text = _successCount.ToString();
            }

            _hitNotes += delta;

            SetTweenEnd();

            base.SetNotesHit(engineId, notesHit);
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
            base.ResetState();
            _successCounter.text = "0";
            _hitNotes = 0;
            _totalNotes = 0;
            _successCount = 0;
            _fill.fillAmount = 0f;
            _successCounter.color = Color.white;
            _totalCounter.color = Color.white;
        }
    }
}