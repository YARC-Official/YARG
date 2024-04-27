using System;
using System.IO;
using System.Threading.Tasks;
using ManagedBass;
using ManagedBass.Mix;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Core.Song;

namespace YARG.Audio.BASS
{
    public sealed class BassStemMixer : StemMixer
    {
        private readonly int _mixerHandle;
        private readonly int _sourceStream;
        private readonly int[] _loopHandles = new int[3];

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

        internal BassStemMixer(string name, BassAudioManager manager, float speed, double volume, int handle, int sourceStream, bool clampStemVolume)
            : base(name, manager, speed, clampStemVolume)
        {
            _mixerHandle = handle;
            _sourceStream = sourceStream;
            SetVolume_Internal(volume);
            _BufferSetter(Settings.SettingsManager.Settings.EnablePlaybackBuffer.Value, Bass.PlaybackBufferLength);
        }

        protected override int Play_Internal(bool restartBuffer)
        {
            if (!Bass.ChannelPlay(_mixerHandle, restartBuffer))
            {
                return (int) Bass.LastError;
            }

            if (Settings.SettingsManager.Settings.EnablePlaybackBuffer.Value)
            {
                if (!Bass.ChannelUpdate(_mixerHandle, Bass.PlaybackBufferLength))
                {
                    YargLogger.LogFormatError("Failed to fill playback buffer: {0}!", Bass.LastError);
                }
            }
            return 0;
        }

        protected override void FadeIn_Internal(double maxVolume, double duration)
        {
            float scaled = (float) BassAudioManager.ExponentialVolume(maxVolume);
            Bass.ChannelSlideAttribute(_mixerHandle, ChannelAttribute.Volume, scaled, (int) (duration * SongEntry.MILLISECOND_FACTOR));
        }

        protected override void FadeOut_Internal(double duration)
        {
            Bass.ChannelSlideAttribute(_mixerHandle, ChannelAttribute.Volume, 0, (int) (duration * SongEntry.MILLISECOND_FACTOR));
        }

        protected override int Pause_Internal()
        {
            if (!Bass.ChannelPause(_mixerHandle))
            {
                return (int) Bass.LastError;
            }
            return 0;
        }

        protected override double GetPosition_Internal()
        {
            long position = Bass.ChannelGetPosition(_mainHandle.Stream);
            if (position < 0)
            {
                YargLogger.LogFormatError("Failed to get channel position in bytes: {0}", Bass.LastError);
                return -1;
            }

            double seconds = Bass.ChannelBytes2Seconds(_mainHandle.Stream, position);
            if (seconds < 0)
            {
                YargLogger.LogFormatError("Failed to get channel position in seconds: {0}", Bass.LastError);
                return -1;
            }

            if (Settings.SettingsManager.Settings.EnablePlaybackBuffer.Value)
            {
                seconds -= Bass.PlaybackBufferLength / 1000.0f;
            }
            return seconds;
        }

        protected override double GetVolume_Internal()
        {
            if (!Bass.ChannelGetAttribute(_mixerHandle, ChannelAttribute.Volume, out float volume))
            {
                YargLogger.LogFormatError("Failed to get volume: {0}", Bass.LastError);
            }
            return BassAudioManager.LogarithmicVolume(volume);
        }

        protected override void SetPosition_Internal(double position)
        {
            bool playing = !IsPaused;
            if (playing)
            {
                // Pause when seeking to avoid desyncing individual stems
                Pause_Internal();
            }

            if (_channels.Count == 0)
            {
                long bytes = Bass.ChannelSeconds2Bytes(_mainHandle.Stream, position);
                if (bytes < 0)
                {
                    YargLogger.LogFormatError("Failed to get channel position in bytes: {0}!", Bass.LastError);
                }
                else if (!BassMix.ChannelSetPosition(_mainHandle.Stream, bytes, PositionFlags.Bytes | PositionFlags.MixerReset))
                {
                    YargLogger.LogFormatError("Failed to set channel position: {0}!", Bass.LastError);
                }
            }
            else
            {
                if (_sourceStream != 0)
                {
                    BassMix.SplitStreamReset(_sourceStream);
                }

                foreach (var channel in _channels)
                {
                    channel.SetPosition(position);
                }
            }

            if (playing)
            {
                Play_Internal(true);
            }
        }

        protected override void SetVolume_Internal(double volume)
        {
            volume = BassAudioManager.ExponentialVolume(volume);
            if (!Bass.ChannelSetAttribute(_mixerHandle, ChannelAttribute.Volume, volume))
            {
                YargLogger.LogFormatError("Failed to set mixer volume: {0}", Bass.LastError);
            }
        }

        protected override int GetData_Internal(float[] buffer)
        {
            int data = Bass.ChannelGetData(_mixerHandle, buffer, (int) (DataFlags.FFT256));
            if (data < 0)
            {
                return (int) Bass.LastError;
            }
            return data;
        }

        protected override void SetSpeed_Internal(float speed, bool shiftPitch)
        {
            speed = (float) Math.Clamp(speed, 0.05, 50);
            if (_speed == speed)
            {
                return;
            }

            _speed = speed;
            foreach (var channel in _channels)
            {
                channel.SetSpeed(speed, shiftPitch);
            }
        }

        protected override bool AddChannel_Internal(SongStem stem)
        {
            _mainHandle = StreamHandle.Create(_sourceStream, null);
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

        protected override bool AddChannel_Internal(SongStem stem, Stream stream)
        {
            if (!BassAudioManager.CreateSourceStream(stream, out int sourceStream))
            {
                YargLogger.LogFormatError("Failed to load stem source stream {stem}: {0}!", Bass.LastError);
                return false;
            }

            if (!BassAudioManager.CreateSplitStreams(sourceStream, null, out var streamHandles, out var reverbHandles))
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

            CreateChannel(stem, sourceStream, streamHandles, reverbHandles);
            return true;
        }

        protected override bool AddChannel_Internal(SongStem stem, int[] indices, float[] panning)
        {
            if (!BassAudioManager.CreateSplitStreams(_sourceStream, indices, out var streamHandles, out var reverbHandles))
            {
                YargLogger.LogFormatError("Failed to load stem {0}: {1}!", stem, Bass.LastError);
                return false;
            }

            if (!BassMix.MixerAddChannel(_mixerHandle, streamHandles.Stream, BassFlags.MixerChanMatrix) ||
                !BassMix.MixerAddChannel(_mixerHandle, reverbHandles.Stream, BassFlags.MixerChanMatrix))
            {
                YargLogger.LogFormatError("Failed to add channel {0} to mixer: {1}!", stem, Bass.LastError);
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

            CreateChannel(stem, 0, streamHandles, reverbHandles);
            return true;
        }

        protected override bool RemoveChannel_Internal(SongStem stemToRemove)
        {
            int index = _channels.FindIndex(channel => channel.Stem == stemToRemove);
            if (index == -1)
            {
                return false;
            }
            _channels[index].Dispose();
            _channels.RemoveAt(index);
            return true;
        }

        protected override bool SetLoop_Internal(double end, double fadeDuration, double volume, Action LoopFunc)
        {
            _loopHandles[0] = Bass.ChannelSetSync(_mainHandle.Stream, SyncFlags.Seeking, 0, (int _, int __, int ___, IntPtr ____) =>
            {
                FadeIn_Internal(volume, fadeDuration);
            });
            if (_loopHandles[0] == 0)
            {
                return false;
            }

            long fadeOutBytes = Bass.ChannelSeconds2Bytes(_mainHandle.Stream, end - fadeDuration);
            _loopHandles[1] = Bass.ChannelSetSync(_mainHandle.Stream, SyncFlags.Position, fadeOutBytes, async (int _, int __, int ___, IntPtr ____) =>
            {
                if (Settings.SettingsManager.Settings.EnablePlaybackBuffer.Value)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(Bass.PlaybackBufferLength));
                }
                FadeOut_Internal(fadeDuration);
            });
            if (_loopHandles[1] == 0)
            {
                Bass.ChannelRemoveSync(_mainHandle.Stream, _loopHandles[0]);
                return false;
            }

            long seekBytes = Bass.ChannelSeconds2Bytes(_mainHandle.Stream, end);
            _loopHandles[2] = Bass.ChannelSetSync(_mainHandle.Stream, SyncFlags.Position, seekBytes, async (int _, int __, int ___, IntPtr ____) =>
            {
                if (Settings.SettingsManager.Settings.EnablePlaybackBuffer.Value)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(Bass.PlaybackBufferLength));
                }
                LoopFunc();
            });
            if (_loopHandles[2] == 0)
            {
                Bass.ChannelRemoveSync(_mainHandle.Stream, _loopHandles[0]);
                Bass.ChannelRemoveSync(_mainHandle.Stream, _loopHandles[1]);
                return false;
            }
            return true;
        }

        protected override void EndLoop_Internal()
        {
            for (int i = 0; i < _loopHandles.Length; i++)
            {
                ref int handle = ref _loopHandles[i];
                if (handle != 0)
                {
                    Bass.ChannelRemoveSync(_mainHandle.Stream, handle);
                    handle = 0;
                }
            }
        }

        protected override void ToggleBuffer_Internal(bool enable)
        {
            _BufferSetter(enable, Bass.PlaybackBufferLength);
        }

        protected override void SetBufferLength_Internal(int length)
        {
            _BufferSetter(Settings.SettingsManager.Settings.EnablePlaybackBuffer.Value, length);
        }

        private void _BufferSetter(bool enable, int length)
        {
            if (!enable)
            {
                length = 0;
            }

            if (!Bass.ChannelSetAttribute(_mixerHandle, ChannelAttribute.Buffer, length))
            {
                YargLogger.LogFormatError("Failed to set playback buffer: {0}!", Bass.LastError);
            }
        }

        protected override void DisposeManagedResources()
        {
            EndLoop_Internal();
            if (_channels.Count == 0)
            {
                _mainHandle.Dispose();
                return;
            }

            foreach (var channel in Channels)
            {
                channel.Dispose();
            }
        }

        protected override void DisposeUnmanagedResources()
        {
            if (_mixerHandle != 0)
            {
                if (!Bass.StreamFree(_mixerHandle))
                {
                    YargLogger.LogFormatError("Failed to free mixer stream (THIS WILL LEAK MEMORY!): {0}!", Bass.LastError);
                }
            }

            if (_sourceStream != 0)
            {
                if (!Bass.StreamFree(_sourceStream))
                {
                    YargLogger.LogFormatError("Failed to free mixer source stream (THIS WILL LEAK MEMORY!): {0}!", Bass.LastError);
                }
            }
        }

        private void CreateChannel(SongStem stem, int sourceStream, StreamHandle streamHandles, StreamHandle reverbHandles)
        {
            var pitchparams = BassAudioManager.SetPitchParams(stem, _speed, streamHandles, reverbHandles);
            var stemchannel = new BassStemChannel(_manager, stem, _clampStemVolume, sourceStream, pitchparams, streamHandles, reverbHandles);

            double length = BassAudioManager.GetLengthInSeconds(streamHandles.Stream);
            if (_mainHandle == null || length > _length)
            {
                _mainHandle = streamHandles;
                _length = length;
            }

            _channels.Add(stemchannel);
        }
    }
}