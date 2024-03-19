using System;
using System.Collections.Concurrent;
using ManagedBass;
using ManagedBass.Fx;
using UnityEngine;
using YARG.Audio.PitchDetection;
using YARG.Core.Logging;
using YARG.Input;
using YARG.Settings;

namespace YARG.Audio.BASS
{
    public class BassMicDevice : IMicDevice
    {
        // This is as low as we can go with BASS
        private const int CLEAN_RECORD_PERIOD_MS = 5;

        public string DisplayName => _deviceInfo.Name;
        public bool IsDefault => _deviceInfo.IsDefault;

        public bool IsMonitoring { get; set; }
        public bool IsRecordingOutput { get; set; }

        private float? _lastPitchOutput;
        private readonly ConcurrentQueue<MicOutputFrame> _frameQueue = new();

        private int _deviceId;
        private DeviceInfo _deviceInfo;

        private int _cleanRecordHandle;
        private int _processedRecordHandle;
        private int _applyGainHandle;
        private int _monitorPlaybackHandle;

        private bool _initialized;
        private bool _disposed;

        private PitchTracker _pitchDetector;

        private readonly ReverbParameters _monitoringReverbParameters = new()
        {
            fDryMix = 0.3f, fWetMix = 1f, fRoomSize = 0.4f, fDamp = 0.7f
        };

        private readonly PeakEQParameters _lowEqParameters = new()
        {
            fBandwidth = 2.5f, fCenter = 20f, fGain = -10f
        };

        private readonly PeakEQParameters _highEqParameters = new()
        {
            fBandwidth = 2.5f, fCenter = 10_000f, fGain = -10f
        };

        public BassMicDevice(int deviceId, DeviceInfo info)
        {
            _deviceId = deviceId;
            _deviceInfo = info;
        }

        public int Initialize()
        {
            if (_initialized || _disposed) return 0;

            _frameQueue.Clear();

            // Must initialise device before recording
            if (!Bass.RecordInit(_deviceId) || !Bass.RecordGetInfo(out var info))
            {
                YargLogger.LogFormatError("Failed to initialize recording device: {0}", Bass.LastError);
                return (int) Bass.LastError;
            }

            const BassFlags FLAGS = BassFlags.Default;

            // We want to start recording immediately because of device context switching and device numbers.
            // If we initialize the device but don't record immediately, the device number might change
            // and we'll be recording from the wrong device.
            _cleanRecordHandle = Bass.RecordStart(44100, info.Channels, FLAGS, CLEAN_RECORD_PERIOD_MS,
                ProcessCleanRecordData, IntPtr.Zero);
            _processedRecordHandle = Bass.RecordStart(44100, info.Channels, FLAGS, IMicDevice.RECORD_PERIOD_MS,
                ProcessRecordData, IntPtr.Zero);
            if (_cleanRecordHandle == 0 || _processedRecordHandle == 0)
            {
                YargLogger.LogFormatError("Failed to start recording: {0}", Bass.LastError);
                Dispose();
                return (int) Bass.LastError;
            }

            // Add EQ
            int lowEqHandle = BassHelpers.AddEqToChannel(_processedRecordHandle, _lowEqParameters);
            int highEqHandle = BassHelpers.AddEqToChannel(_processedRecordHandle, _highEqParameters);
            if (lowEqHandle == 0 || highEqHandle == 0)
            {
                YargLogger.LogError("Failed to add EQ to recording stream!");
                Dispose();
                return (int) Bass.LastError;
            }

            // Set up monitoring stream
            _monitorPlaybackHandle = Bass.CreateStream(44100, info.Channels, FLAGS, StreamProcedureType.Push);
            if (_monitorPlaybackHandle == 0)
            {
                YargLogger.LogFormatError("Failed to create monitor stream: {0}", Bass.LastError);
                Dispose();
                return (int) Bass.LastError;
            }

            // Add reverb to the monitor playback
            int reverbHandle = BassHelpers.FXAddParameters(_monitorPlaybackHandle, EffectType.Freeverb,
                _monitoringReverbParameters, 1);
            if (reverbHandle == 0)
            {
                YargLogger.LogError("Failed to add reverb to monitor stream!");
                Dispose();
                return (int) Bass.LastError;
            }

            // Apply gain to the playback
            _applyGainHandle = Bass.ChannelSetDSP(_monitorPlaybackHandle, ApplyGain);
            if (_applyGainHandle == 0)
            {
                YargLogger.LogFormatError("Failed to add gain to monitor stream: {0}", Bass.LastError);
                Dispose();
                return (int) Bass.LastError;
            }

            // Start monitoring
            if (!Bass.ChannelPlay(_monitorPlaybackHandle))
            {
                YargLogger.LogFormatError("Failed to start monitor stream: {0}", Bass.LastError);
                Dispose();
                return (int) Bass.LastError;
            }

            IsMonitoring = true;
            SetMonitoringLevel(SettingsManager.Settings.VocalMonitoring.Value);

            _pitchDetector = new PitchTracker();

            _initialized = true;
            return 0;
        }

        public int Reset()
        {
            if (!_initialized || _disposed) return 0;

            _frameQueue.Clear();

            if(_cleanRecordHandle != 0)
            {
                // Query number of bytes in the recording buffer
                int available = Bass.ChannelGetData(_cleanRecordHandle, IntPtr.Zero, (int)DataFlags.Available);

                // Getting channel data removes it from the buffer (clearing it)
                if (Bass.ChannelGetData(_cleanRecordHandle, IntPtr.Zero, available) == -1)
                {
                    return (int) Bass.LastError;
                }
            }

            if(_processedRecordHandle != 0)
            {
                int available = Bass.ChannelGetData(_processedRecordHandle, IntPtr.Zero, (int)DataFlags.Available);

                if (Bass.ChannelGetData(_processedRecordHandle, IntPtr.Zero, available) == -1)
                {
                    return (int) Bass.LastError;
                }
            }

            if(_monitorPlaybackHandle != 0)
            {
                // Undefined position flag in ManagedBass. Will flush the buffer of a decoding channel when setting the position
                // By default channels are flushed when setting the position, but this is not the case for decoding channels.
                const int bassPosFlush = 0x1000000;

                // This channel isn't a decoding channel so the flag technically isn't needed. But in the event it is changed to one,
                // then this will ensure it continues to work.
                if (!Bass.ChannelSetPosition(_monitorPlaybackHandle, 0, (PositionFlags) bassPosFlush))
                {
                    return (int) Bass.LastError;
                }
            }

            return 0;
        }

        public bool DequeueOutputFrame(out MicOutputFrame frame)
        {
            frame = new MicOutputFrame();

            if (_frameQueue.IsEmpty)
            {
                return false;
            }

            if (_frameQueue.TryDequeue(out frame))
            {
                return true;
            }

            return false;
        }

        public void ClearOutputQueue()
        {
            _frameQueue.Clear();
        }

        public void SetMonitoringLevel(float volume)
        {
            if (_monitorPlaybackHandle == 0) return;

            if (!Bass.ChannelSetAttribute(_monitorPlaybackHandle, ChannelAttribute.Volume, volume))
            {
                YargLogger.LogFormatError("Failed to set volume attribute: {0}", Bass.LastError);
            }
        }

        public SerializedMic Serialize()
        {
            return new SerializedMic
            {
                DisplayName = DisplayName
            };
        }

        public bool IsSerializedMatch(SerializedMic mic)
        {
            return mic.DisplayName == DisplayName;
        }

        private bool ProcessCleanRecordData(int handle, IntPtr buffer, int length, IntPtr user)
        {
            // Wait for initialization to complete before processing data
            if (!_initialized)
            {
                return true;
            }

            // Copies the data from the recording buffer to the monitor playback buffer.
            if (IsMonitoring)
            {
                Bass.StreamPutData(_monitorPlaybackHandle, buffer, length);
            }

            return true;
        }

        private bool ProcessRecordData(int handle, IntPtr buffer, int length, IntPtr user)
        {
            // Wait for initialization to complete before processing data
            if (!_initialized || !IsRecordingOutput)
            {
                return true;
            }

            CalculatePitchAndAmplitude(buffer, length);
            return true;
        }

        private void ApplyGain(int handle, int channel, IntPtr buffer, int length, IntPtr user)
        {
            BassHelpers.ApplyGain(1.3f, buffer, length);
        }

        private unsafe void CalculatePitchAndAmplitude(IntPtr buffer, int byteLength)
        {
            int sampleCount = byteLength / sizeof(short);
            Span<float> floatBuffer = stackalloc float[sampleCount];

            // Convert 16 bit buffer to floats
            // If this isn't 16 bit god knows what device they're using.
            var shortBufferSpan = new ReadOnlySpan<short>((short*) buffer, sampleCount);
            for (int i = 0; i < sampleCount; i++)
            {
                floatBuffer[i] = shortBufferSpan[i] / 32768f;
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
            if (_lastPitchOutput == null) return;

            // Queue a MicOutput frame
            var frame = new MicOutputFrame(
                InputManager.CurrentInputTime, _lastPitchOutput.Value, amplitude);
            _frameQueue.Enqueue(frame);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                // Free managed resources here
                if (disposing)
                {
                }

                // Free unmanaged resources here
                if (_cleanRecordHandle != 0)
                {
                    Bass.ChannelStop(_cleanRecordHandle);
                    Bass.StreamFree(_cleanRecordHandle);
                    _cleanRecordHandle = 0;
                }

                if (_processedRecordHandle != 0)
                {
                    Bass.ChannelStop(_processedRecordHandle);
                    Bass.StreamFree(_processedRecordHandle);
                    _processedRecordHandle = 0;
                }

                if (_monitorPlaybackHandle != 0)
                {
                    Bass.StreamFree(_monitorPlaybackHandle);
                    _monitorPlaybackHandle = 0;
                }

                // Free the recording device
                if (_initialized)
                {
                    Bass.CurrentRecordingDevice = _deviceId;
                    Bass.RecordFree();
                }

                _disposed = true;
            }
        }
    }
}