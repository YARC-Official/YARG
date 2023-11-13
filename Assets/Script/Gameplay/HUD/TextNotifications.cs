using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YARG.Settings;

namespace YARG.Gameplay.HUD
{
    public class TextNotifications : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _text;

        private int _streak;
        private int _nextStreakCount;

        private string _nextNotification;
        private bool   _notificationPending;

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
                _nextNotification = $"{_nextStreakCount}-NOTE STREAK";
                _notificationPending = true;
                NextNoteStreakNotification();
            }
        }

        private void Update()
        {
            // Never update this if text notifications are disabled
            if (SettingsManager.Settings.DisableTextNotifications.Data) return;

            if (_coroutine == null && _notificationPending)
            {
                _coroutine = StartCoroutine(ShowNextNotification());
            }
        }

        private IEnumerator ShowNextNotification()
        {
            _text.text = _nextNotification;

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
            _notificationPending = false;
        }

        private void NextNoteStreakNotification()
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

        public void ForceReset()
        {
            _notificationPending = false;
            _nextStreakCount = 0;
            _streak = 0;
        }
    }
}