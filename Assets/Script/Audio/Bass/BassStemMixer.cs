using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using ManagedBass;
using ManagedBass.Mix;
using UnityEngine;
using YARG.Core.Audio;

namespace YARG.Audio.BASS
{
    public class BassStemMixer : IStemMixer<BassAudioManager, BassStemChannel>
    {
        public int StemsLoaded { get; protected set; }

        public bool IsPlaying { get; protected set; }

        public IReadOnlyList<BassStemChannel> Channels => _channels;

        public BassStemChannel LeadChannel { get; protected set; }

        public event Action SongEnd
        {
            add
            {
                if (LeadChannel is null)
                {
                    throw new InvalidOperationException("No song is currently loaded!");
                }

                LeadChannel.ChannelEnd += value;
            }
            remove
            {
                if (LeadChannel is not null)
                {
                    LeadChannel.ChannelEnd -= value;
                }
            }
        }

        protected readonly List<BassStemChannel> _channels;

        protected int _mixerHandle;
        private bool _disposed;

        public BassStemMixer()
        {
            _channels = new List<BassStemChannel>();

            StemsLoaded = 0;
            IsPlaying = false;
        }

        ~BassStemMixer()
        {
            Dispose(false);
        }

        public bool Create()
        {
            if (_mixerHandle != 0)
            {
                return true;
            }

            int mixer = BassMix.CreateMixerStream(44100, 2, BassFlags.Default);
            if (mixer == 0)
            {
                return false;
            }

            // Mixer processing threads (for some reason this attribute is undocumented in ManagedBass?)
            if (!Bass.ChannelSetAttribute(mixer, (ChannelAttribute) 86017, 2))
            {
                Debug.LogError($"Failed to set mixer processing threads: {Bass.LastError}");
                Bass.StreamFree(mixer);
                return false;
            }

            _mixerHandle = mixer;

            return true;
        }

        public int Play(bool restart = false)
        {
            if (IsPlaying)
            {
                return 0;
            }

            if (!Bass.ChannelPlay(_mixerHandle, restart))
            {
                return (int) Bass.LastError;
            }

            IsPlaying = true;

            return 0;
        }

        public void FadeIn(float maxVolume)
        {
            foreach (var channel in Channels)
            {
                channel.FadeIn(maxVolume);
            }
        }

        public UniTask FadeOut(CancellationToken token = default)
        {
            List<BassStemChannel> stemChannels = new();
            foreach (var stem in Channels)
                stemChannels.Add(stem);

            var fadeOuts = stemChannels.Select((channel) => channel.FadeOut()).ToArray();
            return UniTask.WhenAll(fadeOuts).AttachExternalCancellation(token);
        }

        public int Pause()
        {
            if (!IsPlaying)
            {
                return 0;
            }

            if (!Bass.ChannelPause(_mixerHandle))
            {
                return (int) Bass.LastError;
            }

            IsPlaying = false;

            return 0;
        }

        public double GetPosition(BassAudioManager manager, bool bufferCompensation = true)
        {
            // No channel in this case
            if (LeadChannel is null)
            {
                return -1;
            }

            return LeadChannel.GetPosition(manager, bufferCompensation);
        }

        public void SetPosition(BassAudioManager manager, double position, bool bufferCompensation = true)
        {
            if (LeadChannel is null)
            {
                return;
            }

            bool playing = IsPlaying;
            if (playing)
            {
                // Pause when seeking to avoid desyncing individual stems
                Pause();
            }

            foreach (var channel in Channels)
            {
                channel.SetPosition(manager, position, bufferCompensation);
            }

            if (playing)
            {
                // Account for buffer when resuming
                if (!Bass.ChannelUpdate(_mixerHandle, BassHelpers.PLAYBACK_BUFFER_LENGTH))
                    Debug.LogError($"Failed to set update channel: {Bass.LastError}");
                Play();
            }
        }

        public int GetData(float[] buffer)
        {
            int data = Bass.ChannelGetData(_mixerHandle, buffer, (int) (DataFlags.FFT256));
            if (data < 0)
            {
                return (int) Bass.LastError;
            }

            return data;
        }

        public void SetPlayVolume(BassAudioManager manager, bool fadeIn)
        {
            foreach (var channel in Channels)
            {
                channel.SetVolume(manager, fadeIn ? 0 : channel.Volume);
            }
        }

        public void SetSpeed(float speed)
        {
            foreach (var channel in Channels)
            {
                channel.SetSpeed(speed);
            }
        }

        public int AddChannel(BassStemChannel channel)
        {
            if (_channels.Any(ch => ch.Stem == channel.Stem))
            {
                return 0;
            }

            if (!BassMix.MixerAddChannel(_mixerHandle, channel.StreamHandle, BassFlags.Default))
            {
                return (int) Bass.LastError;
            }

            if (!BassMix.MixerAddChannel(_mixerHandle, channel.ReverbStreamHandle, BassFlags.Default))
            {
                return (int) Bass.LastError;
            }

            channel.IsMixed = true;

            _channels.Add(channel);
            StemsLoaded++;

            if (channel.LengthD > LeadChannel?.LengthD || LeadChannel is null)
            {
                LeadChannel = channel;
            }

            return 0;
        }

        public int AddChannel(BassStemChannel channel, int[] indices, float[] panning)
        {
            if (_channels.Any(ch => ch.Stem == channel.Stem))
            {
                return 0;
            }

            if (!BassMix.MixerAddChannel(_mixerHandle, channel.StreamHandle, BassFlags.MixerChanMatrix | BassFlags.MixerChanDownMix) ||
                !BassMix.MixerAddChannel(_mixerHandle, channel.ReverbStreamHandle, BassFlags.MixerChanMatrix | BassFlags.MixerChanDownMix))
            {
                return (int) Bass.LastError;
            }

            channel.IsMixed = true;

            // First array = left pan, second = right pan
            float[,] volumeMatrix = new float[2, indices.Length];

            const int LEFT_PAN = 0;
            const int RIGHT_PAN = 1;
            for (int i = 0; i < indices.Length; ++i)
            {
                volumeMatrix[LEFT_PAN, i] = panning[2 * i];
            }

            for (int i = 0; i < indices.Length; ++i)
            {
                volumeMatrix[RIGHT_PAN, i] = panning[2 * i + 1];
            }

            if (!BassMix.ChannelSetMatrix(channel.StreamHandle, volumeMatrix) ||
                !BassMix.ChannelSetMatrix(channel.ReverbStreamHandle, volumeMatrix))
            {
                return (int) Bass.LastError;
            }

            _channels.Add(channel);

            StemsLoaded++;

            if (LeadChannel == null || channel.LengthD > LeadChannel.LengthD)
            {
                LeadChannel = channel;
            }
            return 0;
        }

        public bool RemoveChannel(SongStem stemToRemove)
        {
            var index = _channels.FindIndex(ch => ch.Stem == stemToRemove);
            if (index == -1)
            {
                return false;
            }

            var channelToRemove = _channels[index];
            if (!BassMix.MixerRemoveChannel(channelToRemove.StreamHandle))
            {
                return false;
            }

            channelToRemove.IsMixed = false;

            _channels.RemoveAt(index);
            StemsLoaded--;

            if (channelToRemove == LeadChannel)
            {
                LeadChannel = null;

                // Update lead channel
                foreach (var channel in _channels)
                {
                    if (channel.LengthD > LeadChannel.LengthD)
                    {
                        LeadChannel = channel;
                    }
                }
            }

            return true;
        }

        public BassStemChannel[] GetChannels(SongStem stem)
        {
            return _channels.ToArray();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    ReleaseManagedResources();
                }

                ReleaseUnmanagedResources();
                _disposed = true;
            }
        }

        protected virtual void ReleaseManagedResources()
        {
            // Free managed resources here
            foreach (var channel in Channels)
            {
                channel.Dispose();
            }

            _channels.Clear();
        }

        protected virtual void ReleaseUnmanagedResources()
        {
            // Free unmanaged resources here
            if (_mixerHandle != 0)
            {
                if (!Bass.StreamFree(_mixerHandle))
                {
                    Debug.LogError($"Failed to free mixer stream (THIS WILL LEAK MEMORY!): {Bass.LastError}");
                }

                _mixerHandle = 0;
            }
        }
    }
}