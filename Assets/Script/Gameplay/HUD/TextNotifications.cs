using DG.Tweening;
using System;
using System.Collections;
using System.Linq;
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

    public enum VocalStreakFrequencyMode
    {
        Frequent,
        Sparse,
        Disabled
    }

    public class TextNotifications : MonoBehaviour
    {
        [SerializeField]
        private float _animLength = 2f;
        [SerializeField]
        private float _animBaseToPeakInterval = 0.167f;
        [SerializeField]
        private float _animPeakToValleyInterval = 0.167f;
        [SerializeField]
        private float _animPeakScale = 1.1f;
        [SerializeField]
        private float _animValleyScale = 1f;

        private float _animHoldInterval => _animLength - 2f * (_animBaseToPeakInterval + _animPeakToValleyInterval);

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
        [SerializeField]
        private bool _isVocals;

        private int _streak;
        private int _nextStreakCount;

        private Coroutine _coroutine;

        private readonly TextNotificationQueue _notificationQueue = new();

        public bool ShouldShowNoteStreakNotification =>
                (!_isVocals && SettingsManager.Settings.NoteStreakFrequency.Value != NoteStreakFrequencyMode.Disabled)
            || (_isVocals && SettingsManager.Settings.VocalStreakFrequency.Value != VocalStreakFrequencyMode.Disabled);

        private readonly TextNotificationType[] _highPriorityNotifications = {
            TextNotificationType.FullCombo,
            TextNotificationType.StrongFinish
        };

        private Sequence _animationSequence => DOTween.Sequence()
                .Append(DOTween.Sequence()
                    .Append(_containerRect
                        .DOScale(_animPeakScale, _animBaseToPeakInterval)
                        .SetEase(Ease.OutCirc))
                    .Append(_containerRect

                        .DOScale(_animValleyScale, _animPeakToValleyInterval)
                        .SetEase(Ease.InOutSine))
                    .AppendInterval(_animHoldInterval))
                .Append(DOTween.Sequence()
                    .Append(_containerRect
                        .DOScale(_animPeakScale, _animPeakToValleyInterval)
                        .SetEase(Ease.InOutSine))
                    .Append(_containerRect
                        .DOScale(0f, _animBaseToPeakInterval)
                        .SetEase(Ease.InCirc)));


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
            ShowNotification(TextNotificationType.NewHighScore);
        }

        public void ShowFullCombo()
        {
            ShowNotification(TextNotificationType.FullCombo);
        }

        public void ShowStrongFinish()
        {
            ShowNotification(TextNotificationType.StrongFinish);
        }

        public void ShowHotStart()
        {
            ShowNotification(TextNotificationType.HotStart);
        }

        public void ShowBassGroove()
        {
            ShowNotification(TextNotificationType.BassGroove);
        }

        public void ShowStarPowerReady()
        {
            ShowNotification(TextNotificationType.StarPowerReady);
        }

        // All of these notifications imply that the player got an AWESOME rating. Avoid displaying that if one of these is already queued.
        private TextNotificationType[] incompatibleNotifications = new[]
        {
                TextNotificationType.FullCombo,
                TextNotificationType.StrongFinish,
                TextNotificationType.HotStart,
                TextNotificationType.PhraseStreak
         };

        public void ShowVocalPhraseResult(string result, int streak)
        {
            if (!isActiveAndEnabled) return;

            if (incompatibleNotifications.Contains(_notificationQueue.Current ?? TextNotificationType.VocalPhraseResult)) return;

            _notificationQueue.Enqueue(new TextNotification(TextNotificationType.VocalPhraseResult, result));
        }

        public void ShowNotification(TextNotificationType notificationType)
        {
            var allowSuppress = Array.IndexOf(_highPriorityNotifications, notificationType) == -1;

            if (allowSuppress && !isActiveAndEnabled) return;

            _notificationQueue.Enqueue(new TextNotification(notificationType));
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
                NextStreakNotification();
            }

            // Update the streak
            _streak = streak;

            // Queue the note streak notification
            if (_streak >= _nextStreakCount)
            {
                if (!ShouldShowNoteStreakNotification)
                {
                    return;
                }

                var type = _isVocals ? TextNotificationType.PhraseStreak : TextNotificationType.NoteStreak;
                _notificationQueue.Enqueue(new TextNotification(type, _nextStreakCount));

                NextStreakNotification();
            }
        }

        private void Update()
        {
            // Never update this if text notifications are disabled
            if (SettingsManager.Settings.DisableTextNotifications.Value && !_isVocals) return;

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
            yield return _animationSequence.WaitForCompletion();

            _text.text = string.Empty;
            _coroutine = null;
        }

        private void NextStreakNotification()
        {
            if (_isVocals)
            {
                NextPhraseStreakNotification();
            }
            else
            {
                NextNoteStreakNotification();
            }
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

        private void NextPhraseStreakNotification()
        {
            if (SettingsManager.Settings.VocalStreakFrequency.Value == VocalStreakFrequencyMode.Disabled)
            {
                _nextStreakCount = int.MaxValue;
                return;
            }

            switch (_nextStreakCount)
            {
                case 0:
                    _nextStreakCount = 5;
                    break;
                case 5:
                    _nextStreakCount = 10;
                    break;
                case >= 10 when SettingsManager.Settings.VocalStreakFrequency.Value == VocalStreakFrequencyMode.Frequent:
                    _nextStreakCount += 10;
                    break;
                case 10 when SettingsManager.Settings.VocalStreakFrequency.Value == VocalStreakFrequencyMode.Sparse:
                    _nextStreakCount = 25;
                    break;
                case >= 25 when SettingsManager.Settings.VocalStreakFrequency.Value == VocalStreakFrequencyMode.Sparse:
                    _nextStreakCount += 25;
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
                TextNotificationType.FullCombo => _starpowerColor,
                TextNotificationType.BassGroove => _grooveColor,
                TextNotificationType.StarPowerReady => _starpowerColor,
                _ => _defaultColor,
            };
        }
    }
}