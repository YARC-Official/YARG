using System;
using ManagedBass;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Input;

namespace YARG.Audio.BASS
{
    public class BassSampleChannel : ISampleChannel
    {
        private const double PLAYBACK_SUPPRESS_THRESHOLD = 0.05f;

        public SfxSample Sample { get; }

        private readonly IAudioManager _manager;
        private readonly string _path;
        private readonly int _playbackCount;

        private int _sfxHandle;

        private double _lastPlaybackTime;

        private bool _disposed;

        public BassSampleChannel(IAudioManager manager, string path, int playbackCount, SfxSample sample)
        {
            _manager = manager;
            _path = path;
            _playbackCount = playbackCount;

            _lastPlaybackTime = -1;

            Sample = sample;
        }

        ~BassSampleChannel()
        {
            Dispose(false);
        }

        public int Load()
        {
            if (_sfxHandle != 0)
            {
                return 0;
            }

            int handle = Bass.SampleLoad(_path, 0, 0, _playbackCount, BassFlags.Decode);
            if (handle == 0)
            {
                return (int) Bass.LastError;
            }

            _sfxHandle = handle;
            return 0;
        }

        public void Play()
        {
            if (_sfxHandle == 0) return;

            // Suppress playback if the last instance of this sample was too recent
            if (InputManager.CurrentInputTime - _lastPlaybackTime < PLAYBACK_SUPPRESS_THRESHOLD)
            {
                return;
            }

            int channel = Bass.SampleGetChannel(_sfxHandle);

            double volume = _manager.GetVolumeSetting(SongStem.Sfx) * AudioHelpers.SfxVolume[(int) Sample];
            if (!Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume, volume) || !Bass.ChannelPlay(channel))
                YargLogger.LogFormatError("Failed to play sample channel: {0}", Bass.LastError);

            _lastPlaybackTime = InputManager.CurrentInputTime;
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
                // Free managed resources here
                if (disposing)
                {
                }

                // Free unmanaged resources here
                if (_sfxHandle != 0)
                {
                    Bass.SampleFree(_sfxHandle);
                }

                _disposed = true;
            }
        }
    }
}