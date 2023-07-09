using System;
using ManagedBass;
using ManagedBass.Mix;
using UnityEngine;

namespace YARG.Audio.BASS
{
    public class BassMoggStemMixer : BassStemMixer
    {
        private int _moggSourceHandle;

        public BassMoggStemMixer(IAudioManager manager, int moggStreamHandle) : base(manager)
        {
            _moggSourceHandle = moggStreamHandle;
        }

        public override int AddChannel(IStemChannel channel)
        {
            if (channel is not BassMoggStemChannel moggChannel)
            {
                throw new ArgumentException("Channel must be of type BassMoggStemChannel");
            }


            if (!BassMix.MixerAddChannel(_mixerHandle, moggChannel.StreamHandle, BassFlags.MixerChanMatrix | BassFlags.MixerChanDownMix) ||
                !BassMix.MixerAddChannel(_mixerHandle, moggChannel.ReverbStreamHandle, BassFlags.MixerChanMatrix | BassFlags.MixerChanDownMix))
            {
                return (int) Bass.LastError;
            }

            moggChannel.IsMixed = true;

            float[,] channelPanVol =
            {
                {
                    moggChannel.left
                },
                {
                    moggChannel.right
                }
            };

            if (!BassMix.ChannelSetMatrix(moggChannel.StreamHandle, channelPanVol) ||
                !BassMix.ChannelSetMatrix(moggChannel.ReverbStreamHandle, channelPanVol))
            {
                return (int) Bass.LastError;
            }

            if (_channels.TryGetValue(channel.Stem, out var list))
                list.Add(channel);
            else
                _channels.Add(channel.Stem, new() { channel });

            StemsLoaded++;

            if (channel.LengthD > LeadChannel?.LengthD || LeadChannel is null)
            {
                LeadChannel = channel;
            }

            return 0;
        }

        protected override void ReleaseUnmanagedResources()
        {
            base.ReleaseUnmanagedResources();
            if (_moggSourceHandle != 0)
            {
                if (!Bass.StreamFree(_moggSourceHandle))
                {
                    Debug.LogError("Failed to free mixer stream. THIS WILL LEAK MEMORY!");
                }

                _moggSourceHandle = 0;
            }
        }
    }
}