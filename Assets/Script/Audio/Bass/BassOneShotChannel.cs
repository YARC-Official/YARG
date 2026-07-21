using System;
using System.Collections.Generic;
using System.Threading;
using ManagedBass;
using ManagedBass.Mix;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    /// Mixes scheduled one-shot samples directly into the final playback mixer.
    /// </summary>
    /// <remarks>
    /// The DSP runs after tempo processing, preserving sample pitch and duration at every song speed.
    /// Scheduled hits are mixed at their exact output frame without creating per-hit BASS syncs.
    /// </remarks>
    internal sealed class BassOneShotChannel : OneShotChannel
    {
        private const int MAX_ACTIVE_SAMPLES = 64;
        private const int DECODE_BUFFER_SIZE = 4096;

        private sealed class ScheduleAnchor
        {
            public readonly int Generation;
            public readonly long OutputPosition;
            public readonly double SongPosition;
            public readonly float Speed;
            public readonly bool ClearActiveSamples;

            public ScheduleAnchor(int generation, long outputPosition, double songPosition,
                float speed, bool clearActiveSamples)
            {
                Generation = generation;
                OutputPosition = outputPosition;
                SongPosition = songPosition;
                Speed = speed;
                ClearActiveSamples = clearActiveSamples;
            }
        }

        private readonly int _outputMixerHandle;
        private readonly int _tempoStreamHandle;
        private readonly int _sampleRate;
        private readonly int _channelCount;
        private readonly int _bytesPerFrame;
        private readonly int _sampleFrameCount;
        private readonly double[] _scheduledPlays;
        private readonly float[] _sample;
        private readonly int[] _activeSampleFrames = new int[MAX_ACTIVE_SAMPLES];
        private readonly Func<long, double> _getSongPosition;
        private readonly Func<float> _getSpeed;
        private readonly double _outputLeadTime;
        private readonly DSPProcedure _dspCallback;
        private readonly int _dspHandle;

        private ScheduleAnchor _scheduleAnchor;
        private int _anchorGeneration;

        // State below is owned by the DSP callback.
        private int _callbackAnchorGeneration = -1;
        private long _previousEndPosition;
        private int _nextScheduledPlay;
        private int _activeSampleCount;

        private float _volume = 1;
        private bool _enabled = true;
        private bool _disposed;

        internal event Action<BassOneShotChannel> Disposed;

        public BassOneShotChannel(int outputMixerHandle, int tempoStreamHandle,
            int sampleStream, IReadOnlyList<double> scheduledPlays,
            Func<long, double> getSongPosition, Func<float> getSpeed, double outputLeadTime)
        {
            _outputMixerHandle = outputMixerHandle;
            _tempoStreamHandle = tempoStreamHandle;
            _getSongPosition = getSongPosition ?? throw new ArgumentNullException(nameof(getSongPosition));
            _getSpeed = getSpeed ?? throw new ArgumentNullException(nameof(getSpeed));
            _outputLeadTime = Math.Max(0, outputLeadTime);
            _scheduledPlays = CopyAndSort(scheduledPlays);

            var info = Bass.ChannelGetInfo(outputMixerHandle);
            bool usesFloatSamples = (info.Flags & BassFlags.Float) != 0;
            if (!usesFloatSamples)
            {
                Bass.StreamFree(sampleStream);
                throw new ArgumentException("Playback mixer must use float sample data.",
                    nameof(outputMixerHandle));
            }

            _sampleRate = info.Frequency;
            _channelCount = info.Channels;
            _bytesPerFrame = sizeof(float) * _channelCount;
            _sample = DecodeSample(sampleStream, _sampleRate, _channelCount) ?? Array.Empty<float>();
            _sampleFrameCount = _sample.Length / _channelCount;
            if (_sampleFrameCount == 0)
            {
                return;
            }

            Reanchor(clearActiveSamples: true);
            _dspCallback = ProcessAudio;
            _dspHandle = Bass.ChannelSetDSP(outputMixerHandle, _dspCallback);
            if (_dspHandle == 0)
            {
                LogBassError("Failed to attach one-shot DSP: {0}!");
            }
        }

        public override void SetVolume(double volume)
        {
            Volatile.Write(ref _volume, (float) volume);
        }

        public override void SetEnabled(bool enabled)
        {
            Volatile.Write(ref _enabled, enabled);
        }

        /// <summary>
        /// One-shot state remains attached while the paused playback graph is prepared for seeking.
        /// </summary>
        internal void PrepareForSeek()
        {
        }

        internal void ResetAfterSeek()
        {
            Reanchor(clearActiveSamples: true);
        }

        internal void ResetAfterSpeedChange()
        {
            Reanchor(clearActiveSamples: false);
        }

        private void Reanchor(bool clearActiveSamples)
        {
            if (_disposed)
            {
                return;
            }

            long outputPosition = Bass.ChannelGetPosition(_outputMixerHandle, PositionFlags.Decode);
            long tempoPosition = Bass.ChannelGetPosition(_tempoStreamHandle, PositionFlags.Decode);
            if (outputPosition < 0 || tempoPosition < 0)
            {
                LogBassError("Failed to read one-shot playback position: {0}!");
                return;
            }

            double songPosition = _getSongPosition(tempoPosition);
            float speed = Math.Max(0.0001f, _getSpeed());
            int generation = Interlocked.Increment(ref _anchorGeneration);
            var anchor = new ScheduleAnchor(generation, outputPosition, songPosition, speed,
                clearActiveSamples);
            Volatile.Write(ref _scheduleAnchor, anchor);
        }

        /// <summary>
        /// Mixes active and newly scheduled samples into one final-output buffer.
        /// </summary>
        /// <remarks>
        /// BASS invokes this on its audio thread. It must remain allocation-free and non-blocking.
        /// </remarks>
        private unsafe void ProcessAudio(int _, int channel, IntPtr buffer, int length, IntPtr __)
        {
            int frameCount = length / _bytesPerFrame;
            if (frameCount <= 0)
            {
                return;
            }

            var anchor = Volatile.Read(ref _scheduleAnchor);
            if (anchor == null)
            {
                return;
            }

            if (_callbackAnchorGeneration != anchor.Generation)
            {
                ApplyAnchor(anchor);
            }

            long endPosition = Bass.ChannelGetPosition(channel, PositionFlags.Decode);
            long startPosition = _previousEndPosition;
            if (endPosition <= startPosition)
            {
                return;
            }
            _previousEndPosition = endPosition;

            float* output = (float*) buffer;
            float volume = Volatile.Read(ref _volume);
            MixActiveSamples(output, frameCount, volume);
            MixScheduledSamples(output, frameCount, volume, startPosition, endPosition, anchor);
        }

        private void ApplyAnchor(ScheduleAnchor anchor)
        {
            _callbackAnchorGeneration = anchor.Generation;
            _previousEndPosition = anchor.OutputPosition;
            _nextScheduledPlay = FindFirstScheduledPlay(anchor);
            if (anchor.ClearActiveSamples)
            {
                _activeSampleCount = 0;
            }
        }

        private int FindFirstScheduledPlay(ScheduleAnchor anchor)
        {
            double firstAudibleSongPosition =
                anchor.SongPosition + _outputLeadTime * anchor.Speed;
            int start = 0;
            int end = _scheduledPlays.Length;
            while (start < end)
            {
                int middle = start + (end - start) / 2;
                double scheduledPlay = _scheduledPlays[middle];
                bool alreadyPassed = _outputLeadTime > 0
                    ? scheduledPlay <= firstAudibleSongPosition
                    : scheduledPlay < firstAudibleSongPosition;
                if (alreadyPassed)
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

        private unsafe void MixScheduledSamples(float* output, int frameCount, float volume,
            long startPosition, long endPosition, ScheduleAnchor anchor)
        {
            long bufferLength = endPosition - startPosition;
            while (_nextScheduledPlay < _scheduledPlays.Length)
            {
                double scheduledPlay = _scheduledPlays[_nextScheduledPlay];
                long targetPosition = GetOutputPosition(scheduledPlay, anchor);
                if (targetPosition >= endPosition)
                {
                    return;
                }
                _nextScheduledPlay++;

                if (targetPosition < startPosition || !Volatile.Read(ref _enabled))
                {
                    continue;
                }

                double progress = (double) (targetPosition - startPosition) / bufferLength;
                int startFrame = Math.Clamp((int) Math.Round(progress * frameCount), 0, frameCount - 1);
                StartSample(output, frameCount, startFrame, volume);
            }
        }

        private long GetOutputPosition(double scheduledPlay, ScheduleAnchor anchor)
        {
            double outputDelay =
                (scheduledPlay - anchor.SongPosition) / anchor.Speed - _outputLeadTime;
            long outputFrames = (long) Math.Round(outputDelay * _sampleRate);
            return anchor.OutputPosition + outputFrames * _bytesPerFrame;
        }

        private unsafe void MixActiveSamples(float* output, int frameCount, float volume)
        {
            int writeIndex = 0;
            for (int i = 0; i < _activeSampleCount; i++)
            {
                int sampleFrame = _activeSampleFrames[i];
                MixSample(output, frameCount, startFrame: 0, volume, ref sampleFrame);
                if (sampleFrame < _sampleFrameCount)
                {
                    _activeSampleFrames[writeIndex++] = sampleFrame;
                }
            }
            _activeSampleCount = writeIndex;
        }

        private unsafe void StartSample(float* output, int frameCount, int startFrame, float volume)
        {
            int sampleFrame = 0;
            MixSample(output, frameCount, startFrame, volume, ref sampleFrame);
            if (sampleFrame < _sampleFrameCount && _activeSampleCount < MAX_ACTIVE_SAMPLES)
            {
                _activeSampleFrames[_activeSampleCount++] = sampleFrame;
            }
        }

        private unsafe void MixSample(float* output, int outputFrames, int startFrame, float volume,
            ref int sampleFrame)
        {
            int framesToMix = Math.Min(outputFrames - startFrame, _sampleFrameCount - sampleFrame);
            int source = sampleFrame * _channelCount;
            int destination = startFrame * _channelCount;
            int valuesRemaining = framesToMix * _channelCount;
            while (valuesRemaining-- > 0)
            {
                float mixed = output[destination] + _sample[source++] * volume;
                output[destination++] = Math.Clamp(mixed, -1f, 1f);
            }
            sampleFrame += framesToMix;
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

        private static float[] DecodeSample(int streamHandle, int sampleRate, int channelCount)
        {
            if (streamHandle == 0)
            {
                return null;
            }

            int converter = BassMix.CreateMixerStream(sampleRate, channelCount,
                BassFlags.Float | BassFlags.Decode | BassFlags.MixerEnd);
            if (converter == 0)
            {
                LogBassError("Failed to create one-shot sample converter: {0}!");
                Bass.StreamFree(streamHandle);
                return null;
            }

            try
            {
                if (!BassMix.MixerAddChannel(converter, streamHandle, BassFlags.MixerChanNoRampin))
                {
                    LogBassError("Failed to add one-shot sample to converter: {0}!");
                    return null;
                }

                var samples = new List<float>();
                var buffer = new float[DECODE_BUFFER_SIZE];
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
                    LogBassError("Failed to decode one-shot sample: {0}!");
                }
                return samples.Count == 0 ? null : samples.ToArray();
            }
            finally
            {
                Bass.StreamFree(converter);
                Bass.StreamFree(streamHandle);
            }
        }

        public override void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_dspHandle != 0 &&
                !Bass.ChannelRemoveDSP(_outputMixerHandle, _dspHandle) &&
                Bass.LastError != Errors.Handle)
            {
                LogBassError("Failed to remove one-shot DSP: {0}!");
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
