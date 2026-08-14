#nullable enable
using System;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Input;
using YARG.Settings;

namespace YARG.Audio.BASS.Asio
{
    /// <summary>
    ///     Exposes a single ASIO input channel as a player microphone in the game, managing its pitch detection
    ///     analysis and low-latency vocal monitoring streams.
    /// </summary>
    internal sealed class BassAsioMicDevice : MicDevice
    {
        private readonly string           _baseName;
        private readonly BassAsioMics     _owner;
        private          BassMicAnalyzer? _analyzer;
        private          BassAsioInput?   _input;
        private          float            _monitoringLevel;

        internal BassAsioMicDevice(BassAsioMics owner, string driverId, BassAsioInput input, InputDeviceInfo info) :
            base(info.DisplayName)
        {
            _owner = owner;
            _baseName = info.Name;
            DriverId = driverId;
            ChannelIndex = info.Channel;
            _monitoringLevel = SettingsManager.Settings.VocalMonitoring.Value;
            Resume(input);
        }

        internal string DriverId { get; }

        internal int ChannelIndex { get; }

        public override int Reset()
        {
            bool analysisReset = _analyzer?.Reset() == true;
            bool monitorReset = _input?.ResetMonitor() == true;
            return analysisReset && monitorReset ? 0 : -1;
        }

        public override bool DequeueOutputFrame(out MicOutputFrame frame)
        {
            if (_analyzer != null)
            {
                return _analyzer.DequeueOutputFrame(out frame);
            }

            frame = default;
            return false;
        }

        public override void ClearOutputQueue() => _analyzer?.ClearOutputQueue();

        public override void SetMonitoringLevel(float volume)
        {
            _monitoringLevel = volume;
            if (_input?.EnableMonitoring(volume) == false)
            {
                YargLogger.LogWarning($"Failed to enable monitoring for ASIO microphone '{DisplayName}'");
            }
        }

        public override SerializedMic Serialize() => new(_baseName, ChannelIndex);

        protected override void DisposeUnmanagedResources()
        {
            Suspend();
            _owner.Release(this);
        }

        internal bool Matches(string driverId, int channelIndex) =>
            string.Equals(DriverId, driverId, StringComparison.OrdinalIgnoreCase) && ChannelIndex == channelIndex;

        internal void Suspend()
        {
            _analyzer?.StopAndJoin();
            _analyzer = null;

            _input?.Release();
            _input = null;
        }

        internal void Resume(BassAsioInput input)
        {
            Suspend();
            _input = input;
            try
            {
                _analyzer = new BassMicAnalyzer(input, () => IsRecordingOutput, () => InputManager.CurrentInputTime);
                if (!input.EnableMonitoring(_monitoringLevel))
                {
                    YargLogger.LogWarning($"Failed to restore monitoring for ASIO microphone '{DisplayName}'");
                }
            }
            catch
            {
                Suspend();
                throw;
            }
        }
    }
}