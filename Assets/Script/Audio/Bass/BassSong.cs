#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ManagedBass;
using ManagedBass.Mix;
using UnityEngine;
using YARG.Audio.BASS.Effects;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Settings;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     Controls the playback of a loaded song during gameplay, managing stem volume mixing,
    ///     practice speed / whammy pitch adjustments, song position timeline tracking, and output connection.
    /// </summary>
    public sealed class BassSong : StemMixer
    {
        private const float WHAMMY_SYNC_INTERVAL_SECONDS = 1f;
        private const float MIN_PLAYBACK_SPEED           = 0.05f;
        private const float MAX_PLAYBACK_SPEED           = 51f;

        private readonly BassStemPipeline            _stemPipeline;
        private readonly HashSet<BassOneShotChannel> _oneShots     = new();
        private readonly HashSet<BassToneChannel>    _toneChannels = new();

        private readonly Timer _whammySyncTimer;
        private          int   _bufferLength;

        private BassSongConnection? _connection;
        private bool           _hasConnectedOutput;
        private double         _lastSongPosition;
        private int            _longestHandle;
        private OutputChannel? _outputChannel;
        private double         _playbackDelay;
        private long           _positionBeforeSeek;
        private bool           _resumeAfterAttach;
        private bool           _seekPending;
        private double         _seekPosition;
        private int            _songEndHandle;
        private float          _songSpeed = 1f;
        private float          _speed     = 1f;
        private double         _outputLatency;

        private double _volume = 1;

        private BassSong(string name, BassAudioManager manager, float speed, double volume,
            BassStemPipeline stemPipeline, bool clampStemVolume, OutputChannel? outputChannel)
            : base(name, manager, clampStemVolume)
        {
            _stemPipeline = stemPipeline;

            _whammySyncTimer = new Timer();

            SetOutputChannel_Internal(outputChannel);
            SetVolume_Internal(volume);
            SetPlaybackSpeed_Internal(speed, 0f, true);
            SetBufferLength_Internal(SettingsManager.Settings.PlaybackBufferLength.Value);
        }

        private static bool IsWhammyEnabled => GlobalAudioHandler.UseWhammyFx;

        private bool IsOutputPlaying => _connection?.IsPlaying == true;

        internal static BassSong? Create(string name, BassAudioManager manager, float speed, double volume,
            bool clampStemVolume, bool normalize, OutputChannel? outputChannel)
        {
            bool enableNormalization = normalize && SettingsManager.Settings.EnableNormalization.Value;
            var stemPipeline = BassStemPipeline.Create(44100, 2, BassFlags.Float | BassFlags.Decode,
                withCompressor: true, withNormalization: enableNormalization);
            if (stemPipeline == null)
            {
                return null;
            }

            return new BassSong(name, manager, speed, volume, stemPipeline, clampStemVolume, outputChannel);
        }

        internal event Action<BassSong>? Disposing;

        public override event Action SongEnd
        {
            add
            {
                EnsureSongEndSync();
                _songEnd += value;
            }
            remove => _songEnd -= value;
        }

        private void EnsureSongEndSync()
        {
            if (_songEndHandle != 0)
            {
                return;
            }

            void sync(int _, int __, int ___, IntPtr _____)
            {
                var end = _songEnd;
                if (end != null)
                {
                    UnityMainThreadCallback.QueueEvent(end.Invoke);
                }
            }

            _songEndHandle = BassMix.ChannelSetSync(_longestHandle, SyncFlags.End, 0, sync);
        }

        internal bool TryAttachOutput(BassOutput output)
        {
            var connection = BassSongConnection.Create(output, _stemPipeline.OutputHandle, _bufferLength,
                BassHelpers.ExponentialVolume(_volume), _outputChannel, _oneShots, _toneChannels);
            if (connection == null)
            {
                return false;
            }

            _connection = connection;
            return true;
        }

        internal void ActivateOutput()
        {
            bool resume = _resumeAfterAttach;
            _resumeAfterAttach = false;

            if (!_hasConnectedOutput)
            {
                _hasConnectedOutput = true;
                return;
            }

            if (resume)
            {
                PlayOutput();
            }
        }

        internal void DetachOutput()
        {
            if (IsOutputPlaying)
            {
                _resumeAfterAttach = true;
            }

            foreach (var oneShot in _oneShots)
            {
                oneShot.DetachOutput();
            }

            foreach (var channel in _toneChannels)
            {
                channel.DetachOutput();
            }

            _connection?.Dispose();
            _connection = null;
        }

        public ReadAheadStats GetReadAheadStats() => _connection?.GetReadAheadStats() ?? default;

        protected override int Play_Internal()
        {
            _stemPipeline.ApplyNormalizationGain();

            if (!IsOutputPlaying)
            {
                if (!PlayOutput())
                {
                    return -1;
                }
            }

            if (IsWhammyEnabled)
            {
                _whammySyncTimer.Start(WHAMMY_SYNC_INTERVAL_SECONDS, SyncWhammyDrift);
            }

            return 0;
        }

        private bool PlayOutput()
        {
            if (IsOutputPlaying)
            {
                return true;
            }

            if (_connection?.Play() != 0)
            {
                return false;
            }

            foreach (var oneShot in _oneShots)
            {
                oneShot.SetPlaybackPaused(false);
            }

            return true;
        }

        protected override int Pause_Internal()
        {
            if (!IsOutputPlaying)
            {
                return 0;
            }

            int error = _connection?.Pause() ?? -1;
            if (error != 0)
            {
                return error;
            }

            foreach (var oneShot in _oneShots)
            {
                oneShot.SetPlaybackPaused(true);
            }

            return 0;
        }

        protected override void FadeIn_Internal(double maxVolume, double duration)
        {
            _connection?.FadeTo(BassHelpers.ExponentialVolume(maxVolume),
                (int) (duration * SongMetadata.MILLISECOND_FACTOR));
        }

        protected override void FadeOut_Internal(double duration)
        {
            _connection?.FadeTo(0, (int) (duration * SongMetadata.MILLISECOND_FACTOR));
        }

        private void SyncWhammyDrift()
        {
            foreach (var channel in Channels)
            {
                if (Mathf.Approximately(channel.GetWhammyPitch(), 1f))
                {
                    channel.SetWhammyPitch(0f);
                }
            }
        }

        public double AlignmentDelay { get; private set; }

        private double TotalStreamDelay => AlignmentDelay + _playbackDelay;

        public double PlaybackStartOffset =>
            _outputLatency + (_connection?.PlaybackStartDelay ?? 0) + AlignmentDelay;

        protected override double GetPosition_Internal() => GetSongPosition();

        protected override SyncPosition GetSyncPosition_Internal()
        {
            bool measured = TryGetSongPositionSnapshot(out double heardPosition,
                out double decodePosition, out double outputDelay);
            double controlPosition = measured
                ? decodePosition - (_songSpeed * outputDelay) -
                    (_outputLatency * _songSpeed)
                : heardPosition - (_outputLatency * _songSpeed);
            return new SyncPosition(heardPosition, controlPosition);
        }

        protected override double GetControlPosition_Internal() => GetSyncPosition_Internal().Control;

        protected override double GetTempoStreamLatency_Internal() => _connection?.GetCommandDelay() ?? 0;

        protected override double GetVolume_Internal()
        {
            if (_connection == null)
            {
                return _volume;
            }

            return BassHelpers.LogarithmicVolume(_connection.GetVolume());
        }

        protected override void SetVolume_Internal(double volume)
        {
            _volume = volume;
            _connection?.SetVolume(BassHelpers.ExponentialVolume(volume));
        }

        protected override void SetOutputLatency_Internal(double latency) => _outputLatency = latency;

        protected override void SetPlaybackSpeed_Internal(float songSpeed, float syncAdjustment, bool shiftPitch)
        {
            float effectiveSpeed = Math.Clamp(songSpeed + syncAdjustment, MIN_PLAYBACK_SPEED, MAX_PLAYBACK_SPEED);
            _songSpeed = songSpeed;

            bool speedChanged = _speed != effectiveSpeed;
            if (speedChanged)
            {
                _speed = effectiveSpeed;
                _stemPipeline.SetSpeed(effectiveSpeed, shiftPitch);
            }

            if (!speedChanged)
            {
                return;
            }

            RefreshToneChannels();

            foreach (var oneShot in _oneShots)
            {
                oneShot.ResetAfterSpeedChange();
            }
        }

        protected override void SetPosition_Internal(double position)
        {
            bool wasPlaying = IsOutputPlaying;
            Pause_Internal();

            double playbackOffset = PlaybackStartOffset * _songSpeed;
            double preparedPosition = position + playbackOffset;
            double seekPosition = Math.Clamp(preparedPosition, 0, _length);
            double playbackDelay = Math.Max(0, -preparedPosition);

            _connection?.PrepareForSeek();

            if (!_stemPipeline.RealignChannels(playbackDelay, out double alignmentDelay))
            {
                _stemPipeline.RealignChannels(0, out _);
                return;
            }

            foreach (var channel in _channels)
            {
                channel.SetPosition(seekPosition);
            }

            ResetPosition(seekPosition, alignmentDelay, playbackDelay);

            _connection?.ResetAfterSeek();

            foreach (var oneShot in _oneShots)
            {
                oneShot.ResetAfterSeek();
            }

            RefreshToneChannels();

            if (wasPlaying)
            {
                Play_Internal();
            }
        }

        protected override bool AddChannels_Internal(Stream stream, params StemInfo[] stemInfos)
        {
            if (!_stemPipeline.AddStems(stream, stemInfos, out var createdStems, out double delay))
            {
                return false;
            }

            SetAlignmentDelay(delay);

            foreach (var stem in createdStems)
            {
                CreateChannel(stem.Stem, stem.SourceHandle, stem.StreamHandles, stem.ReverbHandles);
            }

            return true;
        }

        private void CreateChannel(SongStem stem, int sourceHandle, StreamHandle streamHandles,
            StreamHandle reverbHandles)
        {
            var pitch = BassHelpers.SetPitchParams(stem, streamHandles, reverbHandles);
            var channel = new BassStemChannel(_manager, stem, _clampStemVolume, sourceHandle, pitch, streamHandles,
                reverbHandles);

            double length = BassHelpers.GetLengthInSeconds(streamHandles.Stream);
            if (length > _length)
            {
                _longestHandle = streamHandles.Stream;
                _length = length;
            }

            _channels.Add(channel);
        }

        protected override bool RemoveChannel_Internal(SongStem stemToRemove)
        {
            bool removed = false;
            for (int i = _channels.Count - 1; i >= 0; i--)
            {
                var channel = _channels[i];
                if (channel.Stem == stemToRemove)
                {
                    channel.Dispose();
                    _channels.RemoveAt(i);
                    removed = true;
                }
            }

            if (!removed)
            {
                return false;
            }

            return _stemPipeline.RemoveStem(stemToRemove);
        }

        protected override void SetOutputChannel_Internal(OutputChannel? channel)
        {
            _outputChannel = channel;
            _connection?.SetOutputChannel(channel);

            // Keep the guide tone on the same speaker pair as the music when the setting changes
            // mid-song, matching the routing just applied to the tempo stream above.
            foreach (var toneChannel in _toneChannels)
            {
                toneChannel.SetOutputChannel(ToneOutputChannel);
            }
        }

        protected override void SetOutputDevice_Internal(OutputDevice device)
        {
            if (device is BassOutputDevice bassDevice)
            {
                _stemPipeline.SetDevice(bassDevice.DeviceId);
            }
        }

        public void SetReadAheadBuffer(int length) => SetBufferLength_Internal(length);

        public int ReadAheadBufferLength => _bufferLength;

        protected override void SetBufferLength_Internal(int length)
        {
            _bufferLength = Math.Max(0, length);
            _connection?.SetBufferLength(_bufferLength);
        }

        protected override int GetFFTData_Internal(float[] buffer, int fftSize, bool complex)
        {
            DataFlags? fftFlags = GetFFTDataFlags(fftSize);
            if (!fftFlags.HasValue)
            {
                return -1;
            }

            DataFlags flags = fftFlags.Value;
            if (complex)
            {
                flags |= DataFlags.FFTComplex;
            }

            return GetData(buffer, (int) flags);
        }

        private static DataFlags? GetFFTDataFlags(int fftSize)
        {
            return fftSize switch
            {
                8  => DataFlags.FFT256,
                9  => DataFlags.FFT512,
                10 => DataFlags.FFT1024,
                11 => DataFlags.FFT2048,
                12 => DataFlags.FFT4096,
                _  => null,
            };
        }

        protected override int GetSampleData_Internal(float[] buffer) =>
            GetData(buffer, (buffer.Length * sizeof(float)) | (int) DataFlags.Float);

        private int GetData(float[] buffer, int flags) => _connection?.GetData(buffer, flags) ?? -1;

        protected override int GetLevel_Internal(float[] level) => _connection?.GetLevel(level) ?? -1;

        public override OneShotChannel CreateOneShotChannel(int sampleStream, IReadOnlyList<double> scheduledPlays,
            double outputLeadTime = 0, OutputChannel? outputChannel = null)
        {
            var oneShot = _connection!.CreateOneShot(sampleStream, scheduledPlays, ConvertTempoBytesToSongPosition,
                () => _speed, outputLeadTime, outputChannel);
            oneShot.Disposed += RemoveOneShot;
            _oneShots.Add(oneShot);
            return oneShot;
        }

        private void RemoveOneShot(BassOneShotChannel oneShot) => _oneShots.Remove(oneShot);

        public override ToneChannel? CreateToneChannel(double volume, double fadeDuration)
        {
            // The tone is rendered onto the song mixer, which puts it inside the buffered song
            // branch: it travels through the read-ahead buffer alongside the music it is mixed into,
            // and it follows song volume and fades. It also stays out of the data behind
            // GetFFTData, which is read from the tempo stream upstream of this point and drives the
            // venue visuals. Song position comes from the tempo stream, so the offset below is the
            // same mapping ConvertTempoBytesToSongPosition applies on the game thread.
            var channel = BassToneChannel.Create(_stemPipeline.OutputHandle, volume, fadeDuration);
            if (channel == null)
            {
                return null;
            }

            channel.SetTiming(SongTimeOffset, _speed);
            // Route the tone before it is attached, so its first rendered block already lands on
            // the configured pair instead of every speaker.
            channel.SetOutputChannel(ToneOutputChannel);

            // The song mixer is recreated on every output device change, so the channel is tracked
            // here and re-attached by BassSongConnection.Create as one-shots are. With no connection
            // yet it stays registered and unattached, and the next TryAttachOutput picks it up.
            if (_connection != null && !_connection.AttachTone(channel))
            {
                channel.Dispose();
                return null;
            }

            channel.Disposed += RemoveToneChannel;
            _toneChannels.Add(channel);
            return channel;
        }

        private void RemoveToneChannel(BassToneChannel channel) => _toneChannels.Remove(channel);

        /// <summary>
        /// Maps a tempo stream position in seconds onto a song position. Published to the native
        /// tone DSP, which reads the tempo stream directly on the render thread.
        /// </summary>
        private double SongTimeOffset => _seekPosition - TotalStreamDelay;

        /// <summary>
        /// The output channel the guide tone should render into, taken from the song's own
        /// routing so it follows the same speaker pair as the music. A 1-based channel value, or
        /// 0 to broadcast to every channel when no routing is set.
        /// </summary>
        private uint ToneOutputChannel =>
            _outputChannel is { ChannelId: > 0 } outputChannel ? (uint) outputChannel.ChannelId : 0u;

        /// <summary>
        /// Republishes timing to the tone channels, and retries any that are registered but not
        /// attached, so a failed attach during an output device change does not silently disable
        /// the tone for the rest of the song.
        /// </summary>
        private void RefreshToneChannels()
        {
            foreach (var channel in _toneChannels)
            {
                channel.SetTiming(SongTimeOffset, _speed);
                channel.SetOutputChannel(ToneOutputChannel);
                if (_connection != null)
                {
                    channel.Reattach();
                }
            }
        }

        protected override void DisposeManagedResources()
        {
            _whammySyncTimer.Stop();

            _stemPipeline.StopNormalization();

            foreach (var channel in Channels)
            {
                channel.Dispose();
            }
        }

        protected override void DisposeUnmanagedResources()
        {
            Disposing?.Invoke(this);
            Disposing = null;
            DetachOutput();
            foreach (var oneShot in _oneShots.ToArray())
            {
                oneShot.Dispose();
            }

            _oneShots.Clear();

            foreach (var channel in _toneChannels.ToArray())
            {
                channel.Dispose();
            }

            _toneChannels.Clear();
            _stemPipeline.Dispose();
        }


        private double GetSongPosition()
        {
            TryGetSongPositionSnapshot(out double heardPosition, out _, out _);
            return heardPosition;
        }

        private bool TryGetSongPositionSnapshot(out double heardPosition,
            out double decodePosition, out double outputDelay)
        {
            heardPosition = _lastSongPosition;
            decodePosition = _lastSongPosition;
            outputDelay = 0;

            if (_connection?.TryGetPositionSnapshot(out var snapshot) != true)
            {
                YargLogger.LogFormatError("Failed to get byte position: {0}!", Bass.LastError);
                return false;
            }

            outputDelay = snapshot.OutputDelay;
            if (_seekPending)
            {
                if (snapshot.HeardPosition >= _positionBeforeSeek)
                {
                    heardPosition = _lastSongPosition = _seekPosition - TotalStreamDelay;
                    return false;
                }

                _seekPending = false;
            }

            bool heardValid = TryConvertTempoBytesToSongPosition(snapshot.HeardPosition, out heardPosition);
            bool decodeValid = TryConvertTempoBytesToSongPosition(snapshot.DecodePosition, out decodePosition);
            if (heardValid)
            {
                _lastSongPosition = heardPosition;
            }

            return heardValid && decodeValid;
        }

        private double ConvertTempoBytesToSongPosition(long tempoBytes)
        {
            TryConvertTempoBytesToSongPosition(tempoBytes, out double position);
            return position;
        }

        private bool TryConvertTempoBytesToSongPosition(long tempoBytes, out double position)
        {
            bool succeeded = _stemPipeline.TryGetPositionSeconds(tempoBytes, out double seconds);
            position = succeeded ? seconds - TotalStreamDelay + _seekPosition : _lastSongPosition;
            return succeeded;
        }

        private void ResetPosition(double seekPosition, double alignmentDelay, double playbackDelay)
        {
            _positionBeforeSeek = _connection?.GetPosition() ?? -1;
            if (_positionBeforeSeek < 0)
            {
                YargLogger.LogFormatError("Failed to capture position before seek: {0}!", Bass.LastError);
            }

            _seekPending = _positionBeforeSeek > 0;
            _seekPosition = seekPosition;
            AlignmentDelay = alignmentDelay;
            _playbackDelay = playbackDelay;
            _lastSongPosition = seekPosition - TotalStreamDelay;
            RefreshToneChannels();
        }

        private void SetAlignmentDelay(double delay)
        {
            _lastSongPosition -= delay - AlignmentDelay;
            AlignmentDelay = delay;
            RefreshToneChannels();
        }
    }
}
