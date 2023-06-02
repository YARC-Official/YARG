using System;
using ManagedBass;
using ManagedBass.Mix;
using UnityEngine;

namespace YARG.Audio.BASS {
	public class BassMoggStemMixer : BassStemMixer {

		private int _moggSourceHandle;

		public BassMoggStemMixer(IAudioManager manager, int moggStreamHandle) : base(manager) {
			_moggSourceHandle = moggStreamHandle;
		}

		public override int AddChannel(IStemChannel channel) {
			if (channel is not BassMoggStemChannel moggStem) {
				throw new ArgumentException("Channel must be of type BassMoggStemChannel");
			}

			if (_channels.ContainsKey(channel.Stem)) {
				return 0;
			}

			var matrixes = moggStem.Matrixes;
			for (var i = 0; i < moggStem.Channels.Count; i++) {
				var moggChannel = (BassStemChannel) moggStem.Channels[i];
				if (!BassMix.MixerAddChannel(_mixerHandle, moggChannel.StreamHandle, BassFlags.MixerChanMatrix)) {
					return (int) Bass.LastError;
				}
				
				if (!BassMix.MixerAddChannel(_mixerHandle, moggChannel.ReverbStreamHandle, BassFlags.MixerChanMatrix)) {
					return (int) Bass.LastError;
				}

				moggChannel.IsMixed = true;

				float[,] channelPanVol = {
					{ matrixes[i][0] },
					{ matrixes[i][1] }
				};

				if (!BassMix.ChannelSetMatrix(moggChannel.StreamHandle, channelPanVol)) {
					return (int) Bass.LastError;
				}
				
				if (!BassMix.ChannelSetMatrix(moggChannel.ReverbStreamHandle, channelPanVol)) {
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

		protected override void ReleaseUnmanagedResources() {
            base.ReleaseUnmanagedResources();
			if (_moggSourceHandle != 0) {
				if (!Bass.StreamFree(_moggSourceHandle)) {
					Debug.LogError("Failed to free mixer stream. THIS WILL LEAK MEMORY!");
				}
				
				_moggSourceHandle = 0;
			}
		}
    }
}