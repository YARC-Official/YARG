using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using ManagedBass;
using ManagedBass.Mix;
using UnityEngine;

namespace YARG.Audio.BASS
{
    public class BassStemMixer : IStemMixer
    {
        public int StemsLoaded { get; protected set; }

        public bool IsPlaying { get; protected set; }

        public IReadOnlyDictionary<SongStem, List<IStemChannel>> Channels => _channels;

        public IStemChannel LeadChannel { get; protected set; }

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
                if (LeadChannel is null)
                {
                    throw new InvalidOperationException("No song is currently loaded!");
                }

                LeadChannel.ChannelEnd -= value;
            }
        }

        protected readonly IAudioManager _manager;
        protected readonly Dictionary<SongStem, List<IStemChannel>> _channels;

        protected int _mixerHandle;

        private bool _disposed;

        public BassStemMixer(IAudioManager manager)
        {
            _manager = manager;
            _channels = new Dictionary<SongStem, List<IStemChannel>>();

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
            Bass.ChannelSetAttribute(mixer, (ChannelAttribute) 86017, 2);

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
            foreach (var stem in Channels.Values)
                for (int i = 0; i < stem.Count; i++)
                    stem[i].FadeIn(maxVolume);
        }

        public UniTask FadeOut(CancellationToken token = default)
        {
            List<IStemChannel> stemChannels = new();
            foreach (var stem in Channels.Values)
                stemChannels.AddRange(stem);

            var fadeOuts = Enumerable.Select(stemChannels, channel => channel.FadeOut()).ToList();
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

        public double GetPosition(bool desyncCompensation = true)
        {
            // No channel in this case
            if (LeadChannel is null)
            {
                return -1;
            }

            return LeadChannel.GetPosition(desyncCompensation);
        }

        public void SetPosition(double position, bool desyncCompensation = true)
        {
            if (LeadChannel is null)
            {
                return;
            }

            foreach (var stem in Channels.Values)
                for (int i = 0; i < stem.Count; i++)
                    stem[i].SetPosition(position, desyncCompensation);
        }

        public void SetPlayVolume(bool fadeIn)
        {
            foreach (var channel in Channels.Values)
                for (int i = 0; i < channel.Count; i++)
                    channel[i].SetVolume(fadeIn ? 0 : channel[i].Volume);
        }

        public virtual int AddChannel(IStemChannel channel)
        {
            if (channel is not BassStemChannel bassChannel)
            {
                throw new ArgumentException("Channel must be of type BassStemChannel");
            }

            if (_channels.ContainsKey(channel.Stem))
            {
                return 0;
            }

            if (!BassMix.MixerAddChannel(_mixerHandle, bassChannel.StreamHandle, BassFlags.Default))
            {
                return (int) Bass.LastError;
            }

            if (!BassMix.MixerAddChannel(_mixerHandle, bassChannel.ReverbStreamHandle, BassFlags.Default))
            {
                return (int) Bass.LastError;
            }

            bassChannel.IsMixed = true;

            _channels.Add(channel.Stem, new() { channel });
            StemsLoaded++;

            if (channel.LengthD > LeadChannel?.LengthD || LeadChannel is null)
            {
                LeadChannel = channel;
            }

            return 0;
        }

        public bool RemoveChannel(IStemChannel channel)
        {
            if (channel is not BassStemChannel bassChannel)
            {
                throw new ArgumentException("Channel must be of type BassStemChannel");
            }

            if (!_channels.ContainsKey(channel.Stem))
            {
                return false;
            }

            if (!BassMix.MixerRemoveChannel(bassChannel.StreamHandle))
            {
                return false;
            }

            bassChannel.IsMixed = false;

            _channels.Remove(channel.Stem);
            StemsLoaded--;

            if (channel == LeadChannel)
            {
                // Update lead channel
                foreach (var stem in Channels.Values)
                {
                    for (int i = 0; i < stem.Count; i++)
                    {
                        if (LeadChannel is null || stem[i].LengthD > LeadChannel.LengthD)
                        {
                            LeadChannel = stem[i];
                        }
                    }
                }
            }

            return true;
        }

        public IStemChannel[] GetChannels(SongStem stem)
        {
            return !_channels.ContainsKey(stem) ? Array.Empty<IStemChannel>() : _channels[stem].ToArray();
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
            foreach (var stem in Channels.Values)
                for (int i = 0; i < stem.Count; i++)
                    stem[i].Dispose();

            _channels.Clear();
        }

        protected virtual void ReleaseUnmanagedResources()
        {
            // Free unmanaged resources here
            if (_mixerHandle != 0)
            {
                if (!Bass.StreamFree(_mixerHandle))
                {
                    Debug.LogError("Failed to free mixer stream. THIS WILL LEAK MEMORY!");
                }

                _mixerHandle = 0;
            }
        }
    }
}