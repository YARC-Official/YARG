#nullable enable
using System;
using ManagedBass;

namespace YARG.Audio.BASS.Asio
{
    /// <summary>
    ///     Represents a single hardware input channel on an ASIO device. Incoming audio pushed by the ASIO
    ///     driver is fed into a BASS push stream so it can be analyzed and monitored in real time.
    /// </summary>
    internal sealed class BassAsioInput : IBassMicSampleSource
    {
        private bool           _claimed;
        private BassMicSignal? _signal;

        private BassAsioInput(string driverId, string driverName, int channelIndex, int sampleRate, int rootHandle)
        {
            DriverId = driverId;
            DriverName = driverName;
            ChannelIndex = channelIndex;
            SampleRate = sampleRate;
            RootHandle = rootHandle;
        }

        public string DriverId     { get; }
        public string DriverName   { get; }
        public int    ChannelIndex { get; }

        internal int RootHandle { get; }

        internal bool IsActivated { get; private set; }
        public   int  SampleRate  { get; }

        public bool IsValid => _claimed && _signal?.IsValid == true;

        public int Read(Span<float> buffer) => _claimed ? _signal?.Read(buffer) ?? -1 : -1;

        public int GetBacklogBytes() => _claimed ? _signal?.GetBacklogBytes() ?? -1 : -1;

        public bool ResetToLive() => _claimed && _signal?.ResetToLive() == true;

        internal void MarkActivated() => IsActivated = true;

        public static BassAsioInput? Create(string driverId, string driverName, int channelIndex, int sampleRate)
        {
            int rootHandle = Bass.CreateStream(sampleRate, 1, BassFlags.Float | BassFlags.Decode,
                StreamProcedureType.Push);
            return rootHandle == 0
                ? null
                : new BassAsioInput(driverId, driverName, channelIndex, sampleRate, rootHandle);
        }

        internal bool Attach(BassAudioRouter router)
        {
            if (_signal != null)
            {
                return true;
            }

            string name = $"{BassAsioOutput.DEVICE_PREFIX}{DriverName} - Channel {ChannelIndex + 1}";
            _signal = BassMicSignal.Create(RootHandle, null, SampleRate, name, router, 0, false);
            return _signal != null;
        }

        internal bool Claim()
        {
            if (_signal == null || _claimed)
            {
                return false;
            }

            _claimed = true;
            return true;
        }

        internal bool ResetMonitor() => _claimed && _signal?.ResetMonitor() == true;

        internal bool EnableMonitoring(double volume)
        {
            if (!_claimed || _signal == null)
            {
                return false;
            }

            _signal.SetMonitoringLevel(volume);
            return true;
        }

        internal void Release()
        {
            _signal?.SetMonitoringLevel(0);
            _claimed = false;
        }

        internal void FreeNativeStreams()
        {
            Release();
            _signal?.Dispose();
            _signal = null;
            Bass.StreamFree(RootHandle);
        }
    }
}