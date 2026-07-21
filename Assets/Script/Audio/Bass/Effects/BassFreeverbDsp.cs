using System;
using System.Threading;
using ManagedBass;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    /// Attaches a managed Freeverb processor to a BASS channel.
    /// </summary>
    internal sealed class BassFreeverbDsp : IDisposable
    {
#nullable enable
        public static BassFreeverbDsp? Create(int streamHandle, float dryMix, float wetMix,
            float roomSize, float damp, float width = 1, int priority = 0)
#nullable disable
        {
            var info = Bass.ChannelGetInfo(streamHandle);
            if (info.Frequency <= 0 || info.Channels <= 0)
            {
                YargLogger.LogFormatError("Failed to query stream format for managed Freeverb: {0}",
                    Bass.LastError);
                return null;
            }

            var dsp = new BassFreeverbDsp(streamHandle,
                new FreeverbProcessor(info.Frequency, info.Channels,
                    dryMix, wetMix, roomSize, damp, width));
            dsp._dspHandle = Bass.ChannelSetDSP(streamHandle, dsp._callback, IntPtr.Zero, priority);
            if (dsp._dspHandle == 0)
            {
                YargLogger.LogFormatError("Failed to attach managed Freeverb DSP: {0}", Bass.LastError);
                return null;
            }
            return dsp;
        }

        private readonly int _streamHandle;
        private readonly FreeverbProcessor _processor;
        private readonly DSPProcedure _callback;

        private int _dspHandle;
        private int _requestedReset;
        private int _completedReset;
        private bool _disposed;

        private BassFreeverbDsp(int streamHandle, FreeverbProcessor processor)
        {
            _streamHandle = streamHandle;
            _processor = processor;
            _callback = ProcessAudio;
        }

        /// <summary>
        /// Requests a delay-line reset on the BASS audio thread.
        /// </summary>
        public void RequestReset()
        {
            Interlocked.Increment(ref _requestedReset);
        }


        private unsafe void ProcessAudio(int handle, int channel, IntPtr buffer, int length, IntPtr user)
        {
            int requestedReset = Volatile.Read(ref _requestedReset);
            if (requestedReset != _completedReset)
            {
                _processor.Reset();
                _completedReset = requestedReset;
            }


            _processor.Process((float*) buffer, length / sizeof(float));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_dspHandle != 0 && !Bass.ChannelRemoveDSP(_streamHandle, _dspHandle))
            {
                YargLogger.LogFormatError("Failed to remove managed Freeverb DSP: {0}", Bass.LastError);
            }
            _dspHandle = 0;
            _disposed = true;
        }
    }
}
