﻿using System;
using System.Collections.Concurrent;
using ManagedBass;
using ManagedBass.Fx;
using UnityEngine;
using YARG.Audio.BASS;
using YARG.Audio.PitchDetection;
using YARG.Input;
using YARG.Settings;

namespace YARG.Audio
{
    public class BassMicDevice : IMicDevice
    {
        // How often to record samples from the microphone in milliseconds (calls the callback function every n millis)
        private const int RECORD_PERIOD_MILLIS = 50;

        public float PitchUpdatesPerSecond => 1000f / RECORD_PERIOD_MILLIS;

        public string DisplayName => _deviceInfo.Name;
        public bool IsDefault => _deviceInfo.IsDefault;

        public bool IsMonitoring { get; set; }
        public bool IsRecordingOutput { get; set; }

        public MicOutputFrame? LastOutputFrame { get; private set; }

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
                Debug.LogError($"Failed to initialize recording device: {Bass.LastError}");
                return (int) Bass.LastError;
            }

            const BassFlags FLAGS = BassFlags.Default;

            // We want to start recording immediately because of device context switching and device numbers.
            // If we initialize the device but don't record immediately, the device number might change
            // and we'll be recording from the wrong device.
            _cleanRecordHandle = Bass.RecordStart(44100, info.Channels, FLAGS, RECORD_PERIOD_MILLIS,
                ProcessCleanRecordData, IntPtr.Zero);
            _processedRecordHandle = Bass.RecordStart(44100, info.Channels, FLAGS, RECORD_PERIOD_MILLIS,
                ProcessRecordData, IntPtr.Zero);
            if (_cleanRecordHandle == 0 || _processedRecordHandle == 0)
            {
                Debug.LogError($"Failed to start recording: {Bass.LastError}");
                Dispose();
                return (int) Bass.LastError;
            }

            // Add EQ
            int lowEqHandle = BassHelpers.AddEqToChannel(_processedRecordHandle, _lowEqParameters);
            int highEqHandle = BassHelpers.AddEqToChannel(_processedRecordHandle, _highEqParameters);
            if (lowEqHandle == 0 || highEqHandle == 0)
            {
                Debug.LogError($"Failed to add EQ to recording stream!");
                Dispose();
                return (int) Bass.LastError;
            }

            // Set up monitoring stream
            _monitorPlaybackHandle = Bass.CreateStream(44100, info.Channels, FLAGS, StreamProcedureType.Push);
            if (_monitorPlaybackHandle == 0)
            {
                Debug.LogError($"Failed to create monitor stream: {Bass.LastError}");
                Dispose();
                return (int) Bass.LastError;
            }

            // Add reverb to the monitor playback
            int reverbHandle = BassHelpers.FXAddParameters(_monitorPlaybackHandle, EffectType.Freeverb,
                _monitoringReverbParameters, 1);
            if (reverbHandle == 0)
            {
                Debug.LogError($"Failed to add reverb to monitor stream!");
                Dispose();
                return (int) Bass.LastError;
            }

            // Apply gain to the playback
            _applyGainHandle = Bass.ChannelSetDSP(_monitorPlaybackHandle, ApplyGain);
            if (_applyGainHandle == 0)
            {
                Debug.LogError($"Failed to add gain to monitor stream: {Bass.LastError}");
                Dispose();
                return (int) Bass.LastError;
            }

            // Start monitoring
            if (!Bass.ChannelPlay(_monitorPlaybackHandle))
            {
                Debug.LogError($"Failed to start monitor stream: {Bass.LastError}");
                Dispose();
                return (int) Bass.LastError;
            }

            IsMonitoring = true;
            SetMonitoringLevel(SettingsManager.Settings.VocalMonitoring.Data);

            _pitchDetector = new PitchTracker();

            _initialized = true;
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
                Debug.LogError($"Failed to set volume attribute: {Bass.LastError}");
            }
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
            if (amplitude < SettingsManager.Settings.MicrophoneSensitivity.Data)
            {
                // Send a false mic output frame with no pitch
                LastOutputFrame = new MicOutputFrame(
                    InputManager.CurrentInputTime, 0f, amplitude);
                return;
            }

            // Process the pitch buffer
            var pitchOutput = _pitchDetector.ProcessBuffer(floatBuffer);
            if (pitchOutput == null)
            {
                return;
            }

            // Queue a MicOutput frame
            var frame = new MicOutputFrame(
                InputManager.CurrentInputTime, pitchOutput.Value, amplitude);
            LastOutputFrame = frame;
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