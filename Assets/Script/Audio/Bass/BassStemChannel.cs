using System;
using System.Collections.Generic;
using ManagedBass;
using ManagedBass.Fx;
using ManagedBass.Mix;

namespace YARG {
	public class BassStemChannel : IStemChannel {

		private const EffectType REVERB_TYPE = EffectType.Freeverb;

		public SongStem Stem { get; }
		public double LengthD { get; private set; }

		public double Volume { get; private set; }

		public int StreamHandle { get; private set; }
		public int ReverbStreamHandle { get; private set; }

		private readonly string _path;
		private readonly IAudioManager _manager;

		private readonly Dictionary<EffectType, int> _effects;

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

			// Apply a compressor to balance stem volume
			Bass.ChannelSetFX(StreamHandle, EffectType.Compressor, 1);
			Bass.ChannelSetFX(ReverbStreamHandle, EffectType.Compressor, 1);

			var compressorParams = new CompressorParameters {
				fGain = -3,
				fThreshold = -2,
				fAttack = 0.01f,
				fRelease = 0.1f,
				fRatio = 4,
			};

			Bass.FXSetParameters(StreamHandle, compressorParams);
			Bass.FXSetParameters(ReverbStreamHandle, compressorParams);

			Bass.ChannelSetAttribute(StreamHandle, ChannelAttribute.Volume, _manager.GetVolumeSetting(Stem));
			Bass.ChannelSetAttribute(ReverbStreamHandle, ChannelAttribute.Volume, 0);

			if (isSpeedUp) {
				// Gets relative speed from 100% (so 1.05f = 5% increase)
				float percentageSpeed = Math.Abs(speed) * 100;
				float relativeSpeed = percentageSpeed - 100;
				
				Bass.ChannelSetAttribute(StreamHandle, ChannelAttribute.Tempo, relativeSpeed);
				Bass.ChannelSetAttribute(ReverbStreamHandle, ChannelAttribute.Tempo, relativeSpeed);

				// Have to handle pitch separately for some reason
				if (_manager.IsChipmunkSpeedup) {
					float semitoneShift = percentageSpeed switch {
						> 100 => percentageSpeed / 9 - 100 / 9,
						< 100 => percentageSpeed / 3 - 100 / 3,
						_     => 0
					};

					semitoneShift = Math.Clamp(semitoneShift, -60, 60);

					Bass.ChannelSetAttribute(StreamHandle, ChannelAttribute.Pitch, semitoneShift);
					Bass.ChannelSetAttribute(ReverbStreamHandle, ChannelAttribute.Pitch, semitoneShift);
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
				Bass.ChannelSlideAttribute(ReverbStreamHandle, ChannelAttribute.Volume, (float)(newBassVol * 0.7), 1);
			} else {
				Bass.ChannelSlideAttribute(ReverbStreamHandle, ChannelAttribute.Volume, 0, 1);
			}
		}

		public void SetReverb(bool reverb) {
			_isReverbing = reverb;
			if (reverb) {
				// Reverb already applied
				if (_effects.ContainsKey(REVERB_TYPE))
					return;

				// Set reverb FX
				int lowEqHandle = BassHelpers.AddEqToChannel(ReverbStreamHandle, BassHelpers.LowEqParams);
				int midEqHandle = BassHelpers.AddEqToChannel(ReverbStreamHandle, BassHelpers.MidEqParams);
				int highEqHandle = BassHelpers.AddEqToChannel(ReverbStreamHandle, BassHelpers.HighEqParams);
				int reverbFxHandle = BassHelpers.AddReverbToChannel(ReverbStreamHandle);
				
				double volumeSetting = _manager.GetVolumeSetting(Stem);
				//Bass.ChannelSetAttribute(ReverbStreamHandle, ChannelAttribute.Volume,volumeSetting * Volume * 0.7);
				Bass.ChannelSlideAttribute(ReverbStreamHandle, ChannelAttribute.Volume,(float)(volumeSetting * Volume * 0.7f), 200);

				_effects.Add(REVERB_TYPE, reverbFxHandle);
				
				// Add low-high
				_effects.Add(EffectType.PeakEQ, lowEqHandle);
				_effects.Add(EffectType.PeakEQ + 1, midEqHandle);
				_effects.Add(EffectType.PeakEQ + 2, highEqHandle);
			} else {
				// No reverb is applied
				if (!_effects.ContainsKey(REVERB_TYPE)) {
					return;
				}

				// Remove low-high
				Bass.ChannelRemoveFX(ReverbStreamHandle, _effects[EffectType.PeakEQ]);
				Bass.ChannelRemoveFX(ReverbStreamHandle, _effects[EffectType.PeakEQ + 1]);
				Bass.ChannelRemoveFX(ReverbStreamHandle, _effects[EffectType.PeakEQ + 2]);
				Bass.ChannelRemoveFX(ReverbStreamHandle, _effects[REVERB_TYPE]);
				
				//Bass.ChannelSetAttribute(ReverbStreamHandle, ChannelAttribute.Volume, 0);
				Bass.ChannelSlideAttribute(ReverbStreamHandle, ChannelAttribute.Volume, 0, 250);

				_effects.Remove(REVERB_TYPE);
				
				// Remove low-high
				_effects.Remove(EffectType.PeakEQ);
				_effects.Remove(EffectType.PeakEQ + 1);
				_effects.Remove(EffectType.PeakEQ + 2);
			}
		}

		public double GetPosition() {
			return Bass.ChannelBytes2Seconds(StreamHandle, Bass.ChannelGetPosition(StreamHandle));
		}

		public double GetLengthInSeconds() {
			return BassHelpers.GetChannelLengthInSeconds(StreamHandle);
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
	}
}