using System;
using System.Collections.Generic;

namespace YARG.Gameplay.HUD
{
    public class TextNotificationQueue
    {
        private List<TextNotification> _notificationQueue = new();

        public void Enqueue(TextNotification notification)
        {
            //if the queue is not empty, we remove the notifications that can be skipped
            if (_notificationQueue.Count > 0)
            {
                _notificationQueue.RemoveAll(x => x.Type == TextNotificationType.NoteStreak);
            }

            //if the queue is still not empty, don't queue if it's a note streak (for example bass groove + note streak on same note)
            if (_notificationQueue.Count > 0 && notification.Type == TextNotificationType.NoteStreak)
            {
                return;
            }

            //try to show FullCombo as soon as possible if there's multiple notifications
            if (notification.Type == TextNotificationType.FullCombo)
            {
                _notificationQueue.Insert(0, notification);
            }
            else
            {
                _notificationQueue.Add(notification);
            }
        }

        public TextNotification Dequeue()
        {
            if (_notificationQueue.Count > 0)
            {
                var first = _notificationQueue[0];
                _notificationQueue.RemoveAt(0);
                return first;
            }

            throw new InvalidOperationException("TextNoficiationQueue is empty");
        }

        public void Clear()
        {
            _notificationQueue.Clear();
        }

        public int Count => _notificationQueue.Count;
    }

    public readonly struct TextNotification
    {
        public TextNotificationType Type { get; }
        public string Text { get; }

        public TextNotification(TextNotificationType type, string text)
        {
            Type = type;
            Text = text;
        }
    }

    public enum TextNotificationType
    {
        NoteStreak,
        NewHighScore,
        BassGroove,
        FullCombo,
        StarPowerReady,
        HotStart,
        StrongFinish,
        ActivateToSave,
        Saved,
    }
}