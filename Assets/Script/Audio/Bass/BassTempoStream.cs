#nullable enable
using System;
using ManagedBass;
using ManagedBass.Fx;
using YARG.Core.Logging;
using YARG.Settings;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     Wraps a BASS_FX tempo stream to dynamically adjust playback speed and frequency (whammy bar pitch bends
    ///     or practice speed modifications) without recreating or reopening audio streams.
    /// </summary>
    internal abstract class BassTempoStream : IDisposable
    {
        internal abstract int Handle { get; }
        internal virtual double CommandDelay => 0;

        internal static BassTempoStream? Create(int inputHandle, TempoEngine engine)
        {
            if (engine == TempoEngine.YargStretch)
            {
                var stretch = BassStretchStream.Create(inputHandle);
                return stretch == null ? null : new YargStretchTempoStream(stretch);
            }

            int handle = BassX.Require(
                BassFx.TempoCreate(inputHandle, BassFlags.Decode),
                "create tempo stream");
            return new BassFxTempoStream(handle);
        }

        internal abstract void SetSpeed(float speed, bool shiftPitch);
        internal abstract void ResetPosition();
        internal virtual void Prime() { }
        internal abstract bool TryGetPositionSeconds(long positionBytes, out double position);

        internal void SetDevice(int deviceId) => BassX.Check(
            Bass.ChannelSetDevice(Handle, deviceId),
            $"move tempo stream {Handle} to device {deviceId}");

        public abstract void Dispose();

        private sealed class BassFxTempoStream : BassTempoStream
        {
            private bool _disposed;
            private readonly int _handle;

            public BassFxTempoStream(int handle)
            {
                _handle = handle;
            }

            internal override int Handle => _handle;

            internal override void SetSpeed(float speed, bool shiftPitch)
            {
                float relativeSpeed = (speed * 100) - 100;
                if (!Bass.ChannelSetAttribute(Handle, ChannelAttribute.Tempo, relativeSpeed))
                {
                    YargLogger.LogFormatError("Failed to set channel speed: {0}!", Bass.LastError);
                }

                if (GlobalAudioHandler.IsChipmunkSpeedup && shiftPitch)
                {
                    SetChipmunking(speed);
                }
            }

            internal override void ResetPosition()
            {
                BassX.Check(Bass.ChannelSetPosition(Handle, 0), "reset tempo stream position");
            }

            internal override void Prime()
            {
                float[] buffer = new float[4096];
                Bass.ChannelGetData(Handle, buffer, (buffer.Length * sizeof(float)) | (int) DataFlags.Float);
                ResetPosition();
            }

            internal override bool TryGetPositionSeconds(long positionBytes, out double position)
            {
                position = Bass.ChannelBytes2Seconds(Handle, positionBytes);
                if (position >= 0)
                {
                    return true;
                }

                YargLogger.LogFormatError("Failed to convert bytes to seconds: {0}!", Bass.LastError);
                return false;
            }

            public override void Dispose()
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

        private sealed class YargStretchTempoStream : BassTempoStream
        {
            private bool _disposed;
            private readonly BassStretchStream _stretch;

            public YargStretchTempoStream(BassStretchStream stretch)
            {
                _stretch = stretch;
            }

            internal override int Handle => _stretch.StreamHandle;
            internal override double CommandDelay => _stretch.CommandDelay;

            internal override void SetSpeed(float speed, bool shiftPitch)
            {
                float pitch = 1f;
                if (GlobalAudioHandler.IsChipmunkSpeedup && shiftPitch)
                {
                    pitch = Math.Clamp(speed, 1f / 32f, 32f);
                }

                _stretch.SetSpeed(speed, pitch);
            }

            internal override void ResetPosition()
            {
                BassX.Check(Bass.ChannelSetPosition(Handle, 0), "reset tempo stream position");
                _stretch.Flush();
            }

            internal override bool TryGetPositionSeconds(long positionBytes, out double position) =>
                _stretch.TryGetPositionSeconds(positionBytes, out position);

            public override void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _stretch.Dispose();
            }
        }
    }
}
