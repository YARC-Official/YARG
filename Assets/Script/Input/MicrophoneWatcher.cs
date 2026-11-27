using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Input
{
    public static class MicrophoneWatcher
    {
        private const int POLL_INTERVAL_MS = 2000;

        private static HashSet<MicId> _previous = new();
        private static bool _running;

        public static event Action<MicId> MicrophoneAdded;
        public static event Action<MicId> MicrophoneRemoved;
        public static event Action MicrophonesChanged;

        public static string GetDisplayName(string name, MicId id, IEnumerable<MicId> connected = null)
        {
            if (id.EndpointId == null)
            {
                return name;
            }

            int count = 0;
            int number = 1;
            foreach (var other in connected ?? _previous)
            {
                if (other.BaseName == id.BaseName && other.Channel == id.Channel)
                {
                    count++;
                    if (string.CompareOrdinal(other.EndpointId, id.EndpointId) < 0)
                    {
                        number++;
                    }
                }
            }

            return count > 1 ? $"{name} ({number})" : name;
        }

        public static void Start()
        {
            if (_running)
            {
                return;
            }

            _running = true;
            PollLoop(Application.exitCancellationToken).Forget();
        }

        private static async UniTaskVoid PollLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var current = await UniTask.RunOnThreadPool(GetConnectedMicrophones, cancellationToken: token);
                    if (current is not null)
                    {
                        ApplyChanges(current);
                    }

                    await UniTask.Delay(POLL_INTERVAL_MS, DelayType.Realtime, cancellationToken: token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _previous.Clear();
                _running = false;
            }
        }

        private static HashSet<MicId> GetConnectedMicrophones()
        {
            try
            {
                return GlobalAudioHandler.GetAllInputDevices().Select(device => device.Id).ToHashSet();
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Failed to poll microphones");
                return null;
            }
        }

        private static void ApplyChanges(HashSet<MicId> current)
        {
            var removed = _previous.Except(current).ToList();
            var added = current.Except(_previous).ToList();
            _previous = current;

            foreach (var id in removed)
            {
                MicrophoneRemoved?.Invoke(id);
            }

            foreach (var id in added)
            {
                MicrophoneAdded?.Invoke(id);
            }

            if (removed.Count > 0 || added.Count > 0)
            {
                MicrophonesChanged?.Invoke();
            }
        }
    }
}
