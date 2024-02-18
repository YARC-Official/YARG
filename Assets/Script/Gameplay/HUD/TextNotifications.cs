﻿using System.Collections;
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

        private TextNotificationQueue _notificationQueue = new();

        private readonly PerformanceTextScaler _scaler = new(2f);
        private Coroutine _coroutine;

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
            _notificationQueue.Enqueue(new TextNotification(TextNotificationType.NewHighScore, "NEW HIGHSCORE"));
        }

        public void ShowFullCombo()
        {
            _notificationQueue.Enqueue(new TextNotification(TextNotificationType.FullCombo, "FULL COMBO"));
        }

        public void ShowHotStart()
        {
            _notificationQueue.Enqueue(new TextNotification(TextNotificationType.HotStart, "HOT START"));
        }

        public void ShowBassGroove()
        {
            _notificationQueue.Enqueue(new TextNotification(TextNotificationType.BassGroove, "BASS GROOVE"));
        }

        public void ShowStarPowerReady()
        {
            _notificationQueue.Enqueue(new TextNotification(TextNotificationType.StarPowerReady, "OVERDRIVE READY"));
        }

        public void ShowStrongFinish()
        {
            _notificationQueue.Enqueue(new TextNotification(TextNotificationType.StrongFinish, "STRONG FINISH"));
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
                if (!SettingsManager.Settings.NoteStreakFrequency.ValueEquals(NoteStreakFrequencyMode.Sparse))
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

            _scaler.ResetAnimationTime();

            while (_scaler.AnimTimeRemaining > 0f)
            {
                _scaler.AnimTimeRemaining -= Time.deltaTime;
                float scale = _scaler.PerformanceTextScale();

                _text.transform.localScale = new Vector3(scale, scale, scale);
                yield return null;
            }

            _text.text = string.Empty;
            _coroutine = null;
        }

        private void NextNoteStreakNotification()
        {
            if (SettingsManager.Settings.NoteStreakFrequency.ValueEquals(NoteStreakFrequencyMode.Frequent))
            {
                switch (_nextStreakCount)
                {
                    case 0:
                        _nextStreakCount = 50;
                        break;
                    case 50:
                        _nextStreakCount = 100;
                        break;
                    case >= 100:
                        _nextStreakCount += 100;
                        break;
                }
            }
            else if (SettingsManager.Settings.NoteStreakFrequency.ValueEquals(NoteStreakFrequencyMode.Sparse))
            {
                switch (_nextStreakCount)
                {
                    case 0:
                        _nextStreakCount = 50;
                        break;
                    case 50:
                        _nextStreakCount = 100;
                        break;
                    case 100:
                        _nextStreakCount = 250;
                        break;
                    case >= 250:
                        _nextStreakCount += 250;
                        break;
                }
            }
            else
            {
                _nextStreakCount = int.MaxValue;
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