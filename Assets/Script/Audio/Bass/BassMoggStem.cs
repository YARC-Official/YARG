using System;
using System.Collections.Generic;
using ManagedBass;
using ManagedBass.DirectX8;
using ManagedBass.Fx;
using UnityEngine;

namespace YARG {
	public class BassMoggStem : IStemChannel {
		
		private const EffectType REVERB_TYPE = EffectType.DXReverb;
		
		public SongStem Stem { get; }
		public double LengthD { get; private set; }
		public double Volume { get; private set; }
		
		public int[] BassChannels { get; }
		public int[] ChannelIndexes { get; }
		
		private readonly IAudioManager _manager;

		private readonly int[] _splitStreams;

		private readonly Dictionary<int, Dictionary<EffectType, int>> _effects;
		private readonly Dictionary<int, Dictionary<DSPType, int>> _dspHandles;

		private readonly DSPProcedure _dspGain;

		private int _leadChannel;
		
		private double _lastStemVolume;

		private bool _isLoaded;
		private bool _disposed;

		public BassMoggStem(IAudioManager manager, SongStem stem, int[] splitStreams) {
			_manager = manager;
			Stem = stem;

			_splitStreams = splitStreams;
			BassChannels = new int[splitStreams.Length];
			ChannelIndexes = new int[splitStreams.Length];
			
			Volume = 1;
			
			_lastStemVolume = _manager.GetVolumeSetting(Stem);
			_effects = new Dictionary<int, Dictionary<EffectType, int>>();
			_dspHandles = new Dictionary<int, Dictionary<DSPType, int>>();

			_dspGain += BassStemChannel.GainDSP;
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
				BassChannels[i] = BassFx.TempoCreate(_splitStreams[i], flags);
				int bassChannel = BassChannels[i];
				
				Bass.ChannelSetAttribute(bassChannel, ChannelAttribute.Volume, _manager.GetVolumeSetting(Stem));

				if (isSpeedUp) {
					// Gets relative speed from 100% (so 1.05f = 5% increase)
					float relativeSpeed = Math.Abs(speed) * 100;
					relativeSpeed -= 100;
					Bass.ChannelSetAttribute(bassChannel, ChannelAttribute.Tempo, relativeSpeed);

					// Have to handle pitch separately for some reason
					if (_manager.IsChipmunkSpeedup) {
						// Calculates semitone increase, can probably be improved but this will do for now
						float semitones = relativeSpeed > 0 ? 1 * speed : -1 * speed;
						Bass.ChannelSetAttribute(bassChannel, ChannelAttribute.Pitch, semitones);
					}
				}

				// Get longest channel as part of this stem
				double length = GetChannelLengthInSeconds(BassChannels[i]);
				if(length > LengthD) {
					_leadChannel = bassChannel;
					LengthD = length;
				}
				
				_effects.Add(BassChannels[i], new Dictionary<EffectType, int>());
				_dspHandles.Add(BassChannels[i], new Dictionary<DSPType, int>());
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
		}

		public void SetReverb(bool reverb) {
			if (reverb) {
				// Set reverb FX
				foreach (int channel in BassChannels) {
					// Reverb already applied
					if (_effects[channel].ContainsKey(REVERB_TYPE))
						return;
					
					int reverbHandle = AddReverbToChannel(channel);
					int gainDspHandle = Bass.ChannelSetDSP(channel, _dspGain);
					
					_effects[channel].Add(REVERB_TYPE, reverbHandle);
					_dspHandles[channel].Add(DSPType.Gain, gainDspHandle);
				}
			} else {
				foreach (int channel in BassChannels) {
					// No reverb is applied
					if (!_effects[channel].ContainsKey(REVERB_TYPE)) {
						return;
					}

					Bass.ChannelRemoveFX(channel, _effects[channel][REVERB_TYPE]);
					Bass.ChannelRemoveDSP(channel, _dspHandles[channel][DSPType.Gain]);

					_effects[channel].Remove(REVERB_TYPE);
					_dspHandles[channel].Remove(DSPType.Gain);
				}
			}
		}

		public double GetPosition() {
			return Bass.ChannelBytes2Seconds(_leadChannel, Bass.ChannelGetPosition(_leadChannel));
		}

		public double GetLengthInSeconds() {
			return GetChannelLengthInSeconds(_leadChannel);
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
							Debug.LogError($"Failed to free MoggFX channel: {i} - {Bass.LastError}. THIS WILL LEAK MEMORY!");
						}
						
						BassChannels[i] = 0;
					}
				}

				_disposed = true;
			}
		}
		
		private static int AddReverbToChannel(int channel) {
			// Set reverb FX
			int reverbHandle = Bass.ChannelSetFX(channel, REVERB_TYPE, 0);
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
		
		private static double GetChannelLengthInSeconds(int channel) {
			long length = Bass.ChannelGetLength(channel);

			if (length == -1) {
				return (double) Bass.LastError;
			}

			double seconds = Bass.ChannelBytes2Seconds(channel, length);

			if (seconds < 0) {
				return (double) Bass.LastError;
			}

			return seconds;
		}
	}	
}

