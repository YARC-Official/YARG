#nullable enable
using System;
using System.Threading;
using ManagedBass;
using ManagedBass.Mix;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    /// Describes a microphone monitor stream and moves or resets that stream when needed.
    /// </summary>
    internal sealed class BassMonitorSource
    {
        private readonly Action _resetEffects;

        public int Handle { get; }

        public BassMonitorSource(int handle, Action resetEffects)
        {
            Handle = handle;
            _resetEffects = resetEffects;
        }

        public bool TryGetDevice(out int deviceId)
        {
            deviceId = Bass.ChannelGetDevice(Handle);
            if (deviceId >= 0)
            {
                return true;
            }

            YargLogger.LogFormatError("Failed to get monitor source device: {0}", Bass.LastError);
            return false;
        }

        public bool TryMoveToDevice(int deviceId)
        {
            if (!TryGetDevice(out int currentDeviceId))
            {
                return false;
            }

            if (currentDeviceId == deviceId)
            {
                return true;
            }

            if (Bass.ChannelSetDevice(Handle, deviceId))
            {
                return true;
            }

            YargLogger.LogFormatError("Failed to move monitor source to BASS device {0}: {1}", deviceId,
                Bass.LastError);
            return false;
        }

        public bool ResetToLive()
        {
            if (!BassMix.SplitStreamReset(Handle, 0))
            {
                YargLogger.LogFormatError("Failed to reset monitor source: {0}", Bass.LastError);
                return false;
            }

            _resetEffects();
            return true;
        }
    }

    /// <summary>
    /// Registration token for one monitor source. Disposal synchronously detaches the source.
    /// </summary>
    internal sealed class BassMonitor : IDisposable
    {
        private BassAudioRouter? _owner;
        private Action? _attached;
        private Action? _detached;

        internal BassMonitorSource Source     { get; }
        internal bool              IsAttached { get; private set; }
        public   double            Volume     { get; private set; }

        internal BassMonitor(BassAudioRouter owner, BassMonitorSource source, double volume,
            Action? attached, Action? detached)
        {
            _owner = owner;
            Source = source;
            Volume = volume;
            _attached = attached;
            _detached = detached;
        }

        public void SetVolume(double volume)
        {
            Volume = volume;
            Volatile.Read(ref _owner)?.SetMonitorVolume(this, volume);
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.Remove(this);
        }

        internal void MarkAttached()
        {
            IsAttached = true;
            InvokeLifecycleCallback(_attached);
        }

        internal void MarkDetached()
        {
            if (!IsAttached)
            {
                return;
            }

            IsAttached = false;
            InvokeLifecycleCallback(_detached);
        }

        internal void InvalidateOwner()
        {
            Interlocked.Exchange(ref _owner, null);
            MarkDetached();
        }

        private static void InvokeLifecycleCallback(Action? callback)
        {
            try
            {
                callback?.Invoke();
            }
            catch (Exception exception)
            {
                YargLogger.LogException(exception, "Monitor monitor lifecycle callback failed");
            }
        }
    }
}
