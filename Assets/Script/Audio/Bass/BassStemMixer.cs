using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ManagedBass;
using ManagedBass.Mix;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Integration.StageKit;

namespace YARG.Audio.BASS
{
    public sealed class BassStemMixer : StemMixer
    {
        private int _mixerHandle;
        private int _sourceStream;

        private StreamHandle _mainHandle;
        private int _songEndHandle;

        public override event Action SongEnd
        {
            add
            {
                if (_songEndHandle == 0)
                {
                    void sync(int _, int __, int ___, IntPtr _____)
                    {
                        // Prevent potential race conditions by caching the value as a local
                        var end = _songEnd;
                        if (end != null)
                        {
                            UnityMainThreadCallback.QueueEvent(end.Invoke);
                        }
                    }
                    _songEndHandle = BassMix.ChannelSetSync(_mainHandle.Stream, SyncFlags.End, 0, sync);
                }

                _songEnd += value;
            }
            remove
            {
                _songEnd -= value;
            }
        }

        internal BassStemMixer(BassAudioManager manager, float speed, int handle, int sourceStream)
            : base(manager, speed)
        {
            _mixerHandle = handle;
            _sourceStream = sourceStream;
        }

        public override int Play(bool restart = false)
        {
            lock (this)
            {
                if (_mixerHandle == 0)
                {
                    return -1;
                }

                if (IsPlaying)
                {
                    return 0;
                }

                if (!Bass.ChannelPlay(_mixerHandle, restart))
                {
                    return (int) Bass.LastError;
                }

                _isPlaying = true;

                return 0;
            }
        }

        public override void FadeIn(float maxVolume)
        {
            lock (this)
            {
                if (_mixerHandle == 0)
                {
                    return;
                }
                Bass.ChannelSlideAttribute(_mixerHandle, ChannelAttribute.Volume, maxVolume, BassHelpers.FADE_TIME_MILLISECONDS);
            }
        }

        public override void FadeOut()
        {
            lock (this)
            {
                if (_mixerHandle == 0)
                {
                    return;
                }
                Bass.ChannelSlideAttribute(_mixerHandle, ChannelAttribute.Volume, 0, BassHelpers.FADE_TIME_MILLISECONDS);
            }
        }

        public override int Pause()
        {
            lock (this)
            {
                if (_mixerHandle == 0)
                {
                    return -1;
                }
                if (!IsPlaying)
                {
                    return 0;
                }

                if (!Bass.ChannelPause(_mixerHandle))
                {
                    return (int) Bass.LastError;
                }

                _isPlaying = false;

                return 0;
            }
        }

        public override double GetPosition(bool bufferCompensation = true)
        {
            lock (this)
            {
                if (_mixerHandle == 0)
                {
                    return -1;
                }

                // BassMix.ChannelGetPosition is very wonky when seeking
                // compared to Bass.ChannelGetPosition
                // We'll just have to make do with the less granular time reporting
                // long position = IsMixed
                //     ? BassMix.ChannelGetPosition(_streamHandles.Stream)
                //     : Bass.ChannelGetPosition(_streamHandles.Stream);
                long position = Bass.ChannelGetPosition(_mainHandle.Stream);
                if (position < 0)
                {
                    YargLogger.LogFormatError("Failed to get channel position in bytes: {0}!", Bass.LastError);
                    return -1;
                }

                double seconds = Bass.ChannelBytes2Seconds(_mainHandle.Stream, position);
                if (seconds < 0)
                {
                    YargLogger.LogFormatError("Failed to get channel position in seconds: {0}!", Bass.LastError);
                    return -1;
                }

                seconds -= bufferCompensation ? _manager.PlaybackBufferLength : 0;

                return seconds;
            }
        }

        public override float GetVolume()
        {
            lock (this)
            {
                if (_mixerHandle == 0)
                {
                    return 0;
                }

                if (!Bass.ChannelGetAttribute(_mixerHandle, ChannelAttribute.Volume, out float volume))
                {
                    YargLogger.LogFormatError("Failed to get volume: {0}!", Bass.LastError);
                }
                return volume;
            }
        }

        public override void SetPosition(double position, bool bufferCompensation = true)
        {
            lock (this)
            {
                if (_mixerHandle == 0)
                {
                    return;
                }

                bool playing = IsPlaying;
                if (playing)
                {
                    // Pause when seeking to avoid desyncing individual stems
                    Pause();
                }

                if (_channels.Count == 0)
                {
                    long bytes = Bass.ChannelSeconds2Bytes(_mainHandle.Stream, position);
                    if (bytes < 0)
                    {
                        YargLogger.LogFormatError("Failed to get channel position in bytes: {0}!", Bass.LastError);
                    }
                    else if (!Bass.ChannelSetPosition(_mainHandle.Stream, bytes))
                    {
                        YargLogger.LogFormatError("Failed to set channel position: {0}!", Bass.LastError);
                    }
                    return;
                }

                if (_sourceStream != 0)
                {
                    BassMix.SplitStreamReset(_sourceStream);
                }

                foreach (var channel in _channels)
                {
                    channel.SetPosition(position, bufferCompensation);
                }

                if (playing)
                {
                    // Account for buffer when resuming
                    if (!Bass.ChannelUpdate(_mixerHandle, BassHelpers.PLAYBACK_BUFFER_LENGTH))
                        YargLogger.LogFormatError("Failed to set update channel: {0}!", Bass.LastError);
                    Play();
                }
            }
        }

        public override void SetVolume(double volume)
        {
            lock (this)
            {
                if (_mixerHandle == 0)
                {
                    return;
                }

                if (!Bass.ChannelSetAttribute(_mixerHandle, ChannelAttribute.Volume, volume))
                {
                    YargLogger.LogFormatError("Failed to set mixer volume: {0}!", Bass.LastError);
                }
            }
        }

        public override int GetData(float[] buffer)
        {
            lock (this)
            {
                if (_mixerHandle == 0)
                {
                    return -1;
                }

                int data = Bass.ChannelGetData(_mixerHandle, buffer, (int) (DataFlags.FFT256));
                if (data < 0)
                {
                    return (int) Bass.LastError;
                }

                return data;
            }
        }

        public override void SetSpeed(float speed)
        {
            lock (this)
            {
                if (_mixerHandle == 0)
                {
                    return;
                }

                foreach (var channel in _channels)
                {
                    channel.SetSpeed(speed);
                }
            }
        }

        public override bool AddChannel(SongStem stem)
        {
            _mainHandle = StreamHandle.Create(_sourceStream, 1f, null);
            if (_mainHandle == null)
            {
                YargLogger.LogFormatError("Failed to load stem split stream {stem}: {0}!", Bass.LastError);
            }

            if (!BassMix.MixerAddChannel(_mixerHandle, _mainHandle.Stream, BassFlags.Default))
            {
                YargLogger.LogFormatError("Failed to add channel {stem} to mixer: {0}!", Bass.LastError);
                return false;
            }
            _length = BassAudioManager.GetLengthInSeconds(_sourceStream);
            return true;
        }

        public override bool AddChannel(SongStem stem, Stream stream)
        {
            if (!BassAudioManager.CreateSourceStream(stream, out int sourceStream))
            {
                YargLogger.LogFormatError("Failed to load stem source stream {stem}: {0}!", Bass.LastError);
                return false;
            }

            double volume = AudioManager.GetVolumeSetting(stem);
            if (!BassAudioManager.CreateSplitStreams(sourceStream, volume, null, out var streamHandles, out var reverbHandles))
            {
                YargLogger.LogFormatError("Failed to load stem split streams {stem}: {0}!", Bass.LastError);
                return false;
            }

            if (!BassMix.MixerAddChannel(_mixerHandle, streamHandles.Stream, BassFlags.Default) ||
                !BassMix.MixerAddChannel(_mixerHandle, reverbHandles.Stream, BassFlags.Default))
            {
                YargLogger.LogFormatError("Failed to add channel {stem} to mixer: {0}!", Bass.LastError);
                return false;
            }

            double length = BassAudioManager.GetLengthInSeconds(streamHandles.Stream);
            var pitchparams = BassAudioManager.SetPitchParams(stem, _speed, streamHandles, reverbHandles);
            var stemchannel = new BassStemChannel(_manager, stem, sourceStream, volume, pitchparams, streamHandles, reverbHandles);

            if (_mainHandle == null || length > _length)
            {
                _mainHandle = streamHandles;
                _length = length;
            }

            _channels.Add(stemchannel);
            return true;
        }

        public override bool AddChannel(SongStem stem, int[] indices, float[] panning)
        {
            double volume = AudioManager.GetVolumeSetting(stem);
            if (!BassAudioManager.CreateSplitStreams(_sourceStream, volume, indices, out var streamHandles, out var reverbHandles))
            {
                YargLogger.LogFormatError("Failed to load stem {stem}: {0}!", Bass.LastError);
                return false;
            }

            if (!BassMix.MixerAddChannel(_mixerHandle, streamHandles.Stream, BassFlags.MixerChanMatrix | BassFlags.MixerChanDownMix) ||
                !BassMix.MixerAddChannel(_mixerHandle, reverbHandles.Stream, BassFlags.MixerChanMatrix | BassFlags.MixerChanDownMix))
            {
                YargLogger.LogFormatError("Failed to add channel {stem} to mixer: {0}!", Bass.LastError);
                return false;
            }

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

            if (!BassMix.ChannelSetMatrix(streamHandles.Stream, volumeMatrix) ||
                !BassMix.ChannelSetMatrix(reverbHandles.Stream, volumeMatrix))
            {
                YargLogger.LogFormatError("Failed to set {stem} matrices: {0}!", Bass.LastError);
                return false;
            }

            double length = BassAudioManager.GetLengthInSeconds(streamHandles.Stream);
            var pitchparams = BassAudioManager.SetPitchParams(stem, _speed, streamHandles, reverbHandles);
            var stemchannel = new BassStemChannel(_manager, stem, 0, volume, pitchparams, streamHandles, reverbHandles);

            if (_mainHandle == null || length > _length)
            {
                _mainHandle = streamHandles;
                _length = length;
            }

            _channels.Add(stemchannel);
            return true;
        }

        public override bool RemoveChannel(SongStem stemToRemove)
        {
            throw new NotImplementedException();
        }

        protected override void DisposeManagedResources()
        {
            // Free managed resources here
            foreach (var channel in Channels)
            {
                channel.Dispose();
            }

            _channels.Clear();
        }

        protected override void DisposeUnmanagedResources()
        {
            lock (this)
            {
                // Free unmanaged resources here
                if (_mixerHandle != 0)
                {
                    if (!Bass.StreamFree(_mixerHandle))
                    {
                        YargLogger.LogFormatError("Failed to free mixer stream (THIS WILL LEAK MEMORY!): {0}!", Bass.LastError);
                    }
                    _mixerHandle = 0;
                }

                if (_sourceStream != 0)
                {
                    if (!Bass.StreamFree(_sourceStream))
                    {
                        YargLogger.LogFormatError("Failed to free mixer source stream (THIS WILL LEAK MEMORY!): {0}!", Bass.LastError);
                    }
                    _sourceStream = 0;
                }
            }
        }
    }
}