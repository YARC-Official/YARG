﻿using System;
using ManagedBass;
using UnityEngine;

namespace YARG {
	public class BassMicDevice : IMicDevice {

		// How often to record samples from the microphone in milliseconds (calls the callback function every n millis)
		private const int RECORD_PERIOD_MILLIS = 10;
		
		public float Pitch { get; private set; }
		
		public float Amplitude { get; private set; }
		
		private int _recordHandle;
		private int _monitorPlaybackHandle;
		
		private bool _initialized;
		private bool _isPlayback;
		private bool _disposed;
		
		private RecordProcedure _recordProcedure;

		public int Initialize(int device) {
			if(_initialized || _disposed) 
				return 0;

			// Callback function to process any samples received from recording device
			_recordProcedure += ProcessRecordData;
			
			// Must initialise device before recording
			Bass.RecordInit(device);
			Bass.RecordGetInfo(out var info);
			
			const BassFlags flags = BassFlags.Float;
			
			// We want to start recording immediately because of device context switching and device numbers.
			// If we initialize the device but don't record immediately, the device number might change and we'll be recording from the wrong device.
			_recordHandle = Bass.RecordStart(0, 0, flags, RECORD_PERIOD_MILLIS, _recordProcedure, IntPtr.Zero);
			if(_recordHandle == 0) {
				// If we failed to start recording, we need to return the error code.
				_initialized = false;
				Debug.LogError($"Failed to start recording: {Bass.LastError}");
				return (int) Bass.LastError;;
			}

			_monitorPlaybackHandle = Bass.CreateStream(44100, info.Channels, BassFlags.Float, StreamProcedureType.Push);
			if(_monitorPlaybackHandle == 0) {
				_initialized = false;
				Debug.LogError($"Failed to create monitor stream: {Bass.LastError}");
				return (int) Bass.LastError;
			}

			StartPlayback();
			SetMonitoringLevel(1);

			_initialized = true;
			return 0;
		}

		public bool StartPlayback() {
			// True clears buffer before playback
			_isPlayback = Bass.ChannelPlay(_monitorPlaybackHandle, true);

			if (!_isPlayback) {
				Debug.LogError($"{Bass.LastError}");
			}
			
			return _isPlayback;
		}

		public void SetMonitoringLevel(float volume) {
			if(_monitorPlaybackHandle == 0) 
				return;

			if (!Bass.ChannelSetAttribute(_monitorPlaybackHandle, ChannelAttribute.Volume, volume)) {
				Debug.LogError($"Failed to set volume attrib: {Bass.LastError}");
			}
		}

		private bool ProcessRecordData(int handle, IntPtr buffer, int length, IntPtr user) {
			// Copies the data from the recording buffer to the monitor playback buffer.
			if (_isPlayback) {
				Bass.StreamPutData(_monitorPlaybackHandle, buffer, length);
			}
			
			CalculatePitchAndAmplitude(buffer, length);
			return true;
		}
		
		private static unsafe void CalculatePitchAndAmplitude(IntPtr buffer, int length) {
			var bufferPtr = (float*) buffer;
			int samples = length / 4;

			for (int i = 0; i < samples; i++) {
				// Access sample index by: bufferPtr![i]
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
				if (_recordHandle != 0) {
					Bass.ChannelStop(_recordHandle);
					Bass.StreamFree(_recordHandle);
					_recordHandle = 0;
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