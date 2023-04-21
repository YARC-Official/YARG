using System;
using System.Collections.Generic;
using ManagedBass;
using ManagedBass.DirectX8;
using ManagedBass.Fx;
using UnityEngine;

namespace YARG {
	public class BassStemChannel : IStemChannel {
		
		private const EffectType REVERB_TYPE = EffectType.DXReverb;
		
		public SongStem Stem { get; }
		public double LengthD { get; private set; }
		
		public double Volume { get; private set; }
		
		public int StreamHandle { get; private set; }

		private readonly string _path;
		private readonly IAudioManager _manager;

		private readonly Dictionary<EffectType, int> _effects;
		private readonly Dictionary<DSPType, int> _dspHandles;

		private readonly DSPProcedure _dspGain;

		private double _lastStemVolume;
		
		private bool _disposed;

		public BassStemChannel(IAudioManager manager, string path, SongStem stem) {
			_manager = manager;
			_path = path;
			Stem = stem;

			Volume = 1;

			_lastStemVolume = _manager.GetVolumeSetting(Stem);
			_effects = new Dictionary<EffectType, int>();
			_dspHandles = new Dictionary<DSPType, int>();
			
			_dspGain += GainDSP;
		}
		
		~BassStemChannel() {
			Dispose(false);
		}


		public int Load(bool isSpeedUp, float speed) {
			if (StreamHandle != 0) {
				return 0;
			}
			
			int streamHandle = Bass.CreateStream(_path, 0, 0, BassFlags.Prescan | BassFlags.Decode | BassFlags.AsyncFile);
			if (streamHandle == 0) {
				return (int)Bass.LastError;
			}

			const BassFlags flags = BassFlags.SampleOverrideLowestVolume | BassFlags.Decode | BassFlags.FxFreeSource;
			StreamHandle = BassFx.TempoCreate(streamHandle, flags);

			Bass.ChannelSetAttribute(StreamHandle, ChannelAttribute.Volume, _manager.GetVolumeSetting(Stem));

			if (isSpeedUp) {
				// Gets relative speed from 100% (so 1.05f = 5% increase)
				float relativeSpeed = Math.Abs(speed) * 100;
				relativeSpeed -= 100;
				Bass.ChannelSetAttribute(StreamHandle, ChannelAttribute.Tempo, relativeSpeed);

				// Have to handle pitch separately for some reason
				if (_manager.IsChipmunkSpeedup) {
					// Calculates semitone increase, can probably be improved but this will do for now
					float semitones = relativeSpeed > 0 ? 1 * speed : -1 * speed;
					Bass.ChannelSetAttribute(StreamHandle, ChannelAttribute.Pitch, semitones);
				}
			}

			LengthD = GetLengthInSeconds();
			
			return 0;
		}

		public void SetVolume(double newVolume) {
			if (StreamHandle == 0) {
				return;
			}

			double volumeSetting = _manager.GetVolumeSetting(Stem);
			
			double oldBassVol = _lastStemVolume * Volume;
			double newBassVol = volumeSetting * newVolume;
			
			// Values are the same, no need to change
			if (Math.Abs(oldBassVol - newBassVol) < double.Epsilon) {
				Debug.Log($"{Stem} values same");
				return;
			}
			
			Debug.Log($"Updated {Stem} volume to {newVolume}");
			
			Volume = newVolume;
			_lastStemVolume = volumeSetting;
			
			Bass.ChannelSetAttribute(StreamHandle, ChannelAttribute.Volume, newBassVol);
		}
		
		public void SetReverb(bool reverb) {
			if (reverb) {
				// Reverb already applied
				if (_effects.ContainsKey(REVERB_TYPE))
					return;

				// Set reverb FX
				int reverbHandle = AddReverbToChannel();
				int gainDspHandle = Bass.ChannelSetDSP(StreamHandle, _dspGain);

				_effects.Add(REVERB_TYPE, reverbHandle);
				_dspHandles.Add(DSPType.Gain, gainDspHandle);
			} else {
				// No reverb is applied
				if (!_effects.ContainsKey(REVERB_TYPE)) {
					return;
				}

				Bass.ChannelRemoveFX(StreamHandle, _effects[REVERB_TYPE]);
				Bass.ChannelRemoveDSP(StreamHandle, _dspHandles[DSPType.Gain]);

				_effects.Remove(REVERB_TYPE);
				_dspHandles.Remove(DSPType.Gain);
			}
		}
		
		public double GetPosition() {
			return Bass.ChannelBytes2Seconds(StreamHandle, Bass.ChannelGetPosition(StreamHandle));
		}

		public double GetLengthInSeconds() {
			if (StreamHandle == 0) {
				return 0;
			}
			
			long length = Bass.ChannelGetLength(StreamHandle);
			
			if (length == -1) {
				return (double) Bass.LastError;
			}
			
			double seconds = Bass.ChannelBytes2Seconds(StreamHandle, length);
			
			if (seconds < 0) {
				return (double) Bass.LastError;
			}
			
			return seconds;
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
				if (StreamHandle != 0) {
					Bass.StreamFree(StreamHandle);
					StreamHandle = 0;
				}
				
				_disposed = true;
			}
		}
		
		private int AddReverbToChannel() {
			// Set reverb FX
			int reverbHandle = Bass.ChannelSetFX(StreamHandle, REVERB_TYPE, 0);
			if (reverbHandle == 0) {
				return 0;
			}

			IEffectParameter reverbParams = REVERB_TYPE switch {
				EffectType.DXReverb => new DXReverbParameters {
					fInGain = 0.0f, fReverbMix = -5f, fReverbTime = 1000.0f, fHighFreqRTRatio = 0.001f
				},
				EffectType.Freeverb => new ReverbParameters() {
					fDryMix = 1f,
					fWetMix = 2f,
					fRoomSize = 0.5f,
					fDamp = 0.2f,
					fWidth = 1.0f,
					lMode = 0
				},
				_ => throw new ArgumentOutOfRangeException()
			};

			return !Bass.FXSetParameters(reverbHandle, reverbParams) ? 0 : reverbHandle;
		}
		
		private static unsafe void GainDSP(int handle, int channel, IntPtr buffer, int length, IntPtr user) {
			var bufferPtr = (float*) buffer;
			int samples = length / 4;

			for (int i = 0; i < samples; i++) {
				bufferPtr![i] *= 1.3f;
			}
		}
	}
}