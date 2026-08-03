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
    /// Managed coordinator for a native BASS mixer source.
    /// Sample scheduling and mixing never enter managed code.
    /// </summary>
    internal sealed class BassOneShotChannel : OneShotChannel
    {
        private const int DecodeBufferSize = 4096;

        private readonly int _tempoStreamHandle;
        private readonly int _sampleRate;
        private readonly Func<long, double> _getSongPosition;
        private readonly Func<float> _getSpeed;
        private readonly double _outputLeadTime;
        private readonly double[] _scheduledPlays;
        private BassNativeOneShotStream _nativeStream;
        private readonly int _targetMixerHandle;
        private int _outputMixerHandle;
        private bool _playbackPaused;
        private bool _disposed;

        internal event Action<BassOneShotChannel> Disposed;

        public BassOneShotChannel(int outputMixerHandle, int tempoStreamHandle, int sampleStream,
            IReadOnlyList<double> scheduledPlays, Func<long, double> getSongPosition,
            Func<float> getSpeed, double outputLeadTime, bool playbackPaused)
        {
            _outputMixerHandle = outputMixerHandle;
            _targetMixerHandle = outputMixerHandle;
            _playbackPaused = playbackPaused;
            _tempoStreamHandle = tempoStreamHandle;
            _getSongPosition = getSongPosition ?? throw new ArgumentNullException(nameof(getSongPosition));
            _getSpeed = getSpeed ?? throw new ArgumentNullException(nameof(getSpeed));
            _outputLeadTime = Math.Max(0, outputLeadTime);
            _scheduledPlays = scheduledPlays?.ToArray() ?? throw new ArgumentNullException(nameof(scheduledPlays));
            Array.Sort(_scheduledPlays);

            var info = Bass.ChannelGetInfo(outputMixerHandle);
            if (info.Frequency <= 0 || info.Channels <= 0 || (info.Flags & BassFlags.Float) == 0)
            {
                Bass.StreamFree(sampleStream);
                throw new ArgumentException("Playback mixer must use float sample data.", nameof(outputMixerHandle));
            }
            _sampleRate = info.Frequency;
            float[] sample = DecodeSample(sampleStream, _sampleRate, info.Channels);
            if (sample == null || sample.Length == 0) return;

            _nativeStream = BassNativeOneShotStream.Create(
                _sampleRate, info.Channels, sample, _scheduledPlays, _outputLeadTime);
            if (_nativeStream == null) return;
            AttachOutput(outputMixerHandle, playbackPaused);
        }

        public override void SetVolume(double volume)
        {
            _nativeStream?.SetVolume(volume);
        }

        public override void SetEnabled(bool enabled)
        {
            _nativeStream?.SetEnabled(enabled);
        }

        internal void AttachOutput(int outputMixerHandle, bool playbackPaused)
        {
            if (_disposed || _nativeStream == null) return;
            if (!TryGetCurrentSongPosition(outputMixerHandle,
                out double songPosition, out float speed)) return;
            if (_nativeStream.Attach(outputMixerHandle, songPosition, speed, playbackPaused))
                _outputMixerHandle = outputMixerHandle;
        }

        internal void SetPlaybackPaused(bool paused)
        {
            if (_nativeStream == null) return;
            _playbackPaused = paused;

            if (paused)
            {
                _nativeStream.SetPaused(true);
            }
            else
            {
                // Parent mixer can continue while song is paused. Re-anchor before rendering
                // again so the one-shot cursor excludes paused time.
                if (Reanchor(clearActiveVoices: false)) _nativeStream.SetPaused(false);
            }
        }

        internal void PrepareForSeek() { }
        internal void ResetAfterSeek() => Reanchor(clearActiveVoices: true);
        internal void ResetAfterSpeedChange() => Reanchor(clearActiveVoices: false);

        private bool Reanchor(bool clearActiveVoices)
        {
            if (_disposed || _nativeStream == null) return false;
            if (_outputMixerHandle == 0)
            {
                // Initial attach can fail (e.g. transient position read error). Retry on the
                // next re-anchor instead of leaving the one-shot dead for the whole song.
                AttachOutput(_targetMixerHandle, _playbackPaused);
                if (_outputMixerHandle == 0) return false;
            }
            if (!TryGetCurrentSongPosition(_outputMixerHandle,
                out double songPosition, out float speed)) return false;
            return _nativeStream.Resync(songPosition, speed, clearActiveVoices);
        }

        private bool TryGetCurrentSongPosition(int outputMixerHandle,
            out double songPosition, out float speed)
        {
            songPosition = 0;
            speed = 0;
            long tempo = Bass.ChannelGetPosition(_tempoStreamHandle, PositionFlags.Decode);
            if (tempo < 0)
            {
                YargLogger.LogFormatError("Failed to read one-shot playback position: {0}", Bass.LastError);
                return false;
            }

            speed = Math.Max(0.0001f, _getSpeed());
            if (float.IsNaN(speed) || float.IsInfinity(speed))
            {
                YargLogger.LogFormatError("Failed to read one-shot playback speed: {0}", speed);
                return false;
            }
            songPosition = _getSongPosition(tempo);
            return !double.IsNaN(songPosition) && !double.IsInfinity(songPosition);
        }

        private static float[] DecodeSample(int streamHandle, int sampleRate, int channelCount)
        {
            if (streamHandle == 0) return null;
            int converter = BassMix.CreateMixerStream(sampleRate, channelCount,
                BassFlags.Float | BassFlags.Decode | BassFlags.MixerEnd);
            if (converter == 0) { Bass.StreamFree(streamHandle); return null; }
            try
            {
                if (!BassMix.MixerAddChannel(converter, streamHandle, BassFlags.MixerChanNoRampin)) return null;
                var result = new List<float>();
                var buffer = new float[DecodeBufferSize];
                int bytesRead;
                while ((bytesRead = Bass.ChannelGetData(converter, buffer, buffer.Length * sizeof(float))) > 0)
                    for (int i = 0; i < bytesRead / sizeof(float); i++) result.Add(buffer[i]);
                if (bytesRead < 0 && Bass.LastError != Errors.Ended)
                    YargLogger.LogFormatError("Failed to decode one-shot sample: {0}", Bass.LastError);
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
            if (_disposed) return;
            _disposed = true;
            _nativeStream?.Dispose();
            _nativeStream = null;
            var callback = Disposed;
            Disposed = null;
            callback?.Invoke(this);
        }
    }
}
