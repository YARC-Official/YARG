#nullable enable
using System;
using System.Collections.Generic;
using ManagedBass;
using ManagedBass.Mix;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    internal readonly struct BassSourcePositionSnapshot
    {
        public readonly long   HeardPosition;
        public readonly long   DecodePosition;
        public readonly double OutputDelay;

        public BassSourcePositionSnapshot(long heardPosition, long decodePosition, double outputDelay)
        {
            HeardPosition = heardPosition;
            DecodePosition = decodePosition;
            OutputDelay = outputDelay;
        }
    }

    /// <summary>
    ///     Plugs a song's audio pipeline into an active audio output (ASIO or shared).
    ///     Handles prefilling the read-ahead buffer, volume scaling, pausing/resuming, and synchronization delays.
    /// </summary>
    internal sealed class BassSongConnection : IDisposable
    {
        private const int PREFILL_TIMEOUT_MILLISECONDS = 2000;

        private readonly BassOutput          _output;
        private readonly BassReadAheadStream _readAhead;
        private readonly BassMixer           _songMixer;
        private readonly BassMixer           _volumeMixer;
        private readonly int                 _sampleRate;
        private          int                 _bufferLengthMilliseconds;
        private          bool                _bufferNeedsPrefill = true;
        private          bool                _disposed;
        private          int                 _endpointDelayFrames;
        private          bool                _mixerCanReset;
        private          bool                _wasPlayed;

        private BassSongConnection(BassOutput output, int tempoStreamHandle, BassMixer songMixer,
            BassMixer volumeMixer, BassReadAheadStream readAhead, int sampleRate, int bufferLengthMilliseconds)
        {
            _output = output;
            _readAhead = readAhead;
            _songMixer = songMixer;
            _volumeMixer = volumeMixer;
            _sampleRate = sampleRate;
            _bufferLengthMilliseconds = bufferLengthMilliseconds;
            TempoStreamHandle = tempoStreamHandle;
        }

        private int TempoStreamHandle { get; }

        public bool IsPlaying
        {
            get
            {
                bool active = Bass.ChannelIsActive(TempoStreamHandle) is PlaybackState.Playing or PlaybackState.Stalled;
                return active && !BassMix.ChannelHasFlag(TempoStreamHandle, BassFlags.MixerChanPause);
            }
        }

        public double PlaybackStartDelay => _output.SongPlaybackStartDelay;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            SetSongOutputPaused(true);
            _output.DetachSong(_volumeMixer.Handle);
            _readAhead.Dispose();
            _volumeMixer.Dispose();

            _songMixer.RemoveChannel(TempoStreamHandle);
            _songMixer.Dispose();
        }

        public static BassSongConnection? Create(BassOutput output, int tempoStreamHandle, int bufferLengthMilliseconds,
            double volume, OutputChannel? outputChannel, IReadOnlyCollection<BassOneShotChannel> oneShots,
            IReadOnlyCollection<BassToneChannel> toneChannels)
        {
            output.Device.Use();
            var songFlags = BassFlags.Float | BassFlags.Decode | BassFlags.MixerNonStop | BassFlags.MixerPositionEx;
            var songMixer = BassMixer.Create(output.SampleRate, output.ChannelCount, songFlags);
            if (songMixer == null)
            {
                return null;
            }

            var tempoFlags = BassFlags.MixerChanNoRampin | BassFlags.MixerChanBuffer | BassFlags.MixerChanPause;
            if (!songMixer.AddChannel(tempoStreamHandle, tempoFlags))
            {
                songMixer.Dispose();
                return null;
            }

            var readAhead = BassReadAheadStream.Create(output.Device.DeviceId, songMixer.Handle, output.SampleRate,
                output.ChannelCount, output.MinimumBlockFrames, bufferLengthMilliseconds,
                output.UsesIndependentClock);
            if (readAhead == null)
            {
                songMixer.Dispose();
                return null;
            }

            var volumeFlags = BassFlags.Float | BassFlags.Decode | BassFlags.MixerNonStop;
            var volumeMixer = BassMixer.Create(output.SampleRate, output.ChannelCount, volumeFlags);
            if (volumeMixer == null)
            {
                readAhead.Dispose();
                songMixer.Dispose();
                return null;
            }

            var readAheadFlags = BassFlags.MixerChanNoRampin | BassFlags.MixerChanPause;
            if (!volumeMixer.AddChannel(readAhead.StreamHandle, readAheadFlags))
            {
                volumeMixer.Dispose();
                readAhead.Dispose();
                songMixer.Dispose();
                return null;
            }

            if (!output.AttachSong(volumeMixer.Handle))
            {
                volumeMixer.Dispose();
                readAhead.Dispose();
                songMixer.Dispose();
                return null;
            }

            var connection = new BassSongConnection(output, tempoStreamHandle, songMixer, volumeMixer,
                readAhead, output.SampleRate, bufferLengthMilliseconds);
            connection.UpdateMixerLatency();
            connection.SetVolume(volume);
            connection.SetOutputChannel(outputChannel);

            foreach (var oneShot in oneShots)
            {
                connection.AttachOneShot(oneShot);
            }

            // A tone that fails to attach is left registered rather than failing the connection:
            // losing an optional effect is preferable to losing all audio on a device change, and
            // BassSong retries it on the next seek. The attach logs its own reason.
            foreach (var toneChannel in toneChannels)
            {
                _ = connection.AttachTone(toneChannel);
            }

            return connection;
        }

        public int Play()
        {
            if (_bufferNeedsPrefill && !FlushBuffer())
            {
                return -1;
            }

            int error = SetSongPaused(false);
            if (error != 0)
            {
                return error;
            }

            if (_bufferNeedsPrefill)
            {
                if (!_readAhead.Prefill(PREFILL_TIMEOUT_MILLISECONDS))
                {
                    SetSongPaused(true);
                    YargLogger.LogError("Failed to prefill song read-ahead buffer");
                    return -1;
                }

                _bufferNeedsPrefill = false;
            }

            _wasPlayed = true;
            _mixerCanReset = false;
            SetSongOutputPaused(false);
            return 0;
        }

        public int Pause()
        {
            int error = SetSongPaused(true);
            if (error == 0)
            {
                SetSongOutputPaused(true);
            }

            return error;
        }

        public void PrepareForSeek()
        {
            if (!_wasPlayed)
            {
                return;
            }

            _mixerCanReset = FlushBuffer();
            _bufferNeedsPrefill = _mixerCanReset;
        }

        public void ResetAfterSeek()
        {
            if (!_wasPlayed || !_mixerCanReset)
            {
                return;
            }

            if (!_songMixer.SetPositionBytes(0))
            {
                YargLogger.LogFormatError("Failed to reset song mixer position: {0}", Bass.LastError);
            }
        }

        public void FadeTo(double volume, int durationMilliseconds) =>
            _volumeMixer.SlideAttribute(ChannelAttribute.Volume, (float) volume, durationMilliseconds);

        public double GetVolume()
        {
            _volumeMixer.GetAttribute(ChannelAttribute.Volume, out float volume);
            return volume;
        }

        public void SetVolume(double volume)
        {
            if (!_volumeMixer.SetAttribute(ChannelAttribute.Volume, (float) volume))
            {
                YargLogger.LogFormatError("Failed to set song volume: {0}", Bass.LastError);
            }
        }

        public int GetData(float[] buffer, int flags) => BassMix.ChannelGetData(TempoStreamHandle, buffer, flags);

        public int GetLevel(float[] level)
        {
            var flags = LevelRetrievalFlags.Mono | LevelRetrievalFlags.RMS;
            return BassMix.ChannelGetLevel(TempoStreamHandle, level, 0.2f, flags) >= 0
                ? (int) Errors.OK
                : (int) Bass.LastError;
        }

        public long GetPosition() => TryGetPositionSnapshot(out var snapshot) ? snapshot.HeardPosition : -1;

        public bool TryGetPositionSnapshot(out BassSourcePositionSnapshot snapshot)
        {
            if (!_readAhead.TryGetPositionSnapshot(TempoStreamHandle, _endpointDelayFrames, out var native))
            {
                snapshot = default;
                return false;
            }

            snapshot = new BassSourcePositionSnapshot(native.HeardPosition, native.DecodePosition,
                native.TotalDelayFrames / (double) _sampleRate);
            return true;
        }

        public double GetCommandDelay()
        {
            var stats = _readAhead.GetStats();
            return (stats.QueuedFrames + _endpointDelayFrames) / (double) _sampleRate;
        }

        public void SetBufferLength(int lengthMilliseconds)
        {
            int bufferLength = Math.Max(0, lengthMilliseconds);
            if (bufferLength == _bufferLengthMilliseconds)
            {
                return;
            }

            bool resume = IsPlaying;
            if (!FlushBuffer() || !_readAhead.SetBufferLength(bufferLength))
            {
                YargLogger.LogError("Failed to change song read-ahead buffer");
                _bufferNeedsPrefill = true;
                return;
            }

            _bufferLengthMilliseconds = bufferLength;
            _bufferNeedsPrefill = true;
            if (resume && _readAhead.Prefill(PREFILL_TIMEOUT_MILLISECONDS))
            {
                _bufferNeedsPrefill = false;
                SetSongOutputPaused(false);
            }

            UpdateMixerLatency();
        }

        public void SetOutputChannel(OutputChannel? outputChannel)
        {
            var flags = outputChannel is BassOutputChannel bassOutputChannel
                ? bassOutputChannel.Flags
                : BassFlags.Default;
            BassMix.ChannelFlags(TempoStreamHandle, flags, BassFlags.SpeakerFront);
        }

        public BassOneShotChannel CreateOneShot(int sampleStream, IReadOnlyList<double> scheduledPlays,
            Func<long, double> getPosition, Func<float> getSpeed, double outputLeadTime,
            OutputChannel? outputChannel = null) =>
            new(_songMixer.Handle, TempoStreamHandle, sampleStream, scheduledPlays, getPosition, getSpeed, outputLeadTime,
                !IsPlaying, outputChannel);

        public void AttachOneShot(BassOneShotChannel oneShot) => oneShot.AttachOutput(_songMixer.Handle, !IsPlaying);

        public bool AttachTone(BassToneChannel toneChannel) => toneChannel.AttachOutput(_songMixer.Handle);

        public ReadAheadStats GetReadAheadStats() => _readAhead.GetStats();

        private void UpdateMixerLatency()
        {
            _endpointDelayFrames = Math.Max(0, _output.EndpointDelayFrames);
            uint readAheadFrames = _readAhead.GetStats().TargetFrames;
            float latency = (readAheadFrames + _endpointDelayFrames) / (float) _sampleRate;
            if (!_songMixer.SetAttribute(ChannelAttribute.MixerLatency, latency))
            {
                YargLogger.LogFormatError("Failed to set song mixer latency: {0}", Bass.LastError);
            }
        }

        private bool FlushBuffer()
        {
            SetSongOutputPaused(true);
            if (_readAhead.Flush())
            {
                return true;
            }

            YargLogger.LogError("Failed to flush song read-ahead buffer");
            return false;
        }

        private int SetSongPaused(bool paused)
        {
            var flags = paused ? BassFlags.MixerChanPause : BassFlags.Default;
            return BassMix.ChannelFlags(TempoStreamHandle, flags, BassFlags.MixerChanPause) >= 0
                ? 0
                : (int) Bass.LastError;
        }

        private void SetSongOutputPaused(bool paused)
        {
            var flags = paused ? BassFlags.MixerChanPause : BassFlags.Default;
            if (!_volumeMixer.SetFlags(flags, BassFlags.MixerChanPause))
            {
                YargLogger.LogFormatError("Failed to {0} song output: {1}", paused ? "pause" : "play", Bass.LastError);
            }

            if (BassMix.ChannelFlags(_readAhead.StreamHandle, flags, BassFlags.MixerChanPause) < 0)
            {
                YargLogger.LogFormatError("Failed to {0} song read-ahead: {1}", paused ? "pause" : "play",
                    Bass.LastError);
            }
        }
    }
}
