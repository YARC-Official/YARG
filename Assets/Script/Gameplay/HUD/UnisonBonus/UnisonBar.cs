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

        private float Progress => YargMath.InverseLerpF(0f, _totalNotes, _hitNotes);

        private bool _isFailed;

        private void Update()
        {
            if (Mathf.Approximately(_fill.fillAmount, Progress))
            {
                return;
            }

            // Lerp to new progress
            _fill.fillAmount =
                DOVirtual.EasedValue(_fill.fillAmount, Progress, Time.deltaTime * 15, Ease.OutCubic);

            if (_isFailed)
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

        public override void AddParticipant(int participantId, int totalNotes)
        {
            base.AddParticipant(participantId, totalNotes);
            _totalNotes += totalNotes;
            _totalCounter.text = ParticipantTotalNotes.Count.ToString();
        }

        public override void SetNotesHit(int engineId, int notesHit)
        {
            if (ParticipantFailState[engineId])
            {
                return;
            }


            if (notesHit == ParticipantTotalNotes[engineId] && notesHit > ParticipantNotesHit[engineId])
            {
                _successCount++;
                _successCounter.text = _successCount.ToString();
            }

            var delta = notesHit - ParticipantNotesHit[engineId];
            _hitNotes += delta;

            base.SetNotesHit(engineId, notesHit);
        }

        public override void FailUnison(int engineId)
        {
            base.FailUnison(engineId);
            _successCounter.color = _failColor;
            _totalCounter.color = _failColor;
            _fill.color = _failColor;
            _isFailed = true;
        }

        public override void ResetState()
        {
            base.ResetState();
            _successCounter.text = "0";
            _hitNotes = 0;
            _totalNotes = 0;
            _isFailed = false;
            _successCount = 0;
            _fill.fillAmount = 0f;
            _successCounter.color = Color.white;
            _totalCounter.color = Color.white;
        }
    }
}