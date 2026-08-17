#nullable enable
using System;
using ManagedBass;
using ManagedBass.Fx;
using ManagedBass.Mix;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     Wraps a BASS_FX tempo stream to dynamically adjust playback speed and frequency (whammy bar pitch bends
    ///     or practice speed modifications) without recreating or reopening audio streams.
    /// </summary>
    internal sealed class BassTempoStream : IDisposable
    {
        private bool _disposed;

        private BassTempoStream(int handle)
        {
            Handle = handle;
        }

        internal int Handle { get; }

        internal static BassTempoStream Create(int inputHandle)
        {
            int handle = BassX.Require(
                BassFx.TempoCreate(inputHandle, BassFlags.Decode),
                "create tempo stream");
            return new BassTempoStream(handle);
        }

        internal void SetSpeed(float speed, bool shiftPitch)
        {
            float percentageSpeed = speed * 100;
            float relativeSpeed = percentageSpeed - 100;

            if (!Bass.ChannelSetAttribute(Handle, ChannelAttribute.Tempo, relativeSpeed))
            {
                YargLogger.LogFormatError("Failed to set channel speed: {0}!", Bass.LastError);
            }

            if (GlobalAudioHandler.IsChipmunkSpeedup && shiftPitch)
            {
                SetChipmunking(speed);
            }
        }

        internal void ResetPosition() => BassX.Check(
            Bass.ChannelSetPosition(Handle, 0),
            "reset tempo stream position");

        internal void Prime()
        {
            float[] buffer = new float[4096];
            Bass.ChannelGetData(Handle, buffer, (buffer.Length * sizeof(float)) | (int) DataFlags.Float);
            ResetPosition();
        }

        internal void SetDevice(int deviceId) => BassX.Check(
            Bass.ChannelSetDevice(Handle, deviceId),
            $"move tempo stream {Handle} to device {deviceId}");

        internal bool TryGetPositionSeconds(long positionBytes, out double position)
        {
            position = Bass.ChannelBytes2Seconds(Handle, positionBytes);
            if (position >= 0)
            {
                return true;
            }

            YargLogger.LogFormatError("Failed to convert bytes to seconds: {0}!", Bass.LastError);
            return false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            BassX.Check(Bass.StreamFree(Handle), $"free tempo stream {Handle}");
        }

        private void SetChipmunking(float speed)
        {
            double accurateSemitoneShift = 12 * Math.Log(speed, 2);
            float finalSemitoneShift = (float) Math.Clamp(accurateSemitoneShift, -60, 60);
            if (!Bass.ChannelSetAttribute(Handle, ChannelAttribute.Pitch, finalSemitoneShift))
            {
                YargLogger.LogFormatError("Failed to set channel pitch: {0}!", Bass.LastError);
            }
        }
    }
}
