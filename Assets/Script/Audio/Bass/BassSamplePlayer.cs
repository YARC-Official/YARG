#nullable enable
using System;
using System.Collections.Generic;
using ManagedBass;
using ManagedBass.Mix;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     Manages playback of sound effect samples with support for multiple overlapping voices, voice limits,
    ///     volume envelopes, and smooth voice fading (used for drum SFX, crowd claps, and metronomes).
    /// </summary>
    internal sealed class BassSamplePlayer : IDisposable
    {
        private const    BassFlags       SAMPLE_CHANNEL_STREAM = (BassFlags) 2;
        private readonly HashSet<int>    _fadingVoices         = new();
        private readonly string          _name;
        private readonly Action?         _playbackEnded;
        private readonly BassAudioRouter _router;
        private readonly int             _sampleHandle;

        private readonly object       _stateLock = new();
        private readonly HashSet<int> _voices    = new();
        private          bool         _disposed;
        private          bool         _endNotificationQueued;

        private OutputChannel? _outputChannel;
        private double         _volume = 1;

        public BassSamplePlayer(BassAudioRouter router, int sampleHandle, string name, Action? playbackEnded = null)
        {
            _router = router;
            _sampleHandle = sampleHandle;
            _name = name;
            _playbackEnded = playbackEnded;
        }

        public bool IsPlaying
        {
            get
            {
                lock (_stateLock)
                {
                    foreach (int voice in _voices)
                    {
                        var state = Bass.ChannelIsActive(voice);
                        if (state is PlaybackState.Playing or PlaybackState.Stalled &&
                            !BassMix.ChannelHasFlag(voice, BassFlags.MixerChanPause))
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
        }

        public bool IsPaused
        {
            get
            {
                lock (_stateLock)
                {
                    foreach (int voice in _voices)
                    {
                        if (BassMix.ChannelHasFlag(voice, BassFlags.MixerChanPause))
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
        }

        public void Dispose()
        {
            lock (_stateLock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }

            if (!Bass.SampleFree(_sampleHandle))
            {
                YargLogger.LogFormatError("Failed to free {0} sample: {1}!", _name, Bass.LastError);
            }

            lock (_stateLock)
            {
                _voices.Clear();
                _fadingVoices.Clear();
            }
        }

        public bool Play(bool loop = false, int fadeInMilliseconds = 0)
        {
            lock (_stateLock)
            {
                if (_disposed)
                {
                    return false;
                }

                var flags = BassFlags.Decode | SAMPLE_CHANNEL_STREAM;
                if (loop)
                {
                    flags |= BassFlags.Loop;
                }

                int voice = CreateStream(flags);
                if (voice == 0)
                {
                    return false;
                }

                if (Bass.ChannelSetSync(voice, SyncFlags.Free, 0, OnVoiceFreed) == 0)
                {
                    var error = Bass.LastError;
                    Bass.StreamFree(voice);
                    YargLogger.LogFormatError("Failed to track {0} sample voice: {1}!", _name, error);
                    return false;
                }

                double initialVolume = fadeInMilliseconds > 0 ? 0 : _volume;
                if (!Bass.ChannelSetAttribute(voice, ChannelAttribute.Volume, initialVolume))
                {
                    YargLogger.LogFormatError("Failed to set {0} sample volume: {1}!", _name, Bass.LastError);
                }

                _voices.Add(voice);
                if (!_router.PlaySample(voice, _outputChannel))
                {
                    _voices.Remove(voice);
                    Bass.StreamFree(voice);
                    return false;
                }

                if (fadeInMilliseconds > 0 && !Bass.ChannelSlideAttribute(voice, ChannelAttribute.Volume,
                    (float) _volume, fadeInMilliseconds))
                {
                    YargLogger.LogFormatError("Failed to fade in {0}: {1}!", _name, Bass.LastError);
                }

                return true;
            }
        }

        public int CreateStream()
        {
            lock (_stateLock)
            {
                return _disposed ? 0 : CreateStream(BassFlags.Decode | SAMPLE_CHANNEL_STREAM);
            }
        }

        public void Stop(int fadeOutMilliseconds = 0)
        {
            lock (_stateLock)
            {
                foreach (int voice in new List<int>(_voices))
                {
                    bool paused = BassMix.ChannelHasFlag(voice, BassFlags.MixerChanPause);
                    if (fadeOutMilliseconds > 0 && !paused &&
                        Bass.ChannelSlideAttribute(voice, ChannelAttribute.Volume, -1, fadeOutMilliseconds))
                    {
                        _fadingVoices.Add(voice);
                        continue;
                    }

                    Bass.StreamFree(voice);
                }
            }
        }

        public void Pause()
        {
            lock (_stateLock)
            {
                foreach (int voice in _voices)
                {
                    var state = Bass.ChannelIsActive(voice);
                    if (state is PlaybackState.Playing or PlaybackState.Stalled)
                    {
                        BassMix.ChannelFlags(voice, BassFlags.MixerChanPause, BassFlags.MixerChanPause);
                    }
                }
            }
        }

        public void Resume()
        {
            lock (_stateLock)
            {
                foreach (int voice in _voices)
                {
                    if (BassMix.ChannelHasFlag(voice, BassFlags.MixerChanPause))
                    {
                        BassMix.ChannelFlags(voice, 0, BassFlags.MixerChanPause);
                    }
                }
            }
        }

        public void SetVolume(double volume)
        {
            lock (_stateLock)
            {
                _volume = volume;
                foreach (int voice in _voices)
                {
                    if (_fadingVoices.Contains(voice) || Bass.ChannelIsActive(voice) == PlaybackState.Stopped)
                    {
                        continue;
                    }

                    if (!Bass.ChannelSetAttribute(voice, ChannelAttribute.Volume, volume))
                    {
                        YargLogger.LogFormatError("Failed to set {0} sample volume: {1}!", _name, Bass.LastError);
                    }
                }
            }
        }

        public void SetOutputChannel(OutputChannel? outputChannel)
        {
            lock (_stateLock)
            {
                _outputChannel = outputChannel;
                foreach (int voice in _voices)
                {
                    if (Bass.ChannelIsActive(voice) != PlaybackState.Stopped)
                    {
                        _router.SetSampleOutputChannel(voice, outputChannel);
                    }
                }
            }
        }

        private int CreateStream(BassFlags flags)
        {
            int stream = Bass.SampleGetChannel(_sampleHandle, flags);
            if (stream == 0 && Bass.LastError != Errors.Timeout)
            {
                YargLogger.LogFormatError("Failed to create {0} sample voice: {1}!", _name, Bass.LastError);
            }

            return stream;
        }

        private void OnVoiceFreed(int _, int channelHandle, int __, IntPtr ___)
        {
            lock (_stateLock)
            {
                if (!_voices.Remove(channelHandle))
                {
                    return;
                }

                _fadingVoices.Remove(channelHandle);
                if (_disposed || _voices.Count > 0 || _playbackEnded == null || _endNotificationQueued)
                {
                    return;
                }

                _endNotificationQueued = true;
                UnityMainThreadCallback.QueueEvent(NotifyPlaybackEnded);
            }
        }

        private void NotifyPlaybackEnded()
        {
            Action? playbackEnded;
            lock (_stateLock)
            {
                _endNotificationQueued = false;
                playbackEnded = !_disposed && _voices.Count == 0 ? _playbackEnded : null;
            }

            playbackEnded?.Invoke();
        }
    }
}