#nullable enable
using System;
using ManagedBass;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS.Effects
{
    /// <summary>
    /// Bridges a backend-agnostic <see cref="IMixerDspProcessor"/> onto a BASS mixer's DSP chain.
    /// The processor is invoked on the audio render thread for every block the mixer produces,
    /// with the song positions spanning that block.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="BassGainDsp"/> and <see cref="BassFreeverbDsp"/> this is a plain
    /// <see cref="IDisposable"/> rather than a SafeHandle, because it owns a managed callback
    /// delegate and a BASS handle rather than a native <c>yarg_audio</c> pointer.
    /// </remarks>
    internal sealed class BassMixerDsp : IDisposable
    {
        private const string EFFECT_NAME = "guide mixer DSP";

        private readonly IMixerDspProcessor _processor;
        private readonly int                _priority;
        private readonly Func<long, double> _getSongPosition;
        private readonly Func<float>        _getSpeed;

        // ManagedBass already roots the delegate for the lifetime of the attachment (ChannelSetDSP
        // registers it and ChannelRemoveDSP releases it), so this reference is belt and braces: it
        // keeps the delegate reachable even if that implementation detail ever changes.
        private DSPProcedure? _callback;

        private int  _mixerHandle;
        private int  _dspHandle;
        private bool _disposed;

        // Set on the audio thread if the processor throws; stops it being called again.
        private volatile bool _faulted;

        /// <param name="processor">Receives each block of mixed audio on the audio thread.</param>
        /// <param name="getSongPosition">Converts tempo stream decode bytes to a song position in seconds.</param>
        /// <param name="getSpeed">Supplies the current playback speed.</param>
        /// <param name="priority">DSP priority, passed through to <see cref="Bass.ChannelSetDSP"/>.</param>
        internal BassMixerDsp(IMixerDspProcessor processor, Func<long, double> getSongPosition,
            Func<float> getSpeed, int priority = 0)
        {
            _processor = processor ?? throw new ArgumentNullException(nameof(processor));
            _getSongPosition = getSongPosition ?? throw new ArgumentNullException(nameof(getSongPosition));
            _getSpeed = getSpeed ?? throw new ArgumentNullException(nameof(getSpeed));
            _priority = priority;
        }

        internal event Action<BassMixerDsp>? Disposed;

        /// <summary>
        /// Whether the DSP is currently attached to a mixer. False after <see cref="DetachOutput"/>,
        /// or if an attach attempt failed, in which case it can be retried.
        /// </summary>
        internal bool IsAttached => _dspHandle != 0;

        /// <summary>
        /// Attaches to <paramref name="mixerHandle"/>, deriving song positions from
        /// <paramref name="tempoStreamHandle"/>. Safe to call again after <see cref="DetachOutput"/>
        /// to follow the song onto a recreated output connection.
        /// </summary>
        internal bool AttachOutput(int mixerHandle, int tempoStreamHandle)
        {
            if (_disposed)
            {
                return false;
            }

            DetachOutput();

            var info = Bass.ChannelGetInfo(mixerHandle);
            if (info.Frequency <= 0 || info.Channels <= 0 || (info.Flags & BassFlags.Float) == 0)
            {
                YargLogger.LogFormatError(
                    "Cannot attach {0}: mixer {1} must use float sample data " +
                    "(frequency={2}, channels={3}, flags={4}).",
                    EFFECT_NAME, mixerHandle, info.Frequency, info.Channels, info.Flags);
                return false;
            }

            int sampleRate = info.Frequency;
            int channels = info.Channels;

            // Runs on the read-ahead render thread. Every guard below drops the block silently rather
            // than logging: this is the audio path, and YargLogger takes a lock and allocates.
            var callback = new DSPProcedure((_, _, buffer, length, _) =>
            {
                int frames = length / (sizeof(float) * channels);
                if (frames <= 0 || _faulted)
                {
                    return;
                }

                // Read the tempo stream's decode position the same way one-shot scheduling does. The
                // mixer pulls the tempo stream synchronously, so this is the song time at the end of
                // the block being processed.
                long tempoBytes = Bass.ChannelGetPosition(tempoStreamHandle, PositionFlags.Decode);
                if (tempoBytes < 0)
                {
                    return;
                }

                double songTimeEnd = _getSongPosition(tempoBytes);
                if (double.IsNaN(songTimeEnd) || double.IsInfinity(songTimeEnd))
                {
                    return;
                }

                float speed = _getSpeed();
                if (float.IsNaN(speed) || float.IsInfinity(speed))
                {
                    return;
                }

                // Song time advances at the playback speed relative to real time. The processor is
                // given both ends of the block because it has no access to the speed to scale by.
                double songTimeStart =
                    songTimeEnd - (frames / (double) sampleRate) * Math.Max(BassHelpers.MINIMUM_SPEED, speed);

                // An exception unwinding into BASS's native caller would abort the process, so the
                // processor is latched off instead. Reported once, from off the audio thread.
                try
                {
                    unsafe
                    {
                        var span = new Span<float>((void*) buffer, length / sizeof(float));
                        _processor.ProcessAudio(span, frames, channels, sampleRate, songTimeStart, songTimeEnd);
                    }
                }
                catch (Exception exception)
                {
                    _faulted = true;
                    UnityMainThreadCallback.QueueEvent(() => YargLogger.LogException(exception,
                        $"Disabled {EFFECT_NAME} after the processor threw on the audio thread"));
                }
            });

            int dspHandle = Bass.ChannelSetDSP(mixerHandle, callback, IntPtr.Zero, _priority);
            if (dspHandle == 0)
            {
                YargLogger.LogFormatError("Failed to attach {0} to mixer {1}: {2}",
                    EFFECT_NAME, mixerHandle, Bass.LastError);
                return false;
            }

            _callback = callback;
            _mixerHandle = mixerHandle;
            _dspHandle = dspHandle;
            return true;
        }

        /// <summary>
        /// Removes the DSP from the mixer it is attached to, if any.
        /// </summary>
        internal void DetachOutput()
        {
            int mixerHandle = _mixerHandle;
            int dspHandle = _dspHandle;
            if (dspHandle == 0)
            {
                _callback = null;
                return;
            }

            // ChannelRemoveDSP reports Errors.Handle if the mixer has already been freed. Unity does
            // not guarantee the destruction order of components sharing a GameObject, so the owner of
            // this DSP may be torn down after the mixer; a stale handle is possible, not an error.
            if (!Bass.ChannelRemoveDSP(mixerHandle, dspHandle) && Bass.LastError != Errors.Handle)
            {
                // Keep the handles. The DSP is still attached to a live mixer, so forgetting it here
                // would orphan it and let a later attach add a second copy to the same mixer.
                YargLogger.LogFormatError("Failed to remove {0} from mixer {1}: {2}",
                    EFFECT_NAME, mixerHandle, Bass.LastError);
                return;
            }

            _mixerHandle = 0;
            _dspHandle = 0;

            // Released only once BASS can no longer invoke the callback.
            _callback = null;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DetachOutput();

            var callback = Disposed;
            Disposed = null;
            callback?.Invoke(this);
        }
    }
}
