#nullable enable
using System;
using ManagedBass;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     A BASS push stream created with <see cref="BassFlags.Decode" /> so it can be attached to the
    ///     output mixer like any other decode source. The application feeds it with decoded samples via
    ///     <see cref="Push_Internal" />; BASS resamples, downmixes, and routes it to the active output device.
    /// </summary>
    internal sealed class BassPushStream : PushStream
    {
        private BassPushStream(int handle, int sampleRate, int channelCount) : base(handle, sampleRate, channelCount)
        {
        }

        /// <summary>
        ///     Raised after the underlying BASS handle has been freed, so owners can unregister it.
        /// </summary>
        internal event Action<BassPushStream>? Disposed;

        /// <summary>
        ///     Creates a push stream that decodes into the output mixer.
        /// </summary>
        /// <returns>The new stream, or null if the BASS handle could not be created.</returns>
        internal static BassPushStream? Create(int sampleRate, int channelCount)
        {
            int handle = Bass.CreateStream(sampleRate, channelCount,
                BassFlags.Float | BassFlags.Decode, StreamProcedureType.Push);
            if (handle == 0)
            {
                YargLogger.LogFormatError("Failed to create push stream ({0} Hz, {1} channels): {2}",
                    sampleRate, channelCount, Bass.LastError);
                return null;
            }

            return new BassPushStream(handle, sampleRate, channelCount);
        }

        protected override void Push_Internal(IntPtr bufferPtr, int byteLength)
        {
            if (byteLength <= 0)
            {
                return;
            }

            if (Bass.StreamPutData(Handle, bufferPtr, byteLength) < 0)
            {
                YargLogger.LogFormatError("Failed to push data to stream {0}: {1}", Handle, Bass.LastError);
            }
        }

        protected override void SetVolume_Internal(double volume)
        {
            if (!Bass.ChannelSetAttribute(Handle, ChannelAttribute.Volume, volume))
            {
                YargLogger.LogFormatError("Failed to set push stream volume: {0}", Bass.LastError);
            }
        }

        protected override void Clear_Internal()
        {
            if (!Bass.ChannelSetPosition(Handle, 0, PositionFlags.Bytes))
            {
                YargLogger.LogFormatError("Failed to clear push stream queue: {0}", Bass.LastError);
            }
        }

        protected override void DisposeUnmanagedResources()
        {
            if (!Bass.StreamFree(Handle) && Bass.LastError != Errors.Handle)
            {
                YargLogger.LogFormatError("Failed to free push stream {0}: {1}", Handle, Bass.LastError);
            }

            Disposed?.Invoke(this);
        }
    }
}
