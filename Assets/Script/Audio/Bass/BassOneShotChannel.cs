using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using ManagedBass;
using ManagedBass.Mix;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    internal sealed class BassOneShotChannel : OneShotChannel
    {
        private const int MAX_ACTIVE_SAMPLES = 64;
        private const int MAX_SCHEDULES = 64;
        private const int DECODE_BUFFER_SIZE = 4096;

        private readonly object _stateLock = new();

        private readonly int _playbackStreamHandle;
        private readonly int _sampleRate;
        private readonly int _channelCount;
        private readonly int _sampleFrameCount;

        private readonly Func<long, double> _getPlaybackTime;
        private readonly float[]            _sample;
        private readonly int                _dspHandle;

        private readonly ConcurrentQueue<byte> _immediatePlays = new();
        private readonly int[] _activeSampleFrames = new int[MAX_ACTIVE_SAMPLES];

        private ScheduleState _schedule =
            new(MAX_SCHEDULES);

        private ScheduleState _callbackSchedule;
        private int _nextScheduledEvent;

        private int _transportGeneration;
        private int _callbackTransportGeneration = -1;
        private long _previousEndPosition;

        private int _activeSampleCount;
        private float _volume = 1;
        private bool _disposed;

        internal event Action<BassOneShotChannel> Disposed;

        public BassOneShotChannel(int playbackStreamHandle, int sampleStream)
            : this(
                playbackStreamHandle,
                sampleStream,
                position => Bass.ChannelBytes2Seconds(
                    playbackStreamHandle,
                    position))
        {
        }

        public BassOneShotChannel(
            int playbackStreamHandle,
            int sampleStream,
            Func<long, double> getPlaybackTime)
        {
            _playbackStreamHandle = playbackStreamHandle;
            _getPlaybackTime = getPlaybackTime ??
                throw new ArgumentNullException(nameof(getPlaybackTime));

            var info = Bass.ChannelGetInfo(playbackStreamHandle);
            if ((info.Flags & BassFlags.Float) == 0)
            {
                Bass.StreamFree(sampleStream);

                throw new ArgumentException(
                    "Playback stream must use float sample data.",
                    nameof(playbackStreamHandle));
            }

            _sampleRate = info.Frequency;
            _channelCount = info.Channels;
            _sample = DecodeSample(sampleStream) ?? Array.Empty<float>();
            _sampleFrameCount = _sample.Length / _channelCount;

            if (_sampleFrameCount == 0)
            {
                return;
            }

            DSPProcedure callback = MixSamples;
            _previousEndPosition = Math.Max(
                0,
                Bass.ChannelGetPosition(
                    playbackStreamHandle,
                    PositionFlags.Decode));

            _dspHandle = Bass.ChannelSetDSP(
                playbackStreamHandle,
                callback);

            if (_dspHandle == 0)
            {
                LogBassError("Failed to attach one-shot DSP: {0}!");
            }
        }

        private bool IsAvailable => !_disposed && _dspHandle != 0;

        public override void Play()
        {
            lock (_stateLock)
            {
                if (IsAvailable)
                {
                    _immediatePlays.Enqueue(0);
                }
            }
        }

        public override void Schedule(double songTime)
        {
            lock (_stateLock)
            {
                if (!IsAvailable)
                {
                    return;
                }

                var schedule = Volatile.Read(ref _schedule).Add(songTime);
                Volatile.Write(ref _schedule, schedule);
            }
        }

        public override void SetVolume(double volume)
        {
            Volatile.Write(ref _volume, (float) volume);
        }

        public override void ClearSchedule()
        {
            lock (_stateLock)
            {
                if (!_disposed)
                {
                    Volatile.Write(
                        ref _schedule,
                        new ScheduleState(MAX_SCHEDULES));
                }
            }
        }

        public void ResetTransport()
        {
            Interlocked.Increment(ref _transportGeneration);
        }

        private unsafe void MixSamples(
            int _,
            int channel,
            IntPtr buffer,
            int length,
            IntPtr __)
        {
            int frameCount =
                length / (sizeof(float) * _channelCount);

            if (frameCount <= 0 ||
                !TryGetPlaybackWindow(
                    channel,
                    out double startTime,
                    out double endTime))
            {
                return;
            }

            float* output = (float*) buffer;
            float volume = Volatile.Read(ref _volume);

            MixActiveSamples(output, frameCount, volume);
            MixImmediateSamples(output, frameCount, volume);
            MixScheduledSamples(
                output,
                frameCount,
                volume,
                GetCallbackSchedule(startTime),
                startTime,
                endTime);
        }

        private bool TryGetPlaybackWindow(
            int channel,
            out double startTime,
            out double endTime)
        {
            startTime = 0;
            endTime = 0;

            long endPosition =
                Bass.ChannelGetPosition(channel, PositionFlags.Decode);

            if (endPosition < 0)
            {
                return false;
            }

            int generation = Volatile.Read(ref _transportGeneration);
            if (_callbackTransportGeneration != generation)
            {
                ResetCallbackState(generation);
            }

            long startPosition = _previousEndPosition;
            _previousEndPosition = endPosition;

            if (endPosition <= startPosition)
            {
                return false;
            }

            startTime = _getPlaybackTime(startPosition);
            endTime = _getPlaybackTime(endPosition);

            return endTime > startTime;
        }

        private void ResetCallbackState(int generation)
        {
            _callbackTransportGeneration = generation;
            _previousEndPosition = 0;
            _callbackSchedule = null;
            _nextScheduledEvent = 0;
            _activeSampleCount = 0;
        }

        private ScheduleState GetCallbackSchedule(double playbackStart)
        {
            ScheduleState schedule = Volatile.Read(ref _schedule);

            if (!ReferenceEquals(schedule, _callbackSchedule))
            {
                _callbackSchedule = schedule;
                _nextScheduledEvent = FindFirstEvent(
                    schedule.Events,
                    Volatile.Read(ref schedule.Count),
                    playbackStart);
            }

            return schedule;
        }

        private unsafe void MixScheduledSamples(
            float* output,
            int frameCount,
            float volume,
            ScheduleState schedule,
            double playbackStart,
            double playbackEnd)
        {
            int eventCount = Volatile.Read(ref schedule.Count);
            double duration = playbackEnd - playbackStart;

            while (_nextScheduledEvent < eventCount)
            {
                double eventTime =
                    schedule.Events[_nextScheduledEvent];

                if (eventTime >= playbackEnd)
                {
                    return;
                }

                _nextScheduledEvent++;

                if (eventTime < playbackStart)
                {
                    continue;
                }

                double progress =
                    (eventTime - playbackStart) / duration;

                int startFrame = Math.Clamp(
                    (int) Math.Round(progress * frameCount),
                    0,
                    frameCount - 1);

                StartSample(
                    output,
                    frameCount,
                    startFrame,
                    volume);
            }
        }

        private unsafe void MixActiveSamples(
            float* output,
            int frameCount,
            float volume)
        {
            int writeIndex = 0;

            for (int i = 0; i < _activeSampleCount; i++)
            {
                int sampleFrame = _activeSampleFrames[i];

                MixSample(
                    output,
                    frameCount,
                    startFrame: 0,
                    volume,
                    ref sampleFrame);

                if (sampleFrame < _sampleFrameCount)
                {
                    _activeSampleFrames[writeIndex++] = sampleFrame;
                }
            }

            _activeSampleCount = writeIndex;
        }

        private unsafe void MixImmediateSamples(
            float* output,
            int frameCount,
            float volume)
        {
            for (int i = 0;
                 i < MAX_ACTIVE_SAMPLES &&
                 _immediatePlays.TryDequeue(out _);
                 i++)
            {
                StartSample(
                    output,
                    frameCount,
                    startFrame: 0,
                    volume);
            }
        }

        private unsafe void StartSample(
            float* output,
            int frameCount,
            int startFrame,
            float volume)
        {
            int sampleFrame = 0;

            MixSample(
                output,
                frameCount,
                startFrame,
                volume,
                ref sampleFrame);

            if (sampleFrame < _sampleFrameCount &&
                _activeSampleCount < MAX_ACTIVE_SAMPLES)
            {
                _activeSampleFrames[_activeSampleCount++] = sampleFrame;
            }
        }

        private unsafe void MixSample(
            float* output,
            int outputFrames,
            int startFrame,
            float volume,
            ref int sampleFrame)
        {
            int framesToMix = Math.Min(
                outputFrames - startFrame,
                _sampleFrameCount - sampleFrame);

            int source = sampleFrame * _channelCount;
            int destination = startFrame * _channelCount;
            int valuesRemaining = framesToMix * _channelCount;

            while (valuesRemaining-- > 0)
            {
                output[destination++] += _sample[source++] * volume;
            }

            sampleFrame += framesToMix;
        }

        private static int FindFirstEvent(
            double[] events,
            int eventCount,
            double playbackTime)
        {
            int low = 0;
            int high = eventCount;

            while (low < high)
            {
                int middle = low + (high - low) / 2;

                if (events[middle] < playbackTime)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }

        private float[] DecodeSample(int streamHandle)
        {
            int converter = BassMix.CreateMixerStream(
                _sampleRate,
                _channelCount,
                BassFlags.Float |
                BassFlags.Decode |
                BassFlags.MixerEnd);

            if (converter == 0)
            {
                LogBassError(
                    "Failed to create one-shot sample converter: {0}!");

                Bass.StreamFree(streamHandle);
                return null;
            }

            try
            {
                if (!BassMix.MixerAddChannel(
                        converter,
                        streamHandle,
                        BassFlags.MixerChanNoRampin))
                {
                    LogBassError(
                        "Failed to add one-shot sample to converter: {0}!");

                    return null;
                }

                var samples = new List<float>();
                var buffer = new float[DECODE_BUFFER_SIZE];

                int bytesRead;
                while ((bytesRead = Bass.ChannelGetData(
                           converter,
                           buffer,
                           buffer.Length * sizeof(float))) > 0)
                {
                    int sampleCount = bytesRead / sizeof(float);

                    for (int i = 0; i < sampleCount; i++)
                    {
                        samples.Add(buffer[i]);
                    }
                }

                if (bytesRead < 0 && Bass.LastError != Errors.Ended)
                {
                    LogBassError(
                        "Failed to decode one-shot sample: {0}!");
                }

                return samples.Count == 0
                    ? null
                    : samples.ToArray();
            }
            finally
            {
                Bass.StreamFree(converter);
                Bass.StreamFree(streamHandle);
            }
        }

        public override void Dispose()
        {
            Action<BassOneShotChannel> disposed;

            lock (_stateLock)
            {
                if (_disposed)
                {
                    return;
                }

                RemoveDsp();

                _disposed = true;
                disposed = Disposed;
                Disposed = null;
            }

            disposed?.Invoke(this);
        }

        private void RemoveDsp()
        {
            if (_dspHandle == 0 ||
                Bass.ChannelRemoveDSP(
                    _playbackStreamHandle,
                    _dspHandle) ||
                Bass.LastError == Errors.Handle)
            {
                return;
            }

            LogBassError("Failed to remove one-shot DSP: {0}!");
        }

        internal void PlaybackStreamDisposed()
        {
            lock (_stateLock)
            {
                _disposed = true;
                Disposed = null;
            }
        }

        private static void LogBassError(string format)
        {
            YargLogger.LogFormatError(format, Bass.LastError);
        }

        private sealed class ScheduleState
        {
            public readonly double[] Events;
            public          int      Count;

            public ScheduleState(int capacity)
            {
                Events = new double[capacity];
            }

            public ScheduleState Add(double songTime)
            {
                int count = Volatile.Read(ref Count);

                if (CanAppend(songTime, count))
                {
                    Events[count] = songTime;
                    Volatile.Write(ref Count, count + 1);
                    return this;
                }

                int index = Array.BinarySearch(Events, 0, count, songTime);
                if (index < 0)
                {
                    index = ~index;
                }

                var replacement = new ScheduleState(
                    Math.Max(Events.Length * 2, count + 1));

                Array.Copy(Events, 0, replacement.Events, 0, index);
                replacement.Events[index] = songTime;
                Array.Copy(
                    Events,
                    index,
                    replacement.Events,
                    index + 1,
                    count - index);

                replacement.Count = count + 1;
                return replacement;
            }

            private bool CanAppend(double songTime, int count)
            {
                return count < Events.Length &&
                    (count == 0 || songTime >= Events[count - 1]);
            }
        }
    }
}