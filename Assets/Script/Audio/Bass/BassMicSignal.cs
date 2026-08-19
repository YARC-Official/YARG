#nullable enable
using System;
using ManagedBass;
using ManagedBass.Fx;
using ManagedBass.Mix;
using YARG.Audio.BASS.Effects;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     Processes an incoming microphone signal, splitting it into an equalized stream for pitch analysis
    ///     and an effects stream (with reverb and volume control) for live vocal monitoring.
    /// </summary>
    internal sealed class BassMicSignal : IBassMicSampleSource, IDisposable
    {
        private static readonly PeakEQParameters LowAnalysisEq = new()
        {
            fBandwidth = 2.5f,
            fCenter = 20f,
            fGain = -10f,
        };

        private static readonly PeakEQParameters HighAnalysisEq = new()
        {
            fBandwidth = 2.5f,
            fCenter = 10_000f,
            fGain = -10f,
        };
        private readonly string          _name;
        private readonly BassFreeverbDsp _reverb;

        private readonly object       _streamLock = new();
        private          int          _analysisHandle;
        private          BassMonitor? _monitor;
        private          int          _monitorHandle;

        private BassMicSignal(string name, int sampleRate, int monitorHandle, int analysisHandle,
            BassFreeverbDsp reverb)
        {
            _name = name;
            SampleRate = sampleRate;
            _monitorHandle = monitorHandle;
            _analysisHandle = analysisHandle;
            _reverb = reverb;
        }

        public int SampleRate { get; }

        public bool IsValid
        {
            get
            {
                lock (_streamLock)
                {
                    return _analysisHandle != 0;
                }
            }
        }

        public unsafe int Read(Span<float> destination)
        {
            lock (_streamLock)
            {
                if (_analysisHandle == 0)
                {
                    return -1;
                }

                fixed (float* pointer = destination)
                {
                    int bytesRead = Bass.ChannelGetData(_analysisHandle, (IntPtr) pointer,
                        checked(destination.Length * sizeof(float)));
                    return bytesRead < 0 ? -1 : bytesRead / sizeof(float);
                }
            }
        }

        public int GetBacklogBytes()
        {
            lock (_streamLock)
            {
                return _analysisHandle == 0 ? -1 : BassMix.SplitStreamGetAvailable(_analysisHandle);
            }
        }

        public bool ResetToLive()
        {
            lock (_streamLock)
            {
                if (_analysisHandle != 0 && BassMix.SplitStreamReset(_analysisHandle, 0))
                {
                    return true;
                }

                YargLogger.LogFormatError("Failed to reset mic '{0}' analysis split: {1}", _name, Bass.LastError);
                return false;
            }
        }

        public void Dispose()
        {
            var monitor = _monitor;
            _monitor = null;
            monitor?.Dispose();

            lock (_streamLock)
            {
                _reverb.Dispose();
                FreeStream(ref _analysisHandle);
                FreeStream(ref _monitorHandle);
            }
        }

        public static BassMicSignal? Create(int sourceHandle, int[]? channelMap, int sampleRate, string name,
            BassAudioRouter router, double monitoringLevel, bool applyAnalysisEq, Action? attached = null,
            Action? detached = null)
        {
            int monitorHandle = 0;
            int analysisHandle = 0;
            BassFreeverbDsp? reverb = null;

            try
            {
                monitorHandle = BassMix.CreateSplitStream(sourceHandle, BassFlags.Decode | BassFlags.SplitPosition,
                    channelMap);
                if (monitorHandle == 0)
                {
                    YargLogger.LogFormatError("Failed to create mic '{0}' monitor split: {1}", name, Bass.LastError);
                    return null;
                }

                analysisHandle = BassMix.CreateSplitStream(sourceHandle,
                    BassFlags.Decode | BassFlags.SplitPosition | BassFlags.SplitSlave, channelMap);
                if (analysisHandle == 0)
                {
                    YargLogger.LogFormatError("Failed to create mic '{0}' analysis split: {1}", name, Bass.LastError);
                    return null;
                }

                if (applyAnalysisEq && (BassHelpers.AddEqToChannel(analysisHandle, LowAnalysisEq) == 0 ||
                    BassHelpers.AddEqToChannel(analysisHandle, HighAnalysisEq) == 0))
                {
                    YargLogger.LogFormatError("Failed to add EQ to mic '{0}' analysis split: {1}", name,
                        Bass.LastError);
                    return null;
                }

                reverb = BassFreeverbDsp.Create(monitorHandle, 0.3f, 1f, 0.4f, 0.7f, 0f, 1);
                if (reverb == null)
                {
                    YargLogger.LogError($"Failed to add reverb to mic '{name}' monitor split");
                    return null;
                }

                var signal = new BassMicSignal(name, sampleRate, monitorHandle, analysisHandle, reverb);
                monitorHandle = 0;
                analysisHandle = 0;
                reverb = null;

                var source = new BassMonitorSource(signal._monitorHandle, signal._reverb.RequestReset);
                signal._monitor = router.RegisterMonitor(source, monitoringLevel, attached, detached);
                if (signal._monitor != null)
                {
                    return signal;
                }

                YargLogger.LogError($"Failed to register mic '{name}' monitor with active audio output");
                signal.Dispose();
                return null;
            }
            finally
            {
                reverb?.Dispose();
                FreeStream(ref analysisHandle);
                FreeStream(ref monitorHandle);
            }
        }

        public bool ResetMonitor()
        {
            lock (_streamLock)
            {
                if (_monitorHandle == 0 || !BassMix.SplitStreamReset(_monitorHandle, 0))
                {
                    YargLogger.LogFormatError("Failed to reset mic '{0}' monitor split: {1}", _name, Bass.LastError);
                    return false;
                }

                _reverb.RequestReset();
                return true;
            }
        }

        public void SetMonitoringLevel(double volume) => _monitor?.SetVolume(volume);

        private static void FreeStream(ref int handle)
        {
            if (handle == 0)
            {
                return;
            }

            if (!Bass.StreamFree(handle))
            {
                YargLogger.LogFormatError("Failed to free microphone stream {0}: {1}", handle, Bass.LastError);
            }

            handle = 0;
        }
    }
}