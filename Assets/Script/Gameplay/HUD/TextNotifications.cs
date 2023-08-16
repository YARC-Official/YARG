using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace YARG.Gameplay.HUD
{
    public class TextNotifications : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _text;

        private int _streak;
        private int _nextStreakCount;

        private readonly Queue<string> _notificationQueue = new();

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
                _notificationQueue.Enqueue($"{_nextStreakCount}-NOTE STREAK");
                NextNoteStreakNotification();
            }
        }

        private void Update()
        {
            if (_coroutine == null && _notificationQueue.Count > 0)
            {
                _coroutine = StartCoroutine(ShowNextNotification());
            }
        }

        private IEnumerator ShowNextNotification()
        {
            string notification = _notificationQueue.Dequeue();
            _text.text = notification;

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
            // We could make this more complex if we wanted to
            _nextStreakCount += 100;
        }

        public void ForceReset()
        {
            _notificationQueue.Clear();
        }
    }
}