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
        private readonly IMixerDspProcessor _processor;
        private readonly int                _priority;
        private readonly Func<long, double> _getSongPosition;
        private readonly Func<float>        _getSpeed;

        // Held only to pin the delegate against garbage collection while BASS holds a pointer to it.
        private DSPProcedure? _callback;

        private int  _mixerHandle;
        private int  _dspHandle;
        private bool _disposed;

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
                    "Cannot attach mixer DSP: mixer {0} must use float sample data (frequency={1}, channels={2}).",
                    mixerHandle, info.Frequency, info.Channels);
                return false;
            }

            int sampleRate = info.Frequency;
            int channels = info.Channels;

            var callback = new DSPProcedure((_, _, buffer, length, _) =>
            {
                int frames = length / (sizeof(float) * channels);
                if (frames <= 0)
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

                // The block spans fewer song seconds than real seconds when practicing below
                // normal speed, so the processor must be told both ends rather than deriving
                // the span from the sample rate alone.
                double songTimeStart = songTimeEnd - (frames / (double) sampleRate) * Math.Max(0.0001f, speed);

                unsafe
                {
                    var span = new Span<float>((void*) buffer, length / sizeof(float));
                    _processor.ProcessAudio(span, frames, channels, sampleRate, songTimeStart, songTimeEnd);
                }
            });

            int dspHandle = Bass.ChannelSetDSP(mixerHandle, callback, IntPtr.Zero, _priority);
            if (dspHandle == 0)
            {
                YargLogger.LogFormatError("Failed to attach mixer DSP to mixer {0}: {1}",
                    mixerHandle, Bass.LastError);
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
            _mixerHandle = 0;
            _dspHandle = 0;
            _callback = null;

            // The mixer is routinely freed before this runs: GameManager.OnDestroy disposes the song
            // mixer before GameplayBehaviour.OnDestroy tears down the practice manager that owns this
            // DSP. A stale handle is therefore the normal teardown path, not an error.
            if (dspHandle != 0 &&
                !Bass.ChannelRemoveDSP(mixerHandle, dspHandle) &&
                Bass.LastError != Errors.Handle)
            {
                YargLogger.LogFormatError("Failed to remove mixer DSP from mixer {0}: {1}",
                    mixerHandle, Bass.LastError);
            }
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
