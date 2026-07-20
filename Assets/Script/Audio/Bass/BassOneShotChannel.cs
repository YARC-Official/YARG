using System;
using System.Collections.Generic;
using ManagedBass;
using ManagedBass.Mix;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    /// Schedules a one-shot sample directly in the final BASS playback mixer.
    /// </summary>
    /// <remarks>
    /// Scheduled hits do not need independent overlapping voices. Retriggering rewinds the single
    /// sample stream, keeping this channel small while retaining sample-accurate BASS syncs.
    /// </remarks>
    internal sealed class BassOneShotChannel : OneShotChannel
    {
        private readonly int _outputMixerHandle;
        private readonly int _tempoStreamHandle;
        private readonly int _sampleStreamHandle;
        private readonly double[] _scheduledPlays;
        private readonly object _syncLock = new();
        private readonly SyncProcedure _playSampleSync;
        private readonly Func<long, double> _getSongPosition;
        private readonly Func<float> _getSpeed;
        private readonly double _outputLeadTime;

        private int _syncHandle;
        private int _nextScheduledPlay;
        private int _generation;
        private long _scheduleStartOutputPosition;
        private double _scheduleStartSongPosition;
        private float _scheduleSpeed;
        private bool _enabled = true;
        private bool _disposed;

        internal event Action<BassOneShotChannel> Disposed;

        public BassOneShotChannel(int outputMixerHandle, int tempoStreamHandle,
            int sampleStream, IReadOnlyList<double> scheduledPlays,
            Func<long, double> getSongPosition, Func<float> getSpeed, double outputLeadTime)
        {
            _outputMixerHandle = outputMixerHandle;
            _tempoStreamHandle = tempoStreamHandle;
            _sampleStreamHandle = sampleStream;
            _getSongPosition = getSongPosition ?? throw new ArgumentNullException(nameof(getSongPosition));
            _getSpeed = getSpeed ?? throw new ArgumentNullException(nameof(getSpeed));
            _outputLeadTime = Math.Max(0, outputLeadTime);
            _scheduledPlays = CopyAndSort(scheduledPlays);
            _playSampleSync = OnPlaySampleSync;

            RebuildPendingSyncs();
        }

        public override void SetVolume(double volume)
        {
            lock (_syncLock)
            {
                if (!_disposed && _sampleStreamHandle != 0 &&
                    !Bass.ChannelSetAttribute(_sampleStreamHandle, ChannelAttribute.Volume, volume))
                {
                    LogBassError("Failed to set one-shot sample volume: {0}!");
                }
            }
        }

        public override void SetEnabled(bool enabled)
        {
            lock (_syncLock)
            {
                if (!_disposed)
                {
                    _enabled = enabled;
                }
            }
        }

        /// <summary>
        /// Invalidates callbacks and removes the sample before the playback graph is reset.
        /// </summary>
        internal void PrepareForSeek()
        {
            RemoveSync(InvalidateSchedule());
            if (_sampleStreamHandle != 0)
            {
                BassMix.MixerRemoveChannel(_sampleStreamHandle);
            }
        }

        internal void ResetAfterSeek()
        {
            RebuildPendingSyncs();
        }

        internal void ResetAfterSpeedChange()
        {
            RemoveSync(InvalidateSchedule());
            RebuildPendingSyncs();
        }

        private void RebuildPendingSyncs()
        {
            lock (_syncLock)
            {
                if (_disposed || _sampleStreamHandle == 0)
                {
                    return;
                }

                long outputPosition = Bass.ChannelGetPosition(_outputMixerHandle, PositionFlags.Decode);
                long tempoPosition = Bass.ChannelGetPosition(_tempoStreamHandle, PositionFlags.Decode);
                if (outputPosition < 0 || tempoPosition < 0)
                {
                    LogBassError("Failed to read one-shot scheduling position: {0}!");
                    return;
                }

                _scheduleStartOutputPosition = outputPosition;
                _scheduleStartSongPosition = _getSongPosition(tempoPosition);
                _scheduleSpeed = Math.Max(0.0001f, _getSpeed());
                _nextScheduledPlay = FindFirstScheduledPlay(_scheduleStartSongPosition);
                ArmNextSync();
            }
        }

        private void ArmNextSync()
        {
            if (_disposed || _nextScheduledPlay >= _scheduledPlays.Length)
            {
                return;
            }

            double outputDelay =
                (_scheduledPlays[_nextScheduledPlay] - _scheduleStartSongPosition) / _scheduleSpeed -
                _outputLeadTime;
            if (_outputLeadTime > 0 && outputDelay <= 0)
            {
                _nextScheduledPlay++;
                ArmNextSync();
                return;
            }
            long targetPosition = _scheduleStartOutputPosition +
                Bass.ChannelSeconds2Bytes(_outputMixerHandle, outputDelay);

            _syncHandle = Bass.ChannelSetSync(_outputMixerHandle,
                SyncFlags.Position | SyncFlags.Mixtime | SyncFlags.Onetime,
                targetPosition, _playSampleSync, new IntPtr(_generation));
            if (_syncHandle == 0)
            {
                LogBassError("Failed to schedule one-shot sync: {0}!");
            }
        }

        private void OnPlaySampleSync(int handle, int channel, int data, IntPtr user)
        {
            lock (_syncLock)
            {
                if (_disposed || user.ToInt32() != _generation)
                {
                    return;
                }

                // Current sync is one-shot and BASS removes it after this callback.
                _syncHandle = 0;
                if (_enabled)
                {
                    PlaySample();
                }
                _nextScheduledPlay++;
                ArmNextSync();
            }
        }

        private void PlaySample()
        {
            BassMix.MixerRemoveChannel(_sampleStreamHandle);
            if (!Bass.ChannelSetPosition(_sampleStreamHandle, 0, PositionFlags.Bytes) ||
                !BassMix.MixerAddChannel(_outputMixerHandle, _sampleStreamHandle,
                    BassFlags.MixerChanNoRampin))
            {
                LogBassError("Failed to play one-shot sample: {0}!");
            }
        }

        private int InvalidateSchedule()
        {
            lock (_syncLock)
            {
                _generation++;
                int syncHandle = _syncHandle;
                _syncHandle = 0;
                return syncHandle;
            }
        }

        private void RemoveSync(int syncHandle)
        {
            if (syncHandle != 0)
            {
                Bass.ChannelRemoveSync(_outputMixerHandle, syncHandle);
            }
        }

        private int FindFirstScheduledPlay(double songPosition)
        {
            int start = 0;
            int end = _scheduledPlays.Length;
            while (start < end)
            {
                int middle = start + (end - start) / 2;
                if (_scheduledPlays[middle] < songPosition)
                {
                    start = middle + 1;
                }
                else
                {
                    end = middle;
                }
            }
            return start;
        }

        private static double[] CopyAndSort(IReadOnlyList<double> scheduledPlays)
        {
            if (scheduledPlays == null)
            {
                throw new ArgumentNullException(nameof(scheduledPlays));
            }

            var copy = new double[scheduledPlays.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = scheduledPlays[i];
            }
            Array.Sort(copy);
            return copy;
        }

        public override void Dispose()
        {
            int syncHandle;
            lock (_syncLock)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
                _generation++;
                syncHandle = _syncHandle;
                _syncHandle = 0;
            }

            // Native cleanup stays outside _syncLock. BASS may wait for a callback that is itself
            // waiting to acquire the lock.
            RemoveSync(syncHandle);
            if (_sampleStreamHandle != 0)
            {
                BassMix.MixerRemoveChannel(_sampleStreamHandle);
                Bass.StreamFree(_sampleStreamHandle);
            }

            var disposed = Disposed;
            Disposed = null;
            disposed?.Invoke(this);
        }

        private static void LogBassError(string format)
        {
            YargLogger.LogFormatError(format, Bass.LastError);
        }
    }
}
