#nullable enable
using System;
using YARG.Audio.BASS.Effects;
using YARG.Core.Audio;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     Managed coordinator for a native synthesized tone.
    ///     Schedule scanning and tone synthesis never enter managed code.
    /// </summary>
    internal sealed class BassToneChannel : ToneChannel
    {
        private readonly BassSineSynthDsp _dsp;

        private bool _disposed;
        private int  _targetMixerHandle;

        private BassToneChannel(BassSineSynthDsp dsp)
        {
            _dsp = dsp;
        }

        internal event Action<BassToneChannel>? Disposed;

        internal bool IsAttached => _dsp.IsAttached;

        internal static BassToneChannel? Create(int tempoStreamHandle, double volume,
            double fadeDuration)
        {
            var dsp = BassSineSynthDsp.Create(tempoStreamHandle, (float) volume, (float) fadeDuration);
            return dsp == null ? null : new BassToneChannel(dsp);
        }

        public override void SetSchedule(ReadOnlySpan<ToneSegment> segments)
        {
            if (_disposed)
            {
                return;
            }

            _dsp.SetSchedule(segments);
        }

        /// <summary>
        ///     Attaches to a song mixer. Safe to call again after <see cref="DetachOutput"/> to follow
        ///     the song onto a mixer recreated by an output device change; the schedule is retained.
        /// </summary>
        internal bool AttachOutput(int mixerHandle)
        {
            _targetMixerHandle = mixerHandle;
            return !_disposed && _dsp.Attach(mixerHandle);
        }

        internal void DetachOutput()
        {
            if (!_disposed)
            {
                _dsp.Detach();
            }
        }

        /// <summary>
        ///     Retries an attach that previously failed, so a device change cannot silently disable
        ///     the tone for the rest of the song.
        /// </summary>
        internal void Reattach()
        {
            if (!_disposed && !_dsp.IsAttached && _targetMixerHandle != 0)
            {
                _dsp.Attach(_targetMixerHandle);
            }
        }

        internal void SetTiming(double songTimeOffset, float playbackSpeed)
        {
            if (!_disposed)
            {
                _dsp.SetTiming(songTimeOffset, playbackSpeed);
            }
        }

        public override void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _dsp.Dispose();

            var callback = Disposed;
            Disposed = null;
            callback?.Invoke(this);
        }
    }
}
