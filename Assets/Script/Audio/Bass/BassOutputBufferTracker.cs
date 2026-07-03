using System;
using System.Threading;
using ManagedBass;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
#nullable enable
    internal sealed class BassOutputBufferTracker : IDisposable
    {
        private readonly DSPProcedure _dspProcedure;
        private int _channelHandle;
        private int _dspHandle;
        private long _basePlayedBytes;
        private long _producedBytesSinceReset;
        private int _hasSeenCallback;

        public BassOutputBufferTracker(int channelHandle)
        {
            _channelHandle = channelHandle;
            _dspProcedure = OnDsp;

            if (channelHandle == 0)
            {
                return;
            }

            _dspHandle = Bass.ChannelSetDSP(channelHandle, _dspProcedure, IntPtr.Zero, 0);
            if (_dspHandle == 0)
            {
                YargLogger.LogFormatError("Failed to add output buffer tracker DSP: {0}!", Bass.LastError);
                return;
            }

            ResetToCurrentPosition();
        }

        public void Reset(long currentPlayedBytes)
        {
            Interlocked.Exchange(ref _basePlayedBytes, Math.Max(0, currentPlayedBytes));
            Interlocked.Exchange(ref _producedBytesSinceReset, 0);
            Volatile.Write(ref _hasSeenCallback, 0);
        }

        public void ResetToCurrentPosition()
        {
            long currentPlayedBytes = 0;
            if (_channelHandle != 0)
            {
                long playedBytes = Bass.ChannelGetPosition(_channelHandle, PositionFlags.Bytes);
                if (playedBytes >= 0)
                {
                    currentPlayedBytes = playedBytes;
                }
            }

            Reset(currentPlayedBytes);
        }

        public bool TryGetRemainingSeconds(out double seconds)
        {
            seconds = 0;

            if (_channelHandle == 0 || _dspHandle == 0 || Volatile.Read(ref _hasSeenCallback) == 0)
            {
                return false;
            }

            long playedBytes = Bass.ChannelGetPosition(_channelHandle, PositionFlags.Bytes);
            if (playedBytes < 0)
            {
                return false;
            }

            long basePlayedBytes = Interlocked.Read(ref _basePlayedBytes);
            long playedSinceReset = playedBytes - basePlayedBytes;
            if (playedSinceReset < 0)
            {
                return false;
            }

            long producedBytes = Interlocked.Read(ref _producedBytesSinceReset);
            long remainingBytes = producedBytes - playedSinceReset;
            if (remainingBytes < 0)
            {
                remainingBytes = 0;
            }

            double remainingSeconds = Bass.ChannelBytes2Seconds(_channelHandle, remainingBytes);
            if (remainingSeconds < 0 || double.IsNaN(remainingSeconds) || double.IsInfinity(remainingSeconds))
            {
                return false;
            }

            seconds = remainingSeconds;
            return true;
        }

        public void Dispose()
        {
            int dspHandle = Interlocked.Exchange(ref _dspHandle, 0);
            int channelHandle = Interlocked.Exchange(ref _channelHandle, 0);
            if (channelHandle != 0 && dspHandle != 0)
            {
                Bass.ChannelRemoveDSP(channelHandle, dspHandle);
            }
        }

        private void OnDsp(int handle, int channel, IntPtr buffer, int length, IntPtr user)
        {
            Interlocked.Add(ref _producedBytesSinceReset, length);
            Volatile.Write(ref _hasSeenCallback, 1);
        }
    }
#nullable disable
}
