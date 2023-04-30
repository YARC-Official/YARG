using System;
using System.Collections.Generic;
using ManagedBass;
using ManagedBass.Fx;
using ManagedBass.Mix;
using UnityEngine;

namespace YARG {
	public class BassMoggStem : IStemChannel {
		
		private const EffectType REVERB_TYPE = EffectType.DXReverb;
		
		public SongStem Stem { get; }
		public double LengthD { get; private set; }
		public double Volume { get; private set; }
		
		public int[] BassChannels { get; }
		public int[] ReverbChannels { get; }
		public int[] ChannelIndexes { get; }
		
		private readonly IAudioManager _manager;

		private readonly int[] _splitStreams;

		private readonly Dictionary<int, Dictionary<EffectType, int>> _effects;

		private readonly DSPProcedure _dspGain;

		private int _leadChannel;
		
		private double _lastStemVolume;

		private bool _isReverbing;
		private bool _isLoaded;
		private bool _disposed;

		public BassMoggStem(IAudioManager manager, SongStem stem, int[] splitStreams) {
			_manager = manager;
			Stem = stem;

			_splitStreams = splitStreams;
			BassChannels = new int[splitStreams.Length];
			ReverbChannels = new int[splitStreams.Length];
			ChannelIndexes = new int[splitStreams.Length];
			
			Volume = 1;
			
			_lastStemVolume = _manager.GetVolumeSetting(Stem);
			_effects = new Dictionary<int, Dictionary<EffectType, int>>();
		}
		
		~BassMoggStem() {
			Dispose(false);
		}
		
		public int Load(bool isSpeedUp, float speed) {
			if (_disposed) {
				return -1;
			}
			if (_isLoaded) {
				return 0;
			}
			
			const BassFlags flags = BassFlags.SampleOverrideLowestVolume | BassFlags.Decode | BassFlags.FxFreeSource;
			
			for (var i = 0; i < _splitStreams.Length; i++) {
				int main = BassMix.CreateSplitStream(_splitStreams[i], BassFlags.Decode | BassFlags.SplitPosition, null);
				int reverbSplit = BassMix.CreateSplitStream(_splitStreams[i], BassFlags.Decode | BassFlags.SplitPosition, null);
				
				BassChannels[i] = BassFx.TempoCreate(main, flags);
				ReverbChannels[i] = BassFx.TempoCreate(reverbSplit, flags);
				
				// Apply a compressor to balance stem volume
				Bass.ChannelSetFX(BassChannels[i], EffectType.Compressor, 1);
				Bass.ChannelSetFX(ReverbChannels[i], EffectType.Compressor, 1);

				var compressorParams = new CompressorParameters {
					fGain = -3,
					fThreshold = -2,
					fAttack = 0.01f,
					fRelease = 0.1f,
					fRatio = 4,
				};

				Bass.FXSetParameters(BassChannels[i], compressorParams);
				Bass.FXSetParameters(ReverbChannels[i], compressorParams);
				
				Bass.ChannelSetAttribute(BassChannels[i], ChannelAttribute.Volume, _manager.GetVolumeSetting(Stem));
				Bass.ChannelSetAttribute(ReverbChannels[i], ChannelAttribute.Volume, 0);

				if (isSpeedUp) {
					// Gets relative speed from 100% (so 1.05f = 5% increase)
					float relativeSpeed = Math.Abs(speed) * 100;
					relativeSpeed -= 100;
					Bass.ChannelSetAttribute(BassChannels[i], ChannelAttribute.Tempo, relativeSpeed);
					Bass.ChannelSetAttribute(ReverbChannels[i], ChannelAttribute.Tempo, relativeSpeed);

					// Have to handle pitch separately for some reason
					if (_manager.IsChipmunkSpeedup) {
						// Calculates semitone increase, can probably be improved but this will do for now
						float semitones = relativeSpeed > 0 ? 1 * speed : -1 * speed;
						Bass.ChannelSetAttribute(BassChannels[i], ChannelAttribute.Pitch, semitones);
						Bass.ChannelSetAttribute(ReverbChannels[i], ChannelAttribute.Pitch, semitones);
					}
				}

				// Get longest channel as part of this stem
				double length = BassHelpers.GetChannelLengthInSeconds(BassChannels[i]);
				if(length > LengthD) {
					_leadChannel = BassChannels[i];
					LengthD = length;
				}
				
				_effects.Add(BassChannels[i], new Dictionary<EffectType, int>());
				_effects.Add(ReverbChannels[i], new Dictionary<EffectType, int>());
			}

			_isLoaded = true;

			return 0;
		}

		public void SetVolume(double newVolume) {
			if (!_isLoaded) {
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

			foreach (int channel in BassChannels) {
				Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume, newBassVol);
			}

			foreach(int channel in ReverbChannels) {
				if (_isReverbing) {
					Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume, newBassVol * 0.7);
				} else {
					Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume, 0);
				}
			}
		}

		public void SetReverb(bool reverb) {
			_isReverbing = reverb;
			if (reverb) {
				// Set reverb FX
				foreach (int channel in ReverbChannels) {
					// Reverb already applied
					if (_effects[channel].ContainsKey(REVERB_TYPE))
						return;

					int lowEqHandle = BassHelpers.AddEqToChannel(channel, BassHelpers.LowEqParams);
					int midEqHandle = BassHelpers.AddEqToChannel(channel, BassHelpers.MidEqParams);
					int highEqHandle = BassHelpers.AddEqToChannel(channel, BassHelpers.HighEqParams);
					int reverbHandle = BassHelpers.AddReverbToChannel(channel);
					
					double volumeSetting = _manager.GetVolumeSetting(Stem);
					Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume,volumeSetting * Volume * 0.7);
					
					_effects[channel].Add(REVERB_TYPE, reverbHandle);
					
					// Add low-high
					_effects[channel].Add(EffectType.PeakEQ, lowEqHandle);
					_effects[channel].Add(EffectType.PeakEQ + 1, midEqHandle);
					_effects[channel].Add(EffectType.PeakEQ + 2, highEqHandle);
				}
			} else {
				foreach (int channel in ReverbChannels) {
					// No reverb is applied
					if (!_effects[channel].ContainsKey(REVERB_TYPE)) {
						return;
					}
					
					// Remove low-high
					Bass.ChannelRemoveFX(channel, _effects[channel][EffectType.PeakEQ]);
					Bass.ChannelRemoveFX(channel, _effects[channel][EffectType.PeakEQ + 1]);
					Bass.ChannelRemoveFX(channel, _effects[channel][EffectType.PeakEQ + 2]);
					Bass.ChannelRemoveFX(channel, _effects[channel][REVERB_TYPE]);

					Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume, 0);

					_effects[channel].Remove(REVERB_TYPE);
					
					// Remove low-high
					_effects[channel].Remove(EffectType.PeakEQ);
					_effects[channel].Remove(EffectType.PeakEQ + 1);
					_effects[channel].Remove(EffectType.PeakEQ + 2);
				}
			}
		}

		public double GetPosition() {
			return Bass.ChannelBytes2Seconds(_leadChannel, Bass.ChannelGetPosition(_leadChannel));
		}

		public double GetLengthInSeconds() {
			return BassHelpers.GetChannelLengthInSeconds(_leadChannel);
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
				if (_isLoaded) {
					for (var i = 0; i < BassChannels.Length; i++) {
						if (!Bass.StreamFree(BassChannels[i])) {
							Debug.LogError($"Failed to free Split MoggFX channel: {i} - {Bass.LastError}. THIS WILL LEAK MEMORY!");
						}
						
						BassChannels[i] = 0;
					}
					for (var i = 0; i < ReverbChannels.Length; i++) {
						if (!Bass.StreamFree(ReverbChannels[i])) {
							Debug.LogError($"Failed to free Split Reverb MoggFX channel: {i} - {Bass.LastError}. THIS WILL LEAK MEMORY!");
						}
						
						ReverbChannels[i] = 0;
					}
				}

				_disposed = true;
			}
		}
	}	
}

