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
    /// Mixes scheduled instances of a decoded sample directly into a BASS float playback stream using a DSP.
    ///
    /// With this class we can play sfx with sample-accurate timing. This is useful for metronome, claps,
    /// or anything that should play to the beat of the song.
    /// </summary>
    /// <remarks>
    /// The complete schedule is copied during construction, before the DSP callback is attached.
    /// </remarks>
    internal sealed class BassOneShotChannel : OneShotChannel
    {
        private const int MAX_ACTIVE_SAMPLES = 64;
        private const int DECODE_BUFFER_SIZE = 4096;

        private readonly int _playbackStreamHandle;
        private readonly int _channelCount;
        private readonly int _sampleFrameCount;

        private readonly PlaybackTimeResolver _getPlaybackTime;
        private readonly float[]              _sample;
        private readonly double[]             _scheduledPlays;
        private readonly int                  _dspHandle;

        private readonly int[] _activeSampleFrames = new int[MAX_ACTIVE_SAMPLES];

        // A negative index means the schedule must be positioned from the next callback's
        // playback time. This is also the initial state because channels may be created mid-song.
        private int _nextScheduledEvent = -1;

        private int  _seekGeneration;
        private int  _callbackSeekGeneration = -1;
        private long _previousEndPosition;

        private int   _activeSampleCount;
        private float _volume = 1;
        private bool  _disposed;
        internal event Action<BassOneShotChannel> Disposed;
        internal delegate double PlaybackTimeResolver(long streamPosition);

        /// <summary>
        /// Creates a channel that decodes and mixes one sample at the supplied playback times.
        /// </summary>
        /// <param name="playbackStreamHandle">BASS float stream that receives the mixed sample.</param>
        /// <param name="sampleStream">
        /// Owned BASS stream containing the sample. The channel frees it after decoding.
        /// </param>
        /// <param name="scheduledPlays">
        /// Playback times in seconds. The values are copied and sorted before playback begins.
        /// </param>
        /// <param name="getPlaybackTime">
        /// Resolves a byte position in the playback stream to its song time.
        /// </param>
        public BassOneShotChannel(
            int playbackStreamHandle,
            int sampleStream,
            IReadOnlyList<double> scheduledPlays,
            PlaybackTimeResolver getPlaybackTime)
        {
            _playbackStreamHandle = playbackStreamHandle;
            _getPlaybackTime = getPlaybackTime ?? throw new ArgumentNullException(nameof(getPlaybackTime));
            _scheduledPlays = CopyAndSort(scheduledPlays);

            var info = Bass.ChannelGetInfo(playbackStreamHandle);
            bool usesFloatSamples = (info.Flags & BassFlags.Float) != 0;
            if (!usesFloatSamples)
            {
                Bass.StreamFree(sampleStream);
                throw new ArgumentException("Playback stream must use float sample data.",
                    nameof(playbackStreamHandle));
            }

            _channelCount = info.Channels;
            _sample = DecodeSample(sampleStream, info.Frequency, info.Channels) ?? Array.Empty<float>();
            _sampleFrameCount = _sample.Length / _channelCount;
            if (_sampleFrameCount == 0)
            {
                return;
            }

            DSPProcedure dspCallback = ProcessAudio;
            _previousEndPosition = Math.Max(0, Bass.ChannelGetPosition(playbackStreamHandle, PositionFlags.Decode));
            _dspHandle = Bass.ChannelSetDSP(playbackStreamHandle, dspCallback);
            if (_dspHandle == 0)
            {
                LogBassError("Failed to attach one-shot DSP: {0}!");
            }
        }

        /// <summary>
        /// Sets volume applied to future mixing, including samples already in progress.
        /// </summary>
        /// <param name="volume">Linear volume multiplier.</param>
        public override void SetVolume(double volume)
        {
            Volatile.Write(ref _volume, (float) volume);
        }

        internal void ResetAfterSeek()
        {
            Interlocked.Increment(ref _seekGeneration);
        }

        /// <summary>
        /// Mixes active and newly scheduled sample instances into one DSP output buffer.
        /// </summary>
        /// <remarks>
        /// BASS invokes this method on its audio thread. Callback-owned playback state must only be
        /// changed here, except for seek and volume signals published through atomic operations.
        /// </remarks>
        private unsafe void ProcessAudio(int _, int channel, IntPtr buffer, int length, IntPtr __)
        {
            int frameCount = length / (sizeof(float) * _channelCount);
            if (frameCount <= 0 || !GetPlaybackWindow(channel, out double startTime, out double endTime))
            {
                return;
            }
            float* output = (float*) buffer;
            float volume = Volatile.Read(ref _volume);
            MixActiveSamples(output, frameCount, volume);
            PositionSchedule(startTime);
            MixScheduledSamples(output, frameCount, volume, startTime, endTime);
        }

        /// <summary>
        /// Resolves the song-time interval represented by the current callback and applies pending
        /// seek resets before exposing that interval to the mixer.
        /// </summary>
        /// <remarks>
        /// Stream positions delimit adjacent callback buffers. A non-advancing position is ignored
        /// because it cannot describe a valid forward playback interval.
        /// </remarks>
        private bool GetPlaybackWindow(
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
            int generation = Volatile.Read(ref _seekGeneration);
            if (_callbackSeekGeneration != generation)
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

        /// <summary>
        /// Clears callback-owned progress after a seek so schedule and active samples can be rebuilt
        /// from the new playback position.
        /// </summary>
        private void ResetCallbackState(int generation)
        {
            _callbackSeekGeneration = generation;
            _previousEndPosition = 0;
            _nextScheduledEvent = -1;
            _activeSampleCount = 0;
        }

        /// <summary>
        /// Positions the schedule at its first event at or after the current playback time.
        /// </summary>
        private void PositionSchedule(double playbackStart)
        {
            if (_nextScheduledEvent >= 0)
            {
                return;
            }

            _nextScheduledEvent = FindFirstScheduledPlay(playbackStart);
        }

        /// <summary>
        /// Finds the first scheduled play at or after the supplied playback time. Unlike
        /// <see cref="Array.BinarySearch(Array, object)"/>, this preserves duplicate events.
        /// </summary>
        private int FindFirstScheduledPlay(double playbackTime)
        {
            int start = 0;
            int end = _scheduledPlays.Length;
            while (start < end)
            {
                int middle = start + (end - start) / 2;
                if (_scheduledPlays[middle] < playbackTime)
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

        /// <summary>
        /// Starts each event in the current playback interval at its corresponding output frame.
        /// Samples extending beyond this buffer are retained for subsequent callbacks.
        /// </summary>
        private unsafe void MixScheduledSamples(
            float* output,
            int frameCount,
            float volume,
            double playbackStart,
            double playbackEnd)
        {
            double duration = playbackEnd - playbackStart;
            while (_nextScheduledEvent < _scheduledPlays.Length)
            {
                double eventTime = _scheduledPlays[_nextScheduledEvent];
                if (eventTime >= playbackEnd)
                {
                    return;
                }
                _nextScheduledEvent++;

                if (eventTime < playbackStart)
                {
                    continue;
                }

                double progress = (eventTime - playbackStart) / duration;
                int startFrame = Math.Clamp((int) Math.Round(progress * frameCount), 0, frameCount - 1);
                StartSample(output, frameCount, startFrame, volume);
            }
        }

        /// <summary>
        /// Continues sample instances started by earlier callbacks and compacts unfinished instances
        /// in place.
        /// </summary>
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
                output[destination++] += _sample[source++] * volume;
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

        /// <summary>
        /// Decodes and converts an owned sample stream to float data matching the playback stream.
        /// </summary>
        /// <remarks>
        /// Both the source stream and temporary mixer are released before this method returns,
        /// including all failure paths.
        /// </remarks>
        private static float[] DecodeSample(int streamHandle, int sampleRate, int channelCount)
        {
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
                    LogBassError(
                        "Failed to add one-shot sample to converter: {0}!");
                    return null;
                }

                var samples = new List<float>();
                var buffer = new float[DECODE_BUFFER_SIZE];
                int bytesRead;
                while ((bytesRead = Bass.ChannelGetData(converter, buffer, buffer.Length * sizeof(float))) > 0)
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

        /// <summary>
        /// Detaches DSP callback and releases this channel.
        /// </summary>
        public override void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            RemoveDsp();
            _disposed = true;
            var disposed = Disposed;
            Disposed = null;
            disposed?.Invoke(this);
        }

        private void RemoveDsp()
        {
            if (_dspHandle == 0 ||
                Bass.ChannelRemoveDSP(_playbackStreamHandle, _dspHandle) ||
                Bass.LastError == Errors.Handle)
            {
                return;
            }

            LogBassError("Failed to remove one-shot DSP: {0}!");
        }

        internal void PlaybackStreamDisposed()
        {
            _disposed = true;
            Disposed = null;
        }

        private static void LogBassError(string format)
        {
            YargLogger.LogFormatError(format, Bass.LastError);
        }

    }
}
