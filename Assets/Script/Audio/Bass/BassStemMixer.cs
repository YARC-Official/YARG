using System;
using System.Collections.Generic;
using ManagedBass;
using ManagedBass.Mix;
using UnityEngine;

namespace YARG {
	public class BassStemMixer : IStemMixer {

		public int StemsLoaded { get; private set; }

		public bool IsPlaying { get; private set; }

		public IStemChannel LeadChannel { get; private set; }

		private readonly Dictionary<SongStem, IStemChannel> _channels;

		private int _mixerHandle;

		private bool _disposed;

		public BassStemMixer() {
			_channels = new Dictionary<SongStem, IStemChannel>();
			StemsLoaded = 0;
			IsPlaying = false;
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
					Bass.StreamFree(_mixerHandle);
				}

				_disposed = true;
			}
		}
	}

}