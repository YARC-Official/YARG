using System;
using System.Collections.Concurrent;
using ManagedBass;
using ManagedBass.Fx;
using UnityEngine;
using YARG.Audio.PitchDetection;
using YARG.Core.Logging;
using YARG.Core.Audio;
using YARG.Core.IO;
using YARG.Input;
using YARG.Settings;

namespace YARG.Audio.BASS
{
    internal class MonitorPlaybackHandle : IDisposable
    {
        private static readonly ReverbParameters REVERB_PARAMETERS = new()
        {
            fDryMix = 0.3f, fWetMix = 1f, fRoomSize = 0.4f, fDamp = 0.7f
        };

#nullable enable
        public static MonitorPlaybackHandle? Create()
#nullable disable
        {
            // Set up monitoring stream
            int monitorPlaybackHandle = Bass.CreateStream(44100, 1, BassFlags.Default, StreamProcedureType.Push);
            if (monitorPlaybackHandle == 0)
            {
                YargLogger.LogFormatError("Failed to create monitor stream: {0}!", Bass.LastError);
                return null;
            }

            var handle = new MonitorPlaybackHandle(monitorPlaybackHandle);
            using var wrapper = DisposableCounter.Wrap(handle);

            // Add reverb to the monitor playback
            int reverbHandle =
                BassHelpers.FXAddParameters(monitorPlaybackHandle, EffectType.Freeverb, REVERB_PARAMETERS, 1);
            if (reverbHandle == 0)
            {
                YargLogger.LogError("Failed to add reverb to monitor stream!");
                return null;
            }

            // Apply gain to the playback
            handle._applyGain = Bass.ChannelSetDSP(monitorPlaybackHandle, ApplyGain);
            if (handle._applyGain == 0)
            {
                YargLogger.LogFormatError("Failed to add gain to monitor stream: {0}!", Bass.LastError);
                return null;
            }

            // Start monitoring
            if (!Bass.ChannelPlay(monitorPlaybackHandle))
            {
                YargLogger.LogFormatError("Failed to start monitor stream: {0}!", Bass.LastError);
                return null;
            }

            return wrapper.Release();
        }

        public readonly int Handle;

        private int _applyGain;

        private bool _disposed;

        private MonitorPlaybackHandle(int handle)
        {
            Handle = handle;
        }

        private static void ApplyGain(int handle, int channel, IntPtr buffer, int length, IntPtr user)
        {
            BassHelpers.ApplyGain(1.3f, buffer, length);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                Bass.StreamFree(Handle);
                if (_applyGain != 0)
                {
                    Bass.StreamFree(_applyGain);
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~MonitorPlaybackHandle()
        {
            Dispose(false);
        }
    }

    internal class RecordingHandle : IDisposable
    {
        // This is as low as we can go with BASS
        internal const int CLEAN_RECORD_PERIOD_MS = 5;

#nullable enable
        public static RecordingHandle? CreateRecordingHandle(RecordProcedure procedure)
#nullable disable
        {
            int handle = Bass.RecordStart(44100, 1, BassFlags.Default, CLEAN_RECORD_PERIOD_MS,
                procedure, IntPtr.Zero);
            if (handle == 0)
            {
                YargLogger.LogFormatError("Failed to start clean recording: {0}!", Bass.LastError);
                return null;
            }

            int processedHandle = Bass.CreateStream(44100, 1, BassFlags.Decode, StreamProcedureType.Push);
            if (processedHandle == 0)
            {
                YargLogger.LogFormatError("Failed to create processed recording stream: {0}!", Bass.LastError);
                return null;
            }

            return new RecordingHandle(handle, processedHandle);
        }

        public readonly int Handle;
        public readonly int ProcessedHandle;

        private bool _disposed;

        private RecordingHandle(int handle, int processedHandle)
        {
            Handle = handle;
            ProcessedHandle = processedHandle;
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                Bass.ChannelStop(Handle);
                Bass.StreamFree(Handle);

                Bass.ChannelStop(ProcessedHandle);
                Bass.StreamFree(ProcessedHandle);
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~RecordingHandle()
        {
            Dispose(false);
        }
    }

    public sealed class BassMicDevice : MicDevice
    {
#nullable enable
        internal static BassMicDevice? Create(int deviceId, string name)
#nullable disable
        {
            // Must initialise device before recording
            if (!Bass.RecordInit(deviceId) || !Bass.RecordGetInfo(out var info))
            {
                YargLogger.LogFormatError("Failed to initialize recording device: {0}!", Bass.LastError);
                return null;
            }

            var monitorPlayback = MonitorPlaybackHandle.Create();
            if (monitorPlayback == null)
            {
                return null;
            }

            var device = new BassMicDevice(deviceId, name, monitorPlayback);
            using var wrapper = DisposableCounter.Wrap(device);
            device._recordHandle = RecordingHandle.CreateRecordingHandle(device.ProcessRecordData);
            if (device._recordHandle == null)
            {
                monitorPlayback.Dispose();
                return null;
            }

            int lowEqHandle = BassHelpers.AddEqToChannel(device._recordHandle.ProcessedHandle, _lowEqParameters);
            int highEqHandle = BassHelpers.AddEqToChannel(device._recordHandle.ProcessedHandle, _highEqParameters);
            if (lowEqHandle == 0 || highEqHandle == 0)
            {
                YargLogger.LogFormatError("Failed to add EQ to processed recording stream: {0}!", Bass.LastError);
                return null;
            }

            return wrapper.Release();
        }

        private static readonly PeakEQParameters _lowEqParameters = new()
        {
            fBandwidth = 2.5f, fCenter = 20f, fGain = -10f
        };

        private static readonly PeakEQParameters _highEqParameters = new()
        {
            fBandwidth = 2.5f, fCenter = 10_000f, fGain = -10f
        };

        private readonly ConcurrentQueue<MicOutputFrame> _frameQueue = new();

        private readonly PitchTracker _pitchDetector = new();

        private readonly MonitorPlaybackHandle _monitorHandle;

        private readonly int _deviceId;

        private RecordingHandle _recordHandle;

        private float? _lastPitchOutput;

        private int _timeAccumulated;
        private int _processedBufferLength;

        public override int Reset()
        {
            _frameQueue.Clear();

            // Query number of bytes in the recording buffer
            int available = Bass.ChannelGetData(_recordHandle.Handle, IntPtr.Zero, (int) DataFlags.Available);

            // Getting channel data removes it from the buffer (clearing it)
            if (Bass.ChannelGetData(_recordHandle.Handle, IntPtr.Zero, available) == -1)
            {
                return (int) Bass.LastError;
            }

            if (Bass.ChannelGetData(_recordHandle.ProcessedHandle, IntPtr.Zero, 0xFFFFFFF) == -1)
            {
                return (int) Bass.LastError;
            }

            // Undefined position flag in ManagedBass. Will flush the buffer of a decoding channel when setting the position
            // By default channels are flushed when setting the position, but this is not the case for decoding channels.
            const int bassPosFlush = 0x1000000;

            // This channel isn't a decoding channel so the flag technically isn't needed. But in the event it is changed to one,
            // then this will ensure it continues to work.
            if (!Bass.ChannelSetPosition(_monitorHandle.Handle, 0, (PositionFlags) bassPosFlush))
            {
                return (int) Bass.LastError;
            }

            return 0;
        }

        public override bool DequeueOutputFrame(out MicOutputFrame frame)
        {
            return _frameQueue.TryDequeue(out frame);
        }

        public override void ClearOutputQueue()
        {
            _frameQueue.Clear();
        }

        public override void SetMonitoringLevel(float volume)
        {
            if (!Bass.ChannelSetAttribute(_monitorHandle.Handle, ChannelAttribute.Volume, volume))
            {
                YargLogger.LogFormatError("Failed to set volume attribute: {0}", Bass.LastError);
            }
        }

        public override SerializedMic Serialize()
        {
            return new SerializedMic(DisplayName);
        }

        private BassMicDevice(int deviceId, string name, MonitorPlaybackHandle monitorHandle)
            : base(name)
        {
            _deviceId = deviceId;
            _monitorHandle = monitorHandle;
        }

        private bool ProcessRecordData(int handle, IntPtr buffer, int length, IntPtr user)
        {
            // Copies the data from the recording buffer to the monitor playback buffer.
            if (Bass.StreamPutData(_monitorHandle.Handle, buffer, length) == -1)
            {
                YargLogger.LogFormatError("Error pushing data to monitor stream: {0}", Bass.LastError);
            }

            // Wait for initialization to complete before processing data
            if (!IsRecordingOutput)
            {
                return true;
            }

            // Copy the data to the batch handle to apply FX
            Bass.StreamPutData(_recordHandle.ProcessedHandle, buffer, length);

            _timeAccumulated += 10;//RecordingHandle.CLEAN_RECORD_PERIOD_MS;

            _processedBufferLength += length;

            // Enough time has passed for pitch detection
            if(_timeAccumulated >= RECORD_PERIOD_MS)
            {
                YargLogger.LogFormatInfo("Calculating pitch with {0} bytes, time: {1}", _processedBufferLength, _timeAccumulated);

                Span<byte> procBuff = stackalloc byte[_processedBufferLength];

                unsafe
                {
                    fixed(byte* ptr = procBuff)
                    {
                        Bass.ChannelGetData(_recordHandle.ProcessedHandle, (IntPtr) ptr, procBuff.Length);
                    }
                }

                CalculatePitchAndAmplitude(procBuff);
                _timeAccumulated = 0;
                _processedBufferLength = 0;
            }

            return true;
        }

        private void CalculatePitchAndAmplitude(ReadOnlySpan<byte> buffer)
        {
            int sampleCount = buffer.Length / sizeof(short);
            Span<float> floatBuffer = stackalloc float[sampleCount];

            // Convert 16 bit buffer to floats
            // If this isn't 16 bit god knows what device they're using.
            unsafe
            {
                fixed (byte* bytesPtr = buffer)
                {
                    var shortBufferSpan = new ReadOnlySpan<short>(bytesPtr, sampleCount);
                    for (int i = 0; i < sampleCount; i++)
                    {
                        floatBuffer[i] = shortBufferSpan[i] / 32768f;
                    }
                }
            }

            // Calculate the root mean square
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < sampleCount; i += 4, count++)
            {
                sum += floatBuffer[i] * floatBuffer[i];
            }

            sum = Mathf.Sqrt(sum / count);

            // Convert to decibels to get the amplitude
            float amplitude = 20f * Mathf.Log10(sum * 180f);
            if (amplitude < -160f)
            {
                amplitude = -160f;
            }

            // Skip pitch detection if not speaking
            if (amplitude < SettingsManager.Settings.MicrophoneSensitivity.Value)
            {
                _lastPitchOutput = null;
                return;
            }

            // Process the pitch buffer
            var pitchOutput = _pitchDetector.ProcessBuffer(floatBuffer);
            if (pitchOutput != null)
            {
                _lastPitchOutput = pitchOutput;
            }

            // We cannot push a frame if there was no pitch
            if (_lastPitchOutput == null)
            {
                return;
            }

            // Queue a MicOutput frame
            var frame = new MicOutputFrame(
                InputManager.CurrentInputTime, _lastPitchOutput.Value, amplitude);
            _frameQueue.Enqueue(frame);
        }

        protected override void DisposeUnmanagedResources()
        {
            _monitorHandle.Dispose();
            _recordHandle.Dispose();
            Bass.CurrentRecordingDevice = _deviceId;
            Bass.RecordFree();
        }
    }
}