using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using ManagedBass;
using ManagedBass.Mix;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    /// Schedules one-shot decode streams directly in the final BASS playback mixer.
    /// </summary>
    internal sealed class BassOneShotChannel : OneShotChannel
    {
        // Matches previous DSP implementation's overlap limit. Slots share one PCM array, so
        // increasing overlap capacity does not duplicate decoded sample memory.
        private const int DECODER_POOL_SIZE = 64;
        private const int DECODE_BUFFER_SIZE = 4096;

        private sealed class DecoderSlot
        {
            private readonly float[] _sample;
            private int _sampleIndex;

            public readonly StreamProcedure Procedure;
            public int Handle;
            public int EndSyncHandle;
            public int Active;

            public DecoderSlot(float[] sample)
            {
                _sample = sample;
                Procedure = ReadSample;
            }

            public void Rewind()
            {
                _sampleIndex = 0;
            }

            private int ReadSample(int handle, IntPtr buffer, int length, IntPtr user)
            {
                int requestedSamples = length / sizeof(float);
                int remainingSamples = _sample.Length - _sampleIndex;
                int copiedSamples = Math.Min(requestedSamples, remainingSamples);
                if (copiedSamples > 0)
                {
                    Marshal.Copy(_sample, _sampleIndex, buffer, copiedSamples);
                    _sampleIndex += copiedSamples;
                }

                int copiedBytes = copiedSamples * sizeof(float);
                if (_sampleIndex >= _sample.Length)
                {
                    return copiedBytes | unchecked((int) StreamProcedureType.End);
                }
                return copiedBytes;
            }
        }

        private readonly int _outputMixerHandle;
        private readonly int _tempoStreamHandle;
        private readonly double[] _scheduledPlays;
        private readonly DecoderSlot[] _decoderPool;
        private readonly List<int> _syncHandles = new();
        private readonly List<SyncProcedure> _syncProcedures = new();
        private readonly List<SyncProcedure> _decoderEndProcedures = new();
        private readonly Func<long, double> _getSongPosition;
        private readonly Func<float> _getSpeed;

        private float _volume = 1;
        private int _generation;
        private bool _disposed;

        internal event Action<BassOneShotChannel> Disposed;

        public BassOneShotChannel(int outputMixerHandle, int tempoStreamHandle,
            int sampleStream, IReadOnlyList<double> scheduledPlays,
            Func<long, double> getSongPosition, Func<float> getSpeed)
        {
            _outputMixerHandle = outputMixerHandle;
            _tempoStreamHandle = tempoStreamHandle;
            _getSongPosition = getSongPosition ?? throw new ArgumentNullException(nameof(getSongPosition));
            _getSpeed = getSpeed ?? throw new ArgumentNullException(nameof(getSpeed));
            _scheduledPlays = CopyAndSort(scheduledPlays);

            var outputInfo = Bass.ChannelGetInfo(outputMixerHandle);
            float[] sample = DecodeSample(sampleStream, outputInfo.Frequency, outputInfo.Channels);
            if (sample == null || sample.Length == 0)
            {
                _decoderPool = Array.Empty<DecoderSlot>();
                return;
            }

            _decoderPool = new DecoderSlot[DECODER_POOL_SIZE];
            for (int i = 0; i < _decoderPool.Length; i++)
            {
                var slot = new DecoderSlot(sample);
                slot.Handle = Bass.CreateStream(outputInfo.Frequency, outputInfo.Channels,
                    BassFlags.Decode | BassFlags.Float, slot.Procedure, IntPtr.Zero);
                if (slot.Handle == 0)
                {
                    LogBassError("Failed to create one-shot decoder: {0}!");
                    _decoderPool[i] = slot;
                    continue;
                }

                SyncProcedure endProcedure = (handle, channel, data, user) =>
                    Volatile.Write(ref slot.Active, 0);
                slot.EndSyncHandle = Bass.ChannelSetSync(slot.Handle,
                    SyncFlags.End | SyncFlags.Mixtime, 0, endProcedure, IntPtr.Zero);
                if (slot.EndSyncHandle == 0)
                {
                    LogBassError("Failed to create one-shot decoder end sync: {0}!");
                    Bass.StreamFree(slot.Handle);
                    slot.Handle = 0;
                    _decoderPool[i] = slot;
                    continue;
                }
                // Keep callback rooted for the stream lifetime.
                _decoderEndProcedures.Add(endProcedure);
                Bass.ChannelSetAttribute(slot.Handle, ChannelAttribute.Volume, _volume);
                _decoderPool[i] = slot;
            }

            RebuildPendingSyncs();
        }

        public override void SetVolume(double volume)
        {
            _volume = (float) volume;
            foreach (var slot in _decoderPool)
            {
                if (slot?.Handle != 0)
                {
                    Bass.ChannelSetAttribute(slot.Handle, ChannelAttribute.Volume, _volume);
                }
            }
        }

        /// <summary>
        /// Invalidates callbacks and removes click sources before playback graph reset.
        /// </summary>
        internal void PrepareForSeek()
        {
            Interlocked.Increment(ref _generation);
            RemovePendingSyncs();
            foreach (var slot in _decoderPool)
            {
                if (slot?.Handle == 0)
                {
                    continue;
                }
                BassMix.MixerRemoveChannel(slot.Handle);
                Volatile.Write(ref slot.Active, 0);
            }
        }

        internal void ResetAfterSeek()
        {
            RebuildPendingSyncs();
        }

        internal void ResetAfterSpeedChange()
        {
            Interlocked.Increment(ref _generation);
            RemovePendingSyncs();
            RebuildPendingSyncs();
        }

        private void RebuildPendingSyncs()
        {
            if (_disposed || _decoderPool.Length == 0)
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

            double songPosition = _getSongPosition(tempoPosition);
            float speed = Math.Max(0.0001f, _getSpeed());
            int generation = Volatile.Read(ref _generation);

            foreach (double scheduledPlay in _scheduledPlays)
            {
                if (scheduledPlay < songPosition)
                {
                    continue;
                }

                double outputDelay = (scheduledPlay - songPosition) / speed;
                long targetPosition = outputPosition +
                    Bass.ChannelSeconds2Bytes(_outputMixerHandle, outputDelay);

                SyncProcedure procedure = (handle, channel, data, user) =>
                {
                    if (generation == Volatile.Read(ref _generation))
                    {
                        StartDecoder();
                    }
                };
                int syncHandle = Bass.ChannelSetSync(_outputMixerHandle,
                    SyncFlags.Position | SyncFlags.Mixtime | SyncFlags.Onetime,
                    targetPosition, procedure, IntPtr.Zero);
                if (syncHandle == 0)
                {
                    LogBassError("Failed to schedule one-shot sync: {0}!");
                    continue;
                }
                _syncProcedures.Add(procedure);
                _syncHandles.Add(syncHandle);
            }
        }

        private void StartDecoder()
        {
            foreach (var slot in _decoderPool)
            {
                if (slot?.Handle == 0 ||
                    Interlocked.CompareExchange(ref slot.Active, 1, 0) != 0)
                {
                    continue;
                }

                BassMix.MixerRemoveChannel(slot.Handle);
                slot.Rewind();
                if (!Bass.ChannelSetPosition(slot.Handle, 0, PositionFlags.Bytes) ||
                    !BassMix.MixerAddChannel(_outputMixerHandle, slot.Handle,
                        BassFlags.MixerChanNoRampin))
                {
                    Volatile.Write(ref slot.Active, 0);
                }
                return;
            }
        }

        private void RemovePendingSyncs()
        {
            foreach (int handle in _syncHandles)
            {
                Bass.ChannelRemoveSync(_outputMixerHandle, handle);
            }
            _syncHandles.Clear();
            _syncProcedures.Clear();
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
        /// Decodes and converts an owned sample stream to float data matching the playback mixer.
        /// </summary>
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
                while ((bytesRead = Bass.ChannelGetData(
                    converter, buffer, buffer.Length * sizeof(float))) > 0)
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
            Interlocked.Increment(ref _generation);
            RemovePendingSyncs();
            foreach (var slot in _decoderPool)
            {
                if (slot?.Handle == 0)
                {
                    continue;
                }
                BassMix.MixerRemoveChannel(slot.Handle);
                if (slot.EndSyncHandle != 0)
                {
                    Bass.ChannelRemoveSync(slot.Handle, slot.EndSyncHandle);
                }
                Bass.StreamFree(slot.Handle);
            }
            _syncProcedures.Clear();
            _decoderEndProcedures.Clear();
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
