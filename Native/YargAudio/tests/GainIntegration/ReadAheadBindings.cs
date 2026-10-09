using System;
using System.Runtime.InteropServices;
using YARG.Audio.BASS;

namespace YARG.Audio.BASS.Native
{
    internal static class YargAudioNative
    {
        internal static bool CheckAbi() => GetAbiVersion() == 27;

        [DllImport("yarg_audio", EntryPoint = "yarg_audio_get_abi_version", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint GetAbiVersion();
    }

    internal static class YargAudioBindings
    {
        internal static ReadAheadConfig LastConfig { get; private set; }

        internal static int ReadAheadStreamCreate(in ReadAheadConfig config, out BassReadAheadStream stream,
            out int streamHandle, out int bassError)
        {
            LastConfig = config;
            return Create(in config, out stream, out streamHandle, out bassError);
        }

        [DllImport("yarg_audio", EntryPoint = "yarg_read_ahead_stream_create", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Create(in ReadAheadConfig config, out BassReadAheadStream stream,
            out int streamHandle, out int bassError);

        [DllImport("yarg_audio", EntryPoint = "yarg_read_ahead_stream_set_callback_clock", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ReadAheadStreamSetCallbackClock(BassReadAheadStream stream, int enabled);

        [DllImport("yarg_audio", EntryPoint = "yarg_read_ahead_stream_prefill", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ReadAheadStreamPrefill(BassReadAheadStream stream, int timeoutMilliseconds);

        [DllImport("yarg_audio", EntryPoint = "yarg_read_ahead_stream_flush", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ReadAheadStreamFlush(BassReadAheadStream stream);

        [DllImport("yarg_audio", EntryPoint = "yarg_read_ahead_stream_set_buffer_length", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ReadAheadStreamSetBufferLength(BassReadAheadStream stream, int milliseconds);

        [DllImport("yarg_audio", EntryPoint = "yarg_read_ahead_stream_get_position_snapshot", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ReadAheadStreamGetPositionSnapshot(BassReadAheadStream stream, int source,
            int endpointDelayFrames, ref ReadAheadPositionSnapshot snapshot);

        [DllImport("yarg_audio", EntryPoint = "yarg_read_ahead_stream_get_stats", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ReadAheadStreamGetStats(BassReadAheadStream stream, ref ReadAheadStats stats);

        [DllImport("yarg_audio", EntryPoint = "yarg_read_ahead_stream_destroy", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ReadAheadStreamDestroy(IntPtr stream, out int bassError);
    }
}

namespace YARG.Core.Logging
{
    internal static class YargLogger
    {
        internal static void LogFormatError(string format, params object[] arguments) =>
            throw new InvalidOperationException(string.Format(format, arguments));

        internal static void LogException(Exception exception, string message) =>
            throw new InvalidOperationException(message, exception);
    }
}
