#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Win32.SafeHandles;
using YARG.Audio.BASS.Native;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    internal sealed class BassStretchStream : SafeHandleZeroOrMinusOneIsInvalid
    {
        private BassStretchStream() : base(true)
        {
        }

        internal int StreamHandle { get; private set; }
        internal double CommandDelay { get; private set; }

        private static readonly HashSet<BassStretchStream> _liveStreams = new();
        private static readonly object _liveStreamsLock = new();
        private static float _grains = 1f;

        internal static void SetAllGrains(float strength)
        {
            strength = Math.Clamp(strength, 0f, 1f);
            lock (_liveStreamsLock)
            {
                _grains = strength;
                foreach (var stream in _liveStreams)
                {
                    if (YargAudioBindings.StretchStreamSetGrains(stream, strength) != 0)
                    {
                        YargLogger.LogFormatError("Failed to set YargStretch grains: {0}", strength);
                    }
                }
            }
        }

        internal static BassStretchStream? Create(int source)
        {
            try
            {
                if (!YargAudioNative.CheckAbi())
                {
                    return null;
                }

                int result = YargAudioBindings.StretchStreamCreate(source,
                    out var stream, out int streamHandle, out int bassError);
                if (result != 0)
                {
                    stream?.Dispose();
                    YargLogger.LogFormatError("Failed to create YargStretch stream: result={0}, BASS={1}",
                        result, bassError);
                    return null;
                }

                stream.StreamHandle = streamHandle;
                result = YargAudioBindings.StretchStreamGetLatency(stream, out double seconds);
                if (result != 0)
                {
                    stream.Dispose();
                    return null;
                }

                stream.CommandDelay = seconds;
                lock (_liveStreamsLock)
                {
                    _liveStreams.Add(stream);
                }
                if (YargAudioBindings.StretchStreamSetGrains(stream, _grains) != 0)
                {
                    YargLogger.LogFormatError("Failed to set YargStretch grains: {0}", _grains);
                }
                return stream;
            }
            catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException
                or BadImageFormatException)
            {
                YargLogger.LogException(exception, "Failed to load YargStretch");
                return null;
            }
        }

        internal void SetSpeed(float speed, float pitch)
        {
            int result = YargAudioBindings.StretchStreamSetSpeed(this, speed, pitch);
            if (result != 0)
            {
                YargLogger.LogFormatError("Failed to set YargStretch speed: {0}", result);
            }
        }

        internal void Flush()
        {
            int result = YargAudioBindings.StretchStreamFlush(this);
            if (result != 0)
            {
                YargLogger.LogFormatError("Failed to flush YargStretch stream: {0}", result);
            }
        }

        internal bool TryGetPositionSeconds(long bytes, out double seconds) =>
            YargAudioBindings.StretchStreamGetPosition(this, bytes, out seconds) == 0;

        protected override bool ReleaseHandle()
        {
            lock (_liveStreamsLock)
            {
                _liveStreams.Remove(this);
            }
            YargAudioBindings.StretchStreamDestroy(handle);
            StreamHandle = 0;
            return true;
        }
    }
}
