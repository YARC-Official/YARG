using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using YARG.Settings;

namespace YARG.Gameplay.HUD
{
    public enum NoteStreakFrequencyMode
    {
        Frequent,
        Sparse,
        Disabled
    }

    public class TextNotifications : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _text;

        private int _streak;
        private int _nextStreakCount;

        private Coroutine _coroutine;

        private readonly TextNotificationQueue _notificationQueue = new();

        private void OnEnable()
        {
            _text.text = string.Empty;
            _coroutine = null;
        }

        private void OnDisable()
        {
            if (_coroutine != null)
            {
                StopCoroutine(_coroutine);
            }
        }

        public void ShowNewHighScore()
        {
            // Don't build up notifications during a solo
            if (!gameObject.activeSelf) return;

            // Queue the  notification
            _notificationQueue.Enqueue(new TextNotification(TextNotificationType.NewHighScore, "NEW HIGH SCORE"));
        }

        public void ShowFullCombo()
        {
            _notificationQueue.Enqueue(new TextNotification(TextNotificationType.FullCombo, "FULL COMBO"));
        }

        public void ShowStrongFinish()
        {
            _notificationQueue.Enqueue(new TextNotification(TextNotificationType.StrongFinish, "STRONG FINISH"));
        }

        public void ShowHotStart()
        {
            // Don't build up notifications during a solo
            if (!gameObject.activeSelf) return;

            _notificationQueue.Enqueue(new TextNotification(TextNotificationType.HotStart, "HOT START"));
        }

        public void ShowBassGroove()
        {
            // Don't build up notifications during a solo
            if (!gameObject.activeSelf) return;

            _notificationQueue.Enqueue(new TextNotification(TextNotificationType.BassGroove, "BASS GROOVE"));
        }

        public void ShowStarPowerReady()
        {
            // Don't build up notifications during a solo
            if (!gameObject.activeSelf) return;

            _notificationQueue.Enqueue(new TextNotification(TextNotificationType.StarPowerReady, "STAR POWER READY"));
        }

        public void UpdateNoteStreak(int streak)
        {
            // Don't build up notifications during a solo
            if (!gameObject.activeSelf) return;

            // Only push to the queue if there is a change to the streak
            if (streak == _streak) return;

            // If the streak is less than before, then reset
            if (streak < _streak || _streak == 0)
            {
                _nextStreakCount = 0;
                NextNoteStreakNotification();
            }

            // Update the streak
            _streak = streak;

            // Queue the note streak notification
            if (_streak >= _nextStreakCount)
            {
                if (SettingsManager.Settings.NoteStreakFrequency.Value != NoteStreakFrequencyMode.Disabled)
                {
                    _notificationQueue.Enqueue(new TextNotification(TextNotificationType.NoteStreak,
                        $"{_nextStreakCount}-NOTE STREAK"));
                }
                NextNoteStreakNotification();
            }
        }

        private void Update()
        {
            // Never update this if text notifications are disabled
            if (SettingsManager.Settings.DisableTextNotifications.Value) return;

            if (_coroutine == null && _notificationQueue.Count > 0)
            {
                var textNotification = _notificationQueue.Dequeue();
                _coroutine = StartCoroutine(ShowNextNotification(textNotification.Text));
            }
        }

        private IEnumerator ShowNextNotification(string notificationText)
        {
            _text.text = notificationText;
            _text.transform.localScale = Vector3.zero;

            yield return DOTween.Sequence()
                .Append(DOTween.Sequence()
                    .Append(_text.transform.DOScale(1.1f, 0.167f).SetEase(Ease.OutCirc))
                    .Append(_text.transform.DOScale(1f, 0.167f).SetEase(Ease.InOutSine))
                    .AppendInterval(2.667f))
                .Append(DOTween.Sequence()
                    .Append(_text.transform.DOScale(1.1f, 0.167f).SetEase(Ease.InOutSine))
                    .Append(_text.transform.DOScale(0f, 0.167f).SetEase(Ease.InCirc)))
                .WaitForCompletion();

            _text.text = string.Empty;
            _coroutine = null;
        }

        private void NextNoteStreakNotification()
        {
            if (SettingsManager.Settings.NoteStreakFrequency.Value == NoteStreakFrequencyMode.Disabled)
            {
                _nextStreakCount = int.MaxValue;
                return;
            }

            switch (_nextStreakCount)
            {
                case 0:
                    _nextStreakCount = 50;
                    break;
                case 50:
                    _nextStreakCount = 100;
                    break;
                case >= 100 when SettingsManager.Settings.NoteStreakFrequency.Value == NoteStreakFrequencyMode.Frequent:
                    _nextStreakCount += 100;
                    break;
                case 100 when SettingsManager.Settings.NoteStreakFrequency.Value == NoteStreakFrequencyMode.Sparse:
                    _nextStreakCount = 250;
                    break;
                case >= 250 when SettingsManager.Settings.NoteStreakFrequency.Value == NoteStreakFrequencyMode.Sparse:
                    _nextStreakCount += 250;
                    break;
            }
        }

        public void ForceReset()
        {
            _notificationQueue.Clear();
            _nextStreakCount = 0;
            _streak = 0;
        }
    }
}