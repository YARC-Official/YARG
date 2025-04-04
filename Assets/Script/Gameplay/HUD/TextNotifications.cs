using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
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
        [SerializeField]
        private RectTransform _containerRect;
        [SerializeField]
        private Image _notificationBackground;
        [SerializeField]
        private Color _defaultColor;
        [SerializeField]
        private Color _starpowerColor;
        [SerializeField]
        private Color _grooveColor;

        private int _streak;
        private int _nextStreakCount;

        private Coroutine _coroutine;

        private readonly TextNotificationQueue _notificationQueue = new();

        private readonly PerformanceTextScaler _scaler = new(2f);

        private void OnEnable()
        {
            _text.text = string.Empty;
            _coroutine = null;
            _containerRect.localScale = Vector3.zero;
            _containerRect.gameObject.SetActive(true);
            _notificationBackground.gameObject.SetActive(true);
        }

        private void OnDisable()
        {
            if (_coroutine != null)
            {
                StopCoroutine(_coroutine);
            }

            _notificationBackground.gameObject.SetActive(false);
            _containerRect.gameObject.SetActive(false);
        }

        public void ShowNewHighScore()
        {
            // Don't build up notifications during a solo
            if (!gameObject.activeSelf) return;

            // Queue the  notification
            _notificationQueue.Enqueue(new TextNotification(TextNotificationType.NewHighScore));
        }

        public void ShowFullCombo()
        {
            _notificationQueue.Enqueue(new TextNotification(TextNotificationType.FullCombo));
        }

        public void ShowStrongFinish()
        {
            _notificationQueue.Enqueue(new TextNotification(TextNotificationType.StrongFinish));
        }

        public void ShowHotStart()
        {
            // Don't build up notifications during a solo
            if (!isActiveAndEnabled) return;

            _notificationQueue.Enqueue(new TextNotification(TextNotificationType.HotStart));
        }

        public void ShowBassGroove()
        {
            // Don't build up notifications during a solo
            if (!isActiveAndEnabled) return;

            _notificationQueue.Enqueue(new TextNotification(TextNotificationType.BassGroove));
        }

        public void ShowStarPowerReady()
        {
            // Don't build up notifications during a solo
            if (!isActiveAndEnabled) return;


            _notificationQueue.Enqueue(new TextNotification(TextNotificationType.StarPowerReady));
        }

        public void UpdateNoteStreak(int streak)
        {
            // Don't build up notifications during a solo
            if (!isActiveAndEnabled) return;

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
                    _notificationQueue.Enqueue(new TextNotification(TextNotificationType.NoteStreak, _nextStreakCount));
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

                // Set the color of the text background image based on the notifcation type
                _notificationBackground.color = GetBackgroundColor(textNotification.Type);

                _coroutine = StartCoroutine(ShowNextNotification(textNotification.Text));
            }
        }

        private IEnumerator ShowNextNotification(string notificationText)
        {
            _text.text = notificationText;

            _scaler.ResetAnimationTime();

            while (_scaler.AnimTimeRemaining > 0f)
            {
                _scaler.AnimTimeRemaining -= Time.deltaTime;
                float scale = _scaler.PerformanceTextScale();

                _containerRect.localScale = new Vector3(scale, scale, scale);
                yield return null;
            }

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

        public void SetActive(bool active)
        {
            _containerRect.gameObject.SetActive(active);
        }

        private Color GetBackgroundColor(TextNotificationType type)
        {
            return type switch
            {
                TextNotificationType.FullCombo      => _starpowerColor,
                TextNotificationType.BassGroove     => _grooveColor,
                TextNotificationType.StarPowerReady => _starpowerColor,
                _                                   => _defaultColor,
            };
        }
    }
}