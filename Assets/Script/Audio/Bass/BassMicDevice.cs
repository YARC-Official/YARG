using System;
using ManagedBass;
using ManagedBass.Fx;
using UnityEngine;
using YARG.Audio.BASS;
using YARG.Audio.PitchDetection;
using YARG.Settings;

namespace YARG.Audio {
	public class BassMicDevice : IMicDevice {
		// How often to record samples from the microphone in milliseconds (calls the callback function every n millis)
		private const int RECORD_PERIOD_MILLIS = 50;

		public float PitchUpdatesPerSecond => 1000f / RECORD_PERIOD_MILLIS;

		public string DisplayName => _deviceInfo.Name;
		public bool IsDefault => _deviceInfo.IsDefault;

		public bool IsMonitoring { get; set; }

		public float Pitch { get; private set; }
		public float Amplitude { get; private set; }
		public bool VoiceDetected => Amplitude > SettingsManager.Settings.MicrophoneSensitivity.Data;

		private int _deviceId;
		private DeviceInfo _deviceInfo;

		private int _cleanRecordHandle;
		private int _processedRecordHandle;
		private int _monitorPlaybackHandle;

		private bool _initialized;
		private bool _disposed;

		private RecordProcedure _cleanRecordProcedure;
		private RecordProcedure _processedRecordProcedure;
		private DSPProcedure _monitoringGainProcedure;

		private PitchTracker _pitchDetector;

		private readonly ReverbParameters _monitoringReverbParameters = new() {
			fDryMix = 0.3f,
			fWetMix = 1f,
			fRoomSize = 0.4f,
			fDamp = 0.7f
		};

		public BassMicDevice(int deviceId, DeviceInfo info) {
			_deviceId = deviceId;
			_deviceInfo = info;
		}

		public int Initialize() {
			if(_initialized || _disposed)
				return 0;

			// Callback function to process any samples received from recording device
			_cleanRecordProcedure += ProcessCleanRecordData;
			_processedRecordProcedure += ProcessRecordData;

			// Must initialise device before recording
			Bass.RecordInit(_deviceId);
			Bass.RecordGetInfo(out var info);

			const BassFlags flags = BassFlags.Default;

			// We want to start recording immediately because of device context switching and device numbers.
			// If we initialize the device but don't record immediately, the device number might change and we'll be recording from the wrong device.
			_cleanRecordHandle = Bass.RecordStart(44100, info.Channels, flags, RECORD_PERIOD_MILLIS, _cleanRecordProcedure, IntPtr.Zero);
			_processedRecordHandle = Bass.RecordStart(44100, info.Channels, flags, RECORD_PERIOD_MILLIS, _processedRecordProcedure, IntPtr.Zero);
			if(_cleanRecordHandle == 0 || _processedRecordHandle == 0) {
				// If we failed to start recording, we need to return the error code.
				_initialized = false;
				Debug.LogError($"Failed to start recording: {Bass.LastError}");
				return (int) Bass.LastError;
			}

			int lowEqHandle = Bass.ChannelSetFX(_processedRecordHandle, EffectType.PeakEQ, 0);
			int highEqHandle = Bass.ChannelSetFX(_processedRecordHandle, EffectType.PeakEQ, 0);
			Bass.FXSetParameters(lowEqHandle, new PeakEQParameters {
				fBandwidth = 2.5f,
				fCenter = 20f,
				fGain = -10f
			});
			Bass.FXSetParameters(highEqHandle, new PeakEQParameters {
				fBandwidth = 2.5f,
				fCenter = 10_000f,
				fGain = -10f
			});

			_monitorPlaybackHandle = Bass.CreateStream(44100, info.Channels, flags, StreamProcedureType.Push);
			if(_monitorPlaybackHandle == 0) {
				_initialized = false;
				Debug.LogError($"Failed to create monitor stream: {Bass.LastError}");
				return (int) Bass.LastError;
			}

			// Add reverb to the monitor playback
			int reverbHandle = Bass.ChannelSetFX(_monitorPlaybackHandle, EffectType.Freeverb, 1);
			if(reverbHandle == 0) {
				_initialized = false;
				Debug.LogError($"Failed to add reverb to monitor stream: {Bass.LastError}");
				return (int) Bass.LastError;
			}
			Bass.FXSetParameters(reverbHandle, _monitoringReverbParameters);

			_monitoringGainProcedure = (_, _, buffer, length, _) => BassHelpers.ApplyGain(1.3f, buffer, length);

			Bass.ChannelSetDSP(_monitorPlaybackHandle, _monitoringGainProcedure);
			Bass.ChannelPlay(_monitorPlaybackHandle);

			IsMonitoring = true;

			SetMonitoringLevel(SettingsManager.Settings.VocalMonitoring.Data);

			_pitchDetector = new PitchTracker();

			_initialized = true;

			return 0;
		}

		public void SetMonitoringLevel(float volume) {
			if(_monitorPlaybackHandle == 0)
				return;

			if (!Bass.ChannelSetAttribute(_monitorPlaybackHandle, ChannelAttribute.Volume, volume)) {
				Debug.LogError($"Failed to set volume attrib: {Bass.LastError}");
			}
		}

		private bool ProcessCleanRecordData(int handle, IntPtr buffer, int length, IntPtr user) {
			// Wait for initialization to complete before processing data
			if (!_initialized) {
				return true;
			}

			// Copies the data from the recording buffer to the monitor playback buffer.
			if (IsMonitoring) {
				Bass.StreamPutData(_monitorPlaybackHandle, buffer, length);
			}

			return true;
		}

		private bool ProcessRecordData(int handle, IntPtr buffer, int length, IntPtr user) {
			// Wait for initialization to complete before processing data
			if (!_initialized) {
				return true;
			}

			CalculatePitchAndAmplitude(buffer, length);
			return true;
		}

		private unsafe void CalculatePitchAndAmplitude(IntPtr buffer, int byteLength) {
			int sampleCount = byteLength / sizeof(short);
			float* floatBuffer = stackalloc float[sampleCount];

			// Convert 16 bit buffer to floats
			// If this isn't 16 bit god knows what device they're using.
			var shortBufferSpan = new ReadOnlySpan<short>((short*) buffer, sampleCount);
			for (int i = 0; i < sampleCount; i++) {
				floatBuffer[i] = shortBufferSpan[i] / 32768f;
			}

			var bufferSpan = new ReadOnlySpan<float>(floatBuffer, sampleCount);

			// Calculate the root mean square
			float sum = 0f;
			int count = 0;
			for (int i = 0; i < sampleCount; i += 4, count++) {
				sum += bufferSpan[i] * bufferSpan[i];
			}
			sum = Mathf.Sqrt(sum / count);

			// Convert to decibels to get the amplitude
			Amplitude = 20f * Mathf.Log10(sum * 180f);
			if (Amplitude < -160f) {
				Amplitude = -160f;
			}

			// Skip pitch detection if not speaking
			if (!VoiceDetected) {
				return;
			}

			// Process the pitch buffer
			var pitchOutput = _pitchDetector.ProcessBuffer(bufferSpan);
			if (pitchOutput != null) {
				Pitch = pitchOutput.Value;
			}
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {
			if (!_disposed) {
				// Free managed resources here
				if (disposing) {

				}

				// Free unmanaged resources here
				if (_cleanRecordHandle != 0) {
					Bass.ChannelStop(_cleanRecordHandle);
					Bass.StreamFree(_cleanRecordHandle);
					_cleanRecordHandle = 0;
				}

				if (_processedRecordHandle != 0) {
					Bass.ChannelStop(_processedRecordHandle);
					Bass.StreamFree(_processedRecordHandle);
					_processedRecordHandle = 0;
				}

				if (_monitorPlaybackHandle != 0) {
					Bass.StreamFree(_monitorPlaybackHandle);
					_monitorPlaybackHandle = 0;
				}

				_disposed = true;
			}
		}
	}
}