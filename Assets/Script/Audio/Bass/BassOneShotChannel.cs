#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ManagedBass;
using ManagedBass.Mix;
using YARG.Audio.BASS.Effects;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     Managed coordinator for a native BASS mixer source.
    ///     Sample scheduling and mixing never enter managed code.
    /// </summary>
    internal sealed class BassOneShotChannel : OneShotChannel
    {
        private const    int                DECODE_BUFFER_SIZE = 4096;
        private readonly Func<long, double> _getSongPosition;
        private readonly Func<float>        _getSpeed;
        private readonly double             _outputLeadTime;
        private readonly int                _sampleRate;
        private readonly int                _channels;
        private          double[]           _scheduledPlays;

        // Cached PCM decoded once in the constructor so UpdateSchedule() can rebuild just the
        // native (schedule-bearing) stream without re-decoding audio or touching BASS streams.
        private readonly float[]?                 _sample;
        private          double                   _currentVolume = 1;
        private          bool                     _currentEnabled = true;

        private readonly int                      _tempoStreamHandle;
        private          bool                     _disposed;
        private          BassNativeOneShotStream? _nativeStream;
        private          int                      _outputMixerHandle;
        private          bool                     _playbackPaused;
        private          int                      _targetMixerHandle;

        public BassOneShotChannel(int outputMixerHandle, int tempoStreamHandle, int sampleStream,
            IReadOnlyList<double> scheduledPlays, Func<long, double> getSongPosition, Func<float> getSpeed,
            double outputLeadTime, bool playbackPaused, OutputChannel? outputChannel = null)
        {
            _outputMixerHandle = outputMixerHandle;
            _targetMixerHandle = outputMixerHandle;
            _playbackPaused = playbackPaused;
            _tempoStreamHandle = tempoStreamHandle;
            _getSongPosition = getSongPosition;
            _getSpeed = getSpeed;
            _outputLeadTime = Math.Max(0, outputLeadTime);
            _scheduledPlays = scheduledPlays.ToArray();
            Array.Sort(_scheduledPlays);

            var info = Bass.ChannelGetInfo(outputMixerHandle);
            if (info.Frequency <= 0 || info.Channels <= 0 || (info.Flags & BassFlags.Float) == 0)
            {
                Bass.StreamFree(sampleStream);
                throw new ArgumentException("Playback mixer must use float sample data.", nameof(outputMixerHandle));
            }

            var speakerFlags = outputChannel is BassOutputChannel bassOutputChannel
                ? bassOutputChannel.Flags
                : BassFlags.Default;

            _sampleRate = info.Frequency;
            _channels = info.Channels;
            _sample = DecodeSample(sampleStream, _sampleRate, _channels, speakerFlags);
            if (_sample == null || _sample.Length == 0)
            {
                return;
            }

            _nativeStream = BassNativeOneShotStream.Create(
                _sampleRate, _channels, _sample, _scheduledPlays, _outputLeadTime);
            if (_nativeStream == null)
            {
                return;
            }

            AttachOutput(outputMixerHandle, playbackPaused);
        }

        internal event Action<BassOneShotChannel>? Disposed;

        public override void SetVolume(double volume)
        {
            _currentVolume = volume;
            _nativeStream?.SetVolume(volume);
        }

        public override void SetEnabled(bool enabled)
        {
            _currentEnabled = enabled;
            _nativeStream?.SetEnabled(enabled);
        }

        /// <summary>
        /// Rebuilds only the native one-shot stream with a new schedule, reusing the PCM
        /// decoded at construction time. Avoids the BASS stream creation + sample decode that
        /// makes full re-creation (<see cref="BassSong.CreateOneShotChannel"/>) too slow to call
        /// on every offset change during live calibration.
        /// Unity-side only: not part of the <see cref="OneShotChannel"/> base class, so callers
        /// need a reference typed as <see cref="BassOneShotChannel"/> to use it.
        /// </summary>
        public void UpdateSchedule(IReadOnlyList<double> scheduledPlays)
        {
            if (_disposed || _sample == null)
            {
                return;
            }

            _scheduledPlays = scheduledPlays.ToArray();
            Array.Sort(_scheduledPlays);

            _nativeStream?.Dispose();
            _nativeStream = BassNativeOneShotStream.Create(
                _sampleRate, _channels, _sample, _scheduledPlays, _outputLeadTime);
            if (_nativeStream == null)
            {
                return;
            }

            _nativeStream.SetVolume(_currentVolume);
            _nativeStream.SetEnabled(_currentEnabled);
            AttachOutput(_targetMixerHandle, _playbackPaused);
        }

        internal void DetachOutput()
        {
            bool detached = _nativeStream?.Detach() ?? true;
            if (detached)
            {
                _outputMixerHandle = 0;
            }
        }

        internal void AttachOutput(int outputMixerHandle, bool playbackPaused)
        {
            _targetMixerHandle = outputMixerHandle;
            if (_disposed || _nativeStream == null)
            {
                return;
            }

            if (!TryGetCurrentSongPosition(outputMixerHandle, out double songPosition, out float speed))
            {
                return;
            }

            if (_nativeStream.Attach(outputMixerHandle, songPosition, speed, playbackPaused))
            {
                _outputMixerHandle = outputMixerHandle;
            }
        }

        internal void SetPlaybackPaused(bool paused)
        {
            if (_nativeStream == null)
            {
                return;
            }

            _playbackPaused = paused;

            if (paused)
            {
                _nativeStream.SetPaused(true);
            }
            else
            {
                if (Reanchor(false))
                {
                    _nativeStream.SetPaused(false);
                }
            }
        }

        internal void ResetAfterSeek() => Reanchor(true);
        internal void ResetAfterSpeedChange() => Reanchor(false);

        private bool Reanchor(bool clearActiveVoices)
        {
            if (_disposed || _nativeStream == null)
            {
                return false;
            }

            if (_outputMixerHandle == 0)
            {
                AttachOutput(_targetMixerHandle, _playbackPaused);
                if (_outputMixerHandle == 0)
                {
                    return false;
                }
            }

            if (!TryGetCurrentSongPosition(_outputMixerHandle, out double songPosition, out float speed))
            {
                return false;
            }

            return _nativeStream.Resync(songPosition, speed, clearActiveVoices);
        }

        private bool TryGetCurrentSongPosition(int outputMixerHandle, out double songPosition, out float speed)
        {
            songPosition = 0;
            speed = 0;
            long tempo = Bass.ChannelGetPosition(_tempoStreamHandle, PositionFlags.Decode);
            if (tempo < 0)
            {
                YargLogger.LogFormatError("Failed to read one-shot playback position: {0}", Bass.LastError);
                return false;
            }

            speed = Math.Max(BassHelpers.MINIMUM_SPEED, _getSpeed());
            if (float.IsNaN(speed) || float.IsInfinity(speed))
            {
                YargLogger.LogFormatError("Failed to read one-shot playback speed: {0}", speed);
                return false;
            }

            songPosition = _getSongPosition(tempo);
            return !double.IsNaN(songPosition) && !double.IsInfinity(songPosition);
        }

        private static float[]? DecodeSample(int streamHandle, int sampleRate, int channelCount,
            BassFlags speakerFlags)
        {
            if (streamHandle == 0)
            {
                return null;
            }

            int converter = BassMix.CreateMixerStream(sampleRate, channelCount,
                BassFlags.Float | BassFlags.Decode | BassFlags.MixerEnd);
            if (converter == 0)
            {
                Bass.StreamFree(streamHandle);
                return null;
            }

            try
            {
                var flags = BassFlags.MixerChanNoRampin | speakerFlags;
                if (!BassMix.MixerAddChannel(converter, streamHandle, flags))
                {
                    if (speakerFlags != BassFlags.Default &&
                        !BassMix.MixerAddChannel(converter, streamHandle, BassFlags.MixerChanNoRampin))
                    {
                        return null;
                    }
                }

                var result = new List<float>();
                float[] buffer = new float[DECODE_BUFFER_SIZE];
                int bytesRead;
                while ((bytesRead = Bass.ChannelGetData(converter, buffer, buffer.Length * sizeof(float))) > 0)
                {
                    for (int i = 0; i < bytesRead / sizeof(float); i++)
                    {
                        result.Add(buffer[i]);
                    }
                }

                if (bytesRead < 0 && Bass.LastError != Errors.Ended)
                {
                    YargLogger.LogFormatError("Failed to decode one-shot sample: {0}", Bass.LastError);
                }

                return result.Count == 0 ? null : result.ToArray();
            }
            finally
            {
                Bass.StreamFree(converter);
                Bass.StreamFree(streamHandle);
            }
        }

        public override void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _nativeStream?.Dispose();
            _nativeStream = null;
            var callback = Disposed;
            Disposed = null;
            callback?.Invoke(this);
        }
    }
}
