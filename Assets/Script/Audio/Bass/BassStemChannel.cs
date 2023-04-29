using System;
using System.Collections.Generic;
using ManagedBass;
using ManagedBass.DirectX8;
using ManagedBass.Fx;
using ManagedBass.Mix;

namespace YARG {
	public class BassStemChannel : IStemChannel {

		private const EffectType REVERB_TYPE = EffectType.DXReverb;

		public SongStem Stem { get; }
		public double LengthD { get; private set; }

		public double Volume { get; private set; }

		public int StreamHandle { get; private set; }
		public int ReverbStreamHandle { get; private set; }

		private readonly string _path;
		private readonly IAudioManager _manager;

		private readonly Dictionary<EffectType, int> _effects;
		private readonly Dictionary<DSPType, int> _dspHandles;

		private IEffectParameter _eqLowParams;
		private IEffectParameter _eqMidParams;
		private IEffectParameter _eqHighParams;
		
		//private readonly DSPProcedure _dspGain;

		private double _lastStemVolume;

		private int _sourceHandle;
		
		private bool _isReverbing;
		private bool _disposed;

		public BassStemChannel(IAudioManager manager, string path, SongStem stem) {
			_manager = manager;
			_path = path;
			Stem = stem;

			Volume = 1;

			_lastStemVolume = _manager.GetVolumeSetting(Stem);
			_effects = new Dictionary<EffectType, int>();
			_dspHandles = new Dictionary<DSPType, int>();

			SetupEqParams();
			
			//_dspGain += GainDSP;
		}

		~BassStemChannel() {
			Dispose(false);
		}

		public int Load(bool isSpeedUp, float speed) {
			if (_disposed) {
				return -1;
			}
			if (StreamHandle != 0) {
				return 0;
			}

			_sourceHandle = Bass.CreateStream(_path, 0, 0, BassFlags.Prescan | BassFlags.Decode | BassFlags.AsyncFile);
			
			if (_sourceHandle == 0) {
				return (int) Bass.LastError;
			}
			
			int main = BassMix.CreateSplitStream(_sourceHandle, BassFlags.Decode | BassFlags.SplitPosition, null);
			int reverbSplit = BassMix.CreateSplitStream(_sourceHandle, BassFlags.Decode | BassFlags.SplitPosition, null);

			const BassFlags flags = BassFlags.SampleOverrideLowestVolume | BassFlags.Decode | BassFlags.FxFreeSource;
			
			StreamHandle = BassFx.TempoCreate(main, flags);
			ReverbStreamHandle = BassFx.TempoCreate(reverbSplit, flags);

			Bass.ChannelSetAttribute(StreamHandle, ChannelAttribute.Volume, _manager.GetVolumeSetting(Stem));
			Bass.ChannelSetAttribute(ReverbStreamHandle, ChannelAttribute.Volume, 0);

			if (isSpeedUp) {
				// Gets relative speed from 100% (so 1.05f = 5% increase)
				float relativeSpeed = Math.Abs(speed) * 100;
				relativeSpeed -= 100;
				Bass.ChannelSetAttribute(StreamHandle, ChannelAttribute.Tempo, relativeSpeed);
				Bass.ChannelSetAttribute(ReverbStreamHandle, ChannelAttribute.Tempo, relativeSpeed);

				// Have to handle pitch separately for some reason
				if (_manager.IsChipmunkSpeedup) {
					// Calculates semitone increase, can probably be improved but this will do for now
					float semitones = relativeSpeed > 0 ? 1 * speed : -1 * speed;
					Bass.ChannelSetAttribute(StreamHandle, ChannelAttribute.Pitch, semitones);
					Bass.ChannelSetAttribute(ReverbStreamHandle, ChannelAttribute.Pitch, semitones);
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
				return;
			}

			Volume = newVolume;
			_lastStemVolume = volumeSetting;

			Bass.ChannelSetAttribute(StreamHandle, ChannelAttribute.Volume, newBassVol);
			
			if (_isReverbing) {
				Bass.ChannelSetAttribute(ReverbStreamHandle, ChannelAttribute.Volume, newBassVol);
			} else {
				Bass.ChannelSetAttribute(ReverbStreamHandle, ChannelAttribute.Volume, 0);
			}
		}

		public void SetReverb(bool reverb) {
			_isReverbing = reverb;
			if (reverb) {
				// Reverb already applied
				if (_effects.ContainsKey(REVERB_TYPE))
					return;

				// Set reverb FX
				int reverbFxHandle = AddReverbToChannel();
				int lowEqHandle = AddEqToChannel(_eqLowParams);
				int midEqHandle = AddEqToChannel(_eqMidParams);
				int highEqHandle = AddEqToChannel(_eqHighParams);
				//int gainDspHandle = Bass.ChannelSetDSP(StreamHandle, _dspGain);
				
				double volumeSetting = _manager.GetVolumeSetting(Stem);
				Bass.ChannelSetAttribute(ReverbStreamHandle, ChannelAttribute.Volume,volumeSetting * Volume);

				_effects.Add(REVERB_TYPE, reverbFxHandle);
				
				// Add low-high
				_effects.Add(EffectType.PeakEQ, lowEqHandle);
				_effects.Add(EffectType.PeakEQ + 1, midEqHandle);
				_effects.Add(EffectType.PeakEQ + 2, highEqHandle);
				//_dspHandles.Add(DSPType.Gain, gainDspHandle);
			} else {
				// No reverb is applied
				if (!_effects.ContainsKey(REVERB_TYPE)) {
					return;
				}

				Bass.ChannelRemoveFX(ReverbStreamHandle, _effects[REVERB_TYPE]);
				
				// Remove low-high
				Bass.ChannelRemoveFX(ReverbStreamHandle, _effects[EffectType.PeakEQ]);
				Bass.ChannelRemoveFX(ReverbStreamHandle, _effects[EffectType.PeakEQ + 1]);
				Bass.ChannelRemoveFX(ReverbStreamHandle, _effects[EffectType.PeakEQ + 2]);
				//Bass.ChannelRemoveDSP(ReverbStreamHandle, _dspHandles[DSPType.Gain]);
				Bass.ChannelSetAttribute(ReverbStreamHandle, ChannelAttribute.Volume, 0);

				_effects.Remove(REVERB_TYPE);
				
				// Remove low-high
				_effects.Remove(EffectType.PeakEQ);
				_effects.Remove(EffectType.PeakEQ + 1);
				_effects.Remove(EffectType.PeakEQ + 2);
				//_dspHandles.Remove(DSPType.Gain);
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

				if (ReverbStreamHandle != 0) {
					Bass.StreamFree(ReverbStreamHandle);
					ReverbStreamHandle = 0;
				}

				if (_sourceHandle != 0) {
					Bass.StreamFree(_sourceHandle);
					_sourceHandle = 0;
				}

				_disposed = true;
			}
		}

		private int AddReverbToChannel() {
			// Set reverb FX
			int reverbFxHandle = Bass.ChannelSetFX(ReverbStreamHandle, REVERB_TYPE, 0);
			if (reverbFxHandle == 0) {
				return 0;
			}

			IEffectParameter reverbParams = REVERB_TYPE switch {
				EffectType.DXReverb => new DXReverbParameters {
					fInGain = -2f, fReverbMix = -1f, fReverbTime = 1000.0f, fHighFreqRTRatio = 0.001f
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

			return !Bass.FXSetParameters(reverbFxHandle, reverbParams) ? 0 : reverbFxHandle;
		}

		private int AddEqToChannel(IEffectParameter eqParams) {
			int eqHandle = Bass.ChannelSetFX(ReverbStreamHandle, EffectType.PeakEQ, 1);
			if (eqHandle == 0) {
				return 0;
			}
			
			return !Bass.FXSetParameters(eqHandle, eqParams) ? 0 : eqHandle;
		}

		private void SetupEqParams() {
			_eqLowParams = new PeakEQParameters {
				fBandwidth = 2.0f,
				fCenter = 500.0f,
				fGain = -20f
			};
			
			_eqMidParams = new PeakEQParameters {
				fBandwidth = 2.0f,
				fCenter = 1500.0f,
				fGain = -5f
			};
			
			_eqHighParams = new PeakEQParameters {
				fBandwidth = 3.0f,
				fCenter = 5000.0f,
				fGain = 4f
			};
		}
		
		public static unsafe void GainDSP(int handle, int channel, IntPtr buffer, int length, IntPtr user) {
			var bufferPtr = (float*) buffer;
			int samples = length / 4;

			for (int i = 0; i < samples; i++) {
				bufferPtr![i] *= 1.3f;
			}
		}
	}
}