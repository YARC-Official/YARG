using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using ManagedBass;
using ManagedBass.Mix;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Core.IO;

namespace YARG.Audio.BASS
{
    public class BassStemMixer : IStemMixer<BassAudioManager, BassStemChannel>
    {
        public static bool CreateMixerHandle(out int mixerHandle)
        {
            mixerHandle = BassMix.CreateMixerStream(44100, 2, BassFlags.Default);
            if (mixerHandle == 0)
            {
                Debug.LogError($"Failed to create mixer: {Bass.LastError}");
                return false;
            }

            // Mixer processing threads (for some reason this attribute is undocumented in ManagedBass?)
            if (!Bass.ChannelSetAttribute(mixerHandle, (ChannelAttribute) 86017, 2))
            {
                Debug.LogError($"Failed to set mixer processing threads: {Bass.LastError}");
                Bass.StreamFree(mixerHandle);
                return false;
            }
            return true;
        }

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

        private readonly List<BassStemChannel> _channels;

        private int _mixerHandle;
        private int _sourceStream;
        private bool _disposed;

        public BassStemMixer(int mixerHandle, int sourceStream)
        {
            _mixerHandle = mixerHandle;
            _sourceStream = sourceStream;
            _channels = new List<BassStemChannel>();

            IsPlaying = false;
        }

        ~BassStemMixer()
        {
            Dispose(false);
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

            // UNCOMMENT THE BELOW IF PROBLEMS ARE REPORTED WITH SYNC

            bool playing = IsPlaying;
            if (playing)
            {
                // Pause when seeking to avoid desyncing individual stems
                Pause();
            }

            if (_sourceStream != 0)
            {
                BassMix.SplitStreamReset(_sourceStream);
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

        public int AddChannel(BassStemChannel channel, int[]? indices, float[]? panning)
        {
            if (_channels.Any(ch => ch.Stem == channel.Stem))
            {
                return 0;
            }

            var flags = indices != null ? BassFlags.MixerChanMatrix | BassFlags.MixerChanDownMix : BassFlags.Default;
            if (!BassMix.MixerAddChannel(_mixerHandle, channel.StreamHandle, flags) ||
                !BassMix.MixerAddChannel(_mixerHandle, channel.ReverbStreamHandle, flags))
            {
                return (int) Bass.LastError;
            }

            channel.IsMixed = true;

            if (indices != null)
            {
                // First array = left pan, second = right pan
                float[,] volumeMatrix = new float[2, indices.Length];

                const int LEFT_PAN = 0;
                const int RIGHT_PAN = 1;
                for (int i = 0; i < indices.Length; ++i)
                {
                    volumeMatrix[LEFT_PAN, i] = panning![2 * i];
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
            }

            _channels.Add(channel);

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

        private void ReleaseManagedResources()
        {
            // Free managed resources here
            foreach (var channel in Channels)
            {
                channel.Dispose();
            }

            _channels.Clear();
        }

        private void ReleaseUnmanagedResources()
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

            if (_sourceStream != 0)
            {
                if (!Bass.StreamFree(_sourceStream))
                {
                    Debug.LogError($"Failed to free mixer source stream (THIS WILL LEAK MEMORY!): {Bass.LastError}");
                }

                _sourceStream = 0;
            }
        }
    }
}