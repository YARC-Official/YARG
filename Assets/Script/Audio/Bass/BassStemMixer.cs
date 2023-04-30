using System;
using System.Collections.Generic;
using System.Linq;
using ManagedBass;
using ManagedBass.Mix;
using UnityEngine;
using YARG.Serialization;

namespace YARG {
	public class BassStemMixer : IStemMixer {

		public int StemsLoaded { get; private set; }
		
		public bool IsPlaying { get; private set; }
		
		public IReadOnlyDictionary<SongStem, IStemChannel> Channels => _channels;

		public IStemChannel LeadChannel { get; private set; }

		private readonly IAudioManager _manager;
		private readonly Dictionary<SongStem, IStemChannel> _channels;
		private readonly XboxMoggData _moggData;
		private readonly bool _isMogg;

		private int _mixerHandle;
		
		private int _moggSourceHandle;

		private bool _disposed;

		public BassStemMixer(IAudioManager manager) {
			_manager = manager;
			_channels = new Dictionary<SongStem, IStemChannel>();
			
			StemsLoaded = 0;
			IsPlaying = false;
		}

		public BassStemMixer(IAudioManager manager, int moggHandle, XboxMoggData moggData) : this(manager) {
			_isMogg = true;
			_moggSourceHandle = moggHandle;
			_moggData = moggData;
		}

		~BassStemMixer() {
			Dispose(false);
		}

		public bool Create() {
			if (_mixerHandle != 0) {
				return true;
			}

			int mixer = BassMix.CreateMixerStream(44100, 2, BassFlags.Default);
			if (mixer == 0) {
				return false;
			}

			// Mixer processing threads (for some reason this attribute is undocumented in ManagedBass?)
			Bass.ChannelSetAttribute(mixer, (ChannelAttribute) 86017, 2);

			_mixerHandle = mixer;
			
			return true;
		}

		public bool SetupMogg(bool isSpeedUp) {
			if (!_isMogg) {
				return false;
			}
			
			int[] splitStreams = SplitMoggIntoChannels();

			// There was an array splitting if its an empty array
			if (splitStreams.Length == 0) {
				return false;
			}
			
			foreach((var stem, int[] channelIndexes) in _moggData.stemMaps) {
				// For every channel index in this stem, add it to the list of channels
				int[] channelStreams = channelIndexes.Select(i => splitStreams[i]).ToArray();
				var channel = new BassMoggStem(_manager, stem, channelStreams);
				if (channel.Load(isSpeedUp, PlayMode.Play.speed) < 0) {
					return false;
				}

				var matrixes = new List<float[]>();
				foreach (var channelIndex in channelIndexes) {
					var matrix = new float[2];
					matrix[0] = _moggData.matrixRatios[channelIndex, 0];
					matrix[1] = _moggData.matrixRatios[channelIndex, 1];
					matrixes.Add(matrix);
				}

				int code = AddMoggChannel(channel, matrixes);
				if (code != 0) {
					return false;
				}
			}

			return true;
		}

		public int Play(bool restart = false) {
			if (IsPlaying) {
				return 0;
			}

			if (!Bass.ChannelPlay(_mixerHandle, restart)) {
				return (int) Bass.LastError;
			}

			IsPlaying = true;

			return 0;
		}

		public int Pause() {
			if (!IsPlaying) {
				return 0;
			}

			if (!Bass.ChannelPause(_mixerHandle)) {
				return (int) Bass.LastError;
			}

			IsPlaying = false;

			return 0;
		}

		public double GetPosition() {
			// No channel in this case
			if (LeadChannel is null) {
				return -1;
			}
			return LeadChannel.GetPosition();
		}

		public int AddChannel(IStemChannel channel) {
			if (channel is not BassStemChannel bassChannel) {
				throw new ArgumentException("Channel must be of type BassStemChannel");
			}

			if (_channels.ContainsKey(channel.Stem)) {
				return 0;
			}

			if (!BassMix.MixerAddChannel(_mixerHandle, bassChannel.StreamHandle, BassFlags.Default)) {
				return (int) Bass.LastError;
			}
			
			if (!BassMix.MixerAddChannel(_mixerHandle, bassChannel.ReverbStreamHandle, BassFlags.Default)) {
				return (int) Bass.LastError;
			}

			_channels.Add(channel.Stem, channel);
			StemsLoaded++;

			if (channel.LengthD > LeadChannel?.LengthD || LeadChannel is null) {
				LeadChannel = channel;
			}

			return 0;
		}
		
		public int AddMoggChannel(IStemChannel channel, IList<float[]> matrixes) {
			if (channel is not BassMoggStem moggStem) {
				throw new ArgumentException("Channel must be of type BassMoggStem");
			}

			if (_channels.ContainsKey(channel.Stem)) {
				return 0;
			}

			for (var i = 0; i < moggStem.BassChannels.Length; i++) {
				int bassChannel = moggStem.BassChannels[i];
				int reverbChannel = moggStem.ReverbChannels[i];

				if (!BassMix.MixerAddChannel(_mixerHandle, bassChannel, BassFlags.MixerChanMatrix)) {
					return (int) Bass.LastError;
				}
				
				if (!BassMix.MixerAddChannel(_mixerHandle, reverbChannel, BassFlags.MixerChanMatrix)) {
					return (int) Bass.LastError;
				}

				float[,] channelPanVol = {
					{ matrixes[i][0] },
					{ matrixes[i][1] }
				};

				if (!BassMix.ChannelSetMatrix(bassChannel, channelPanVol)) {
					return (int) Bass.LastError;
				}
				
				if (!BassMix.ChannelSetMatrix(reverbChannel, channelPanVol)) {
					return (int) Bass.LastError;
				}
			}

			_channels.Add(channel.Stem, channel);
			StemsLoaded++;

			if (channel.LengthD > LeadChannel?.LengthD || LeadChannel is null) {
				LeadChannel = channel;
			}

			return 0;
		}

		public bool RemoveChannel(IStemChannel channel) {
			if (channel is not BassStemChannel bassChannel) {
				throw new ArgumentException("Channel must be of type BassStemChannel");
			}

			if (!_channels.ContainsKey(channel.Stem)) {
				return false;
			}

			if (!BassMix.MixerRemoveChannel(bassChannel.StreamHandle)) {
				return false;
			}

			_channels.Remove(channel.Stem);
			StemsLoaded--;

			if (channel == LeadChannel) {
				// Update lead channel
				foreach (var c in _channels.Values) {
					if (c.LengthD > LeadChannel?.LengthD) {
						LeadChannel = c;
					}
				}
			}

			return true;
		}

		public IStemChannel GetChannel(SongStem stem) {
			return !_channels.ContainsKey(stem) ? null : _channels[stem];
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {
			if (!_disposed) {
				// Free managed resources here
				if (disposing) {
					foreach (var channel in _channels.Values) {
						channel.Dispose();
					}

					_channels.Clear();
				}

				// Free unmanaged resources here
				if (_mixerHandle != 0) {
					if (!Bass.StreamFree(_mixerHandle)) {
						Debug.LogError("Failed to free mixer stream. THIS WILL LEAK MEMORY!");
					}
					
					_mixerHandle = 0;
				}

				if (_moggSourceHandle != 0) {
					if (!Bass.StreamFree(_moggSourceHandle)) {
						Debug.LogError("Failed to free Mogg source stream. THIS WILL LEAK MEMORY!");
					}

					_moggSourceHandle = 0;
				}

				_disposed = true;
			}
		}

		private int[] SplitMoggIntoChannels() {
			var channels = new int[_moggData.ChannelCount];

			var channelMap = new int[2];
			channelMap[1] = -1;
			
			for (var i = 0; i < channels.Length; i++) {
				channelMap[0] = i;
				
				int splitHandle = BassMix.CreateSplitStream(_moggSourceHandle, BassFlags.Decode | BassFlags.SplitPosition, channelMap);
				if (splitHandle == 0) {
					return Array.Empty<int>();
				}

				channels[i] = splitHandle;
			}

			return channels;
		}
	}

}