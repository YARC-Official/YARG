#nullable enable
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using YARG.Audio.BASS.Native;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    public enum ReadAheadState : uint
    {
        Created,
        Empty,
        Prefilling,
        Ready,
        Running,
        Starved,
        SourceFailed,
        Stopping,
        Stopped,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ReadAheadConfig
    {
        public uint Size;
        public int  BassDeviceId;
        public uint SourceMixer;
        public uint SampleRate;
        public uint Channels;
        public uint MinimumBlockFrames;
        public uint BufferMilliseconds;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ReadAheadStats
    {
        public uint           Size;
        public ReadAheadState State;
        public int            LastError;
        public uint           TargetFrames;
        public uint           QueuedFrames;
        public uint           MinimumQueuedFrames;
        public ulong          ProducedFrames;
        public ulong          ConsumedFrames;
        public ulong          RequestedFrames;
        public ulong          UnderrunFrames;
        public ulong          UnderrunEvents;
        public ulong          MaximumRenderNanoseconds;
        public ulong          PositionOutputFrame;
        public uint           CallbackFrames;
        public uint           CallbackElapsedFrames;
        public long           CallbackCorrectionFrames;
        public long           CallbackClockOffsetFrames;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ReadAheadPositionSnapshot
    {
        public uint Size;
        public uint TotalDelayFrames;
        public long HeardPosition;
        public long DecodePosition;
    }

    /// <summary>
    ///     Wraps a native C++ read-ahead stream that buffers decoded audio frames in a ring buffer,
    ///     preventing audio underruns and glitches during low-latency ASIO callbacks.
    /// </summary>
    internal sealed class BassReadAheadStream : SafeHandleZeroOrMinusOneIsInvalid
    {
        private BassReadAheadStream() : base(true)
        {
        }

        public int StreamHandle { get; private set; }

        public static BassReadAheadStream? Create(int bassDeviceId, int sourceMixer, int sampleRate, int channels,
            int minimumBlockFrames, int bufferMilliseconds, bool useIndependentClock = false)
        {
            if (sourceMixer == 0 || sampleRate <= 0 || channels <= 0 || minimumBlockFrames <= 0 ||
                bufferMilliseconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceMixer));
            }

            try
            {
                if (!YargAudioNative.CheckAbi())
                {
                    return null;
                }

                var config = new ReadAheadConfig
                {
                    Size = (uint) Marshal.SizeOf<ReadAheadConfig>(),
                    BassDeviceId = bassDeviceId,
                    SourceMixer = unchecked((uint) sourceMixer),
                    SampleRate = checked((uint) sampleRate),
                    Channels = checked((uint) channels),
                    MinimumBlockFrames = checked((uint) minimumBlockFrames),
                    BufferMilliseconds = checked((uint) bufferMilliseconds),
                };

                int result = Native.Create(in config, out var stream, out uint streamHandle, out int bassError);
                if (result != 0 || stream == null || stream.IsInvalid || streamHandle == 0)
                {
                    stream?.Dispose();
                    YargLogger.LogFormatError("Failed to create native read-ahead stream: result={0}, BASS={1}", result,
                        bassError);
                    return null;
                }

                if (Native.SetCallbackClock(stream, useIndependentClock ? 1 : 0) != 0)
                {
                    stream.Dispose();
                    return null;
                }

                stream.StreamHandle = unchecked((int) streamHandle);
                return stream;
            }
            catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException
                or BadImageFormatException)
            {
                YargLogger.LogException(exception, "Failed to load YargAudio native plugin");
                return null;
            }
        }

        public bool Prefill(int timeoutMilliseconds)
        {
            ThrowIfDisposed();
            return Native.Prefill(this, checked((uint) timeoutMilliseconds)) == 0;
        }

        public bool Flush()
        {
            ThrowIfDisposed();
            return Native.Flush(this) == 0;
        }

        public bool SetBufferLength(int bufferMilliseconds)
        {
            ThrowIfDisposed();
            return Native.SetBufferLength(this, checked((uint) bufferMilliseconds)) == 0;
        }

        public long GetSourcePosition(int sourceHandle, int endpointDelayFrames)
        {
            return TryGetPositionSnapshot(sourceHandle, endpointDelayFrames, out var snapshot)
                ? snapshot.HeardPosition
                : -1;
        }

        public bool TryGetPositionSnapshot(int sourceHandle, int endpointDelayFrames,
            out ReadAheadPositionSnapshot snapshot)
        {
            ThrowIfDisposed();
            snapshot = new ReadAheadPositionSnapshot
            {
                Size = (uint) Marshal.SizeOf<ReadAheadPositionSnapshot>(),
            };
            return Native.GetPositionSnapshot(this, unchecked((uint) sourceHandle),
                checked((uint) Math.Max(0, endpointDelayFrames)), ref snapshot) == 0;
        }

        public ReadAheadStats GetStats()
        {
            ThrowIfDisposed();
            var stats = new ReadAheadStats
            {
                Size = (uint) Marshal.SizeOf<ReadAheadStats>(),
            };
            int result = Native.GetStats(this, ref stats);
            if (result != 0)
            {
                stats.LastError = result;
            }

            return stats;
        }

        protected override bool ReleaseHandle()
        {
            Native.Destroy(handle, out _);
            StreamHandle = 0;
            return true;
        }

        private void ThrowIfDisposed()
        {
            if (IsClosed || IsInvalid)
            {
                throw new ObjectDisposedException(nameof(BassReadAheadStream));
            }
        }

        private static class Native
        {
            private const string LIBRARY = "yarg_audio";

            [DllImport(LIBRARY, EntryPoint = "yarg_read_ahead_stream_create",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int Create(in ReadAheadConfig config, out BassReadAheadStream stream,
                out uint streamHandle, out int bassError);

            [DllImport(LIBRARY, EntryPoint = "yarg_read_ahead_stream_prefill",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int Prefill(BassReadAheadStream stream, uint timeoutMilliseconds);

            [DllImport(LIBRARY, EntryPoint = "yarg_read_ahead_stream_set_callback_clock",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int SetCallbackClock(BassReadAheadStream stream, int enabled);

            [DllImport(LIBRARY, EntryPoint = "yarg_read_ahead_stream_flush",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int Flush(BassReadAheadStream stream);

            [DllImport(LIBRARY, EntryPoint = "yarg_read_ahead_stream_set_buffer_length",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int SetBufferLength(BassReadAheadStream stream, uint bufferMilliseconds);

            [DllImport(LIBRARY, EntryPoint = "yarg_read_ahead_stream_get_source_position",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern long GetSourcePosition(BassReadAheadStream stream, uint source,
                uint endpointDelayFrames, out int error);

            [DllImport(LIBRARY, EntryPoint = "yarg_read_ahead_stream_get_position_snapshot",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int GetPositionSnapshot(BassReadAheadStream stream, uint source,
                uint endpointDelayFrames, ref ReadAheadPositionSnapshot snapshot);

            [DllImport(LIBRARY, EntryPoint = "yarg_read_ahead_stream_get_stats",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int GetStats(BassReadAheadStream stream, ref ReadAheadStats stats);

            [DllImport(LIBRARY, EntryPoint = "yarg_read_ahead_stream_destroy",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int Destroy(IntPtr stream, out int bassError);
        }
    }
}
