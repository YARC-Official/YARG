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
    /// <summary>
    /// Mixes immediate and scheduled instances of one sample into a float playback stream.
    /// </summary>
    internal sealed class BassOneShotChannel : OneShotChannel
    {
        private const int MAX_ACTIVE_SAMPLES = 64;

        private struct ActiveSample
        {
            public float[] Data;
            public int Frame;
        }

        private sealed class ScheduleState
        {
            public readonly double[] Events;
            public int Count;

            public ScheduleState(int capacity)
            {
                Events = new double[capacity];
            }
        }

        private readonly int _playbackStreamHandle;
        private readonly int _sampleRate;
        private readonly int _channelCount;
        private readonly Func<long, double> _getPlaybackTime;
        private readonly float[] _sample;
        private readonly DSPProcedure _callback;
        private readonly int _dspHandle;
        private readonly ConcurrentQueue<byte> _immediatePlays = new();
        private readonly ActiveSample[] _activeSamples = new ActiveSample[MAX_ACTIVE_SAMPLES];

        private ScheduleState _state = new(64);
        private ScheduleState _activeState;
        private int _transportGeneration;
        private int _activeTransportGeneration = -1;
        private long _previousEndPosition;
        private int _nextEvent;
        private int _activeSampleCount;
        private float _volume = 1;
        private bool _disposed;

        internal event Action<BassOneShotChannel> Disposed;

        /// <summary>
        /// Attaches to a stream using its own position in seconds as scheduling time.
        /// </summary>
        public BassOneShotChannel(int playbackStreamHandle, int sampleStream)
            : this(playbackStreamHandle, sampleStream,
                position => Bass.ChannelBytes2Seconds(playbackStreamHandle, position))
        {
        }

        /// <summary>
        /// Attaches to a stream using a function that maps stream positions to scheduling time.
        /// </summary>
        public BassOneShotChannel(int playbackStreamHandle, int sampleStream,
            Func<long, double> getPlaybackTime)
        {
            _playbackStreamHandle = playbackStreamHandle;
            _getPlaybackTime = getPlaybackTime ?? throw new ArgumentNullException(nameof(getPlaybackTime));

            ChannelInfo info = Bass.ChannelGetInfo(playbackStreamHandle);
            if ((info.Flags & BassFlags.Float) == 0)
            {
                Bass.StreamFree(sampleStream);
                throw new ArgumentException("Playback stream must use float sample data.",
                    nameof(playbackStreamHandle));
            }

            _sampleRate = info.Frequency;
            _channelCount = info.Channels;
            _sample = DecodeSample(sampleStream) ?? Array.Empty<float>();
            if (_sample.Length == 0)
            {
                return;
            }

            _callback = MixSamples;
            _previousEndPosition = Math.Max(0,
                Bass.ChannelGetPosition(playbackStreamHandle, PositionFlags.Decode));
            _dspHandle = Bass.ChannelSetDSP(playbackStreamHandle, _callback);
            if (_dspHandle == 0)
            {
                YargLogger.LogFormatError("Failed to attach one-shot DSP: {0}!", Bass.LastError);
            }
        }

        public override void Play()
        {
            lock (this)
            {
                if (!_disposed && _dspHandle != 0)
                {
                    _immediatePlays.Enqueue(0);
                }
            }
        }

        public override void Schedule(double songTime)
        {
            lock (this)
            {
                if (_disposed || _dspHandle == 0)
                {
                    return;
                }

                ScheduleState state = Volatile.Read(ref _state);
                int count = Volatile.Read(ref state.Count);
                if (count < state.Events.Length &&
                    (count == 0 || songTime >= state.Events[count - 1]))
                {
                    state.Events[count] = songTime;
                    Volatile.Write(ref state.Count, count + 1);
                    return;
                }

                int insertionIndex = Array.BinarySearch(state.Events, 0, count, songTime);
                if (insertionIndex < 0)
                {
                    insertionIndex = ~insertionIndex;
                }
                var newState = new ScheduleState(Math.Max(state.Events.Length * 2, count + 1));
                Array.Copy(state.Events, 0, newState.Events, 0, insertionIndex);
                newState.Events[insertionIndex] = songTime;
                Array.Copy(state.Events, insertionIndex, newState.Events, insertionIndex + 1,
                    count - insertionIndex);
                newState.Count = count + 1;
                Volatile.Write(ref _state, newState);
            }
        }

        public override void SetVolume(double volume)
        {
            Volatile.Write(ref _volume, (float) volume);
        }

        public override void ClearSchedule()
        {
            lock (this)
            {
                if (!_disposed)
                {
                    Volatile.Write(ref _state, new ScheduleState(64));
                }
            }
        }

        public void ResetTransport()
        {
            Interlocked.Increment(ref _transportGeneration);
        }

        private unsafe void MixSamples(int _, int channel, IntPtr buffer, int length, IntPtr __)
        {
            int frameCount = length / (sizeof(float) * _channelCount);
            if (frameCount <= 0)
            {
                return;
            }

            long endPosition = Bass.ChannelGetPosition(channel, PositionFlags.Decode);
            if (endPosition < 0)
            {
                return;
            }

            int transportGeneration = Volatile.Read(ref _transportGeneration);
            if (_activeTransportGeneration != transportGeneration)
            {
                _activeTransportGeneration = transportGeneration;
                _previousEndPosition = 0;
                _activeState = null;
                _activeSampleCount = 0;
            }

            long startPosition = _previousEndPosition;
            _previousEndPosition = endPosition;
            if (endPosition <= startPosition)
            {
                return;
            }

            double playbackStart = _getPlaybackTime(startPosition);
            double playbackEnd = _getPlaybackTime(endPosition);
            if (playbackEnd <= playbackStart)
            {
                return;
            }

            ScheduleState state = Volatile.Read(ref _state);
            if (!ReferenceEquals(state, _activeState))
            {
                _activeState = state;
                _nextEvent = FindFirstEvent(state.Events,
                    Volatile.Read(ref state.Count), playbackStart);
            }

            float* output = (float*) buffer;
            float volume = Volatile.Read(ref _volume);
            MixActiveSamples(output, frameCount, volume);
            MixImmediateSamples(output, frameCount, volume);

            int eventCount = Volatile.Read(ref state.Count);
            while (_nextEvent < eventCount)
            {
                double eventTime = state.Events[_nextEvent];
                if (eventTime >= playbackEnd)
                {
                    break;
                }
                _nextEvent++;

                if (eventTime < playbackStart)
                {
                    continue;
                }

                double progress = (eventTime - playbackStart) /
                    (playbackEnd - playbackStart);
                int startFrame = Math.Clamp((int) Math.Round(progress * frameCount), 0,
                    frameCount - 1);
                StartSample(output, frameCount, startFrame, volume);
            }
        }

        private unsafe void MixActiveSamples(float* output, int frameCount, float volume)
        {
            int writeIndex = 0;
            for (int i = 0; i < _activeSampleCount; i++)
            {
                ActiveSample activeSample = _activeSamples[i];
                MixSample(output, frameCount, 0, volume, ref activeSample);
                if (activeSample.Frame * _channelCount < activeSample.Data.Length)
                {
                    _activeSamples[writeIndex++] = activeSample;
                }
            }
            _activeSampleCount = writeIndex;
        }

        private unsafe void MixImmediateSamples(float* output, int frameCount, float volume)
        {
            for (int i = 0; i < MAX_ACTIVE_SAMPLES && _immediatePlays.TryDequeue(out _); i++)
            {
                StartSample(output, frameCount, 0, volume);
            }
        }

        private unsafe void StartSample(float* output, int frameCount, int startFrame, float volume)
        {
            var activeSample = new ActiveSample { Data = _sample };
            MixSample(output, frameCount, startFrame, volume, ref activeSample);
            if (activeSample.Frame * _channelCount < _sample.Length &&
                _activeSampleCount < _activeSamples.Length)
            {
                _activeSamples[_activeSampleCount++] = activeSample;
            }
        }

        private unsafe void MixSample(float* output, int outputFrames, int startFrame,
            float volume, ref ActiveSample activeSample)
        {
            int sampleFrames = activeSample.Data.Length / _channelCount;
            int framesToMix = Math.Min(outputFrames - startFrame, sampleFrames - activeSample.Frame);
            int source = activeSample.Frame * _channelCount;
            int destination = startFrame * _channelCount;
            int valuesToMix = framesToMix * _channelCount;
            for (int i = 0; i < valuesToMix; i++)
            {
                output[destination++] += activeSample.Data[source++] * volume;
            }
            activeSample.Frame += framesToMix;
        }

        private static int FindFirstEvent(double[] events, int eventCount, double playbackTime)
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
            int converter = BassMix.CreateMixerStream(_sampleRate, _channelCount,
                BassFlags.Float | BassFlags.Decode | BassFlags.MixerEnd);
            if (converter == 0)
            {
                YargLogger.LogFormatError("Failed to create one-shot sample converter: {0}!",
                    Bass.LastError);
                Bass.StreamFree(streamHandle);
                return null;
            }

            if (!BassMix.MixerAddChannel(converter, streamHandle, BassFlags.MixerChanNoRampin))
            {
                YargLogger.LogFormatError("Failed to add one-shot sample to converter: {0}!",
                    Bass.LastError);
                Bass.StreamFree(converter);
                Bass.StreamFree(streamHandle);
                return null;
            }

            var samples = new List<float>();
            var buffer = new float[4096];
            int bytesRead;
            while ((bytesRead = Bass.ChannelGetData(converter, buffer,
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
                YargLogger.LogFormatError("Failed to decode one-shot sample: {0}!", Bass.LastError);
            }

            Bass.StreamFree(converter);
            Bass.StreamFree(streamHandle);
            return samples.Count > 0 ? samples.ToArray() : null;
        }

        public override void Dispose()
        {
            Action<BassOneShotChannel> disposed;
            lock (this)
            {
                if (_disposed)
                {
                    return;
                }

                if (_dspHandle != 0 &&
                    !Bass.ChannelRemoveDSP(_playbackStreamHandle, _dspHandle) &&
                    Bass.LastError != Errors.Handle)
                {
                    YargLogger.LogFormatError("Failed to remove one-shot DSP: {0}!", Bass.LastError);
                }
                _disposed = true;
                disposed = Disposed;
                Disposed = null;
            }

            disposed?.Invoke(this);
        }

        internal void PlaybackStreamDisposed()
        {
            lock (this)
            {
                _disposed = true;
                Disposed = null;
            }
        }
    }
}
