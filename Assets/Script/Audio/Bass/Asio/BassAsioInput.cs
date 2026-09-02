#nullable enable
using System;
using ManagedBass;
using YARG.Core.Audio;

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

        private BassAsioInput(string driverId, string driverName, int channelIndex, int sampleRate, int bufferFrames,
            int rootHandle)
        {
            DriverId = driverId;
            DriverName = driverName;
            ChannelIndex = channelIndex;
            SampleRate = sampleRate;
            BufferFrames = bufferFrames;
            RootHandle = rootHandle;
        }

        public string DriverId     { get; }
        public string DriverName   { get; }
        public int    ChannelIndex { get; }

        internal int RootHandle { get; }

        internal bool IsActivated { get; private set; }
        public   int  SampleRate   { get; }
        public   int  BufferFrames { get; }

        public bool IsValid => _claimed && _signal?.IsValid == true;

        public int Read(Span<float> buffer) => _claimed ? _signal?.Read(buffer) ?? -1 : -1;

        public int GetBacklogBytes() => _claimed ? _signal?.GetBacklogBytes() ?? -1 : -1;

        internal bool TryCreateRecordingChannel(bool withEffects, out int handle)
        {
            if (!_claimed || _signal == null)
            {
                handle = 0;
                return false;
            }

            return _signal.TryCreateRecordingChannel(withEffects, out handle);
        }

        internal void ReleaseRecordingChannel(int handle) => _signal?.ReleaseRecordingChannel(handle);

        public bool ResetToLive() => _claimed && _signal?.ResetToLive() == true;

        internal bool Reset()
        {
            if (!_claimed || _signal == null)
            {
                return false;
            }

            Bass.ChannelSetPosition(RootHandle, 0, PositionFlags.Bytes);
            bool monitorReset = _signal.ResetMonitor();
            bool analysisReset = _signal.ResetAnalysis();
            return monitorReset && analysisReset;
        }

        public MicBufferInfo? GetBufferInfo()
        {
            if (!_claimed)
            {
                return null;
            }

            int bufferMs = SampleRate > 0 ? (int) Math.Round(BufferFrames * 1000.0 / SampleRate) : 0;
            int waitingBytes = _signal?.GetBacklogBytes() ?? 0;

            return new MicBufferInfo(
                bufferFrames: BufferFrames,
                bufferMilliseconds: bufferMs,
                sampleRate: SampleRate,
                channels: 1,
                isAsio: true,
                cushionMilliseconds: 0,
                waitingBytes: Math.Max(0, waitingBytes));
        }

        internal void MarkActivated() => IsActivated = true;

        public static BassAsioInput? Create(string driverId, string driverName, int channelIndex, int sampleRate,
            int bufferFrames)
        {
            int rootHandle = Bass.CreateStream(sampleRate, 1, BassFlags.Float | BassFlags.Decode,
                StreamProcedureType.Push);
            return rootHandle == 0
                ? null
                : new BassAsioInput(driverId, driverName, channelIndex, sampleRate, bufferFrames, rootHandle);
        }

        internal bool Attach(BassAudioRouter router)
        {
            if (_signal != null)
            {
                return true;
            }

            string name = $"{BassAsioOutput.DEVICE_PREFIX}{DriverName} - Channel {ChannelIndex + 1}";
            _signal = BassMicSignal.Create(
                sourceHandle: RootHandle,
                channelMap: null,
                sampleRate: SampleRate,
                name: name,
                router: router,
                monitoringLevel: 0,
                applyAnalysisEq: true);
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

        internal bool SetReverbLevel(float wet)
        {
            if (!_claimed || _signal == null)
            {
                return false;
            }

            _signal.SetReverbLevel(wet);
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
