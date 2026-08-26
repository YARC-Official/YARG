#nullable enable
using System;
using YARG.Core.Audio;
using YARG.Settings;

namespace YARG.Audio.BASS.Wasapi
{
    /// <summary>
    ///     Represents a single microphone input channel captured from a WASAPI device, bridging the
    ///     hardware push stream into pitch analysis and real-time vocal monitoring.
    /// </summary>
    internal sealed class BassWasapiMicSource : BassMicSourceBase
    {
        private readonly BassWasapiMicCapture _capture;
        private readonly Action               _onDisposed;
        private readonly BassMicSignal        _signal;

        public BassWasapiMicSource(BassWasapiMicCapture capture, InputDeviceInfo device,
            BassAudioRouter router, Action onDisposed)
            : base(device.Name, device.DisplayName, device.Channel)
        {
            _capture = capture;
            _onDisposed = onDisposed;

            int[]? channelMap = capture.Channels > 1
                ? new[]
                {
                    device.Channel,
                    -1,
                }
                : null;
            float monitoringLevel = SettingsManager.Settings.VocalMonitoring.Value;

            _signal = BassMicSignal.Create(
                    capture.ReadHandle,
                    channelMap,
                    capture.SampleRate,
                    device.DisplayName,
                    router,
                    monitoringLevel,
                    true,
                    null,
                    OnMonitorDetached)
                ?? throw new InvalidOperationException(
                    $"Failed to create WASAPI mic signal for '{device.DisplayName}'");
        }

        public static BassWasapiMicSource? Create(BassWasapiMicCapture capture, InputDeviceInfo device,
            BassAudioRouter router, Action onDisposed)
        {
            try
            {
                return new BassWasapiMicSource(capture, device, router, onDisposed);
            }
            catch (Exception)
            {
                return null;
            }
        }

        protected override int GetSampleRateCore() => _capture.SampleRate;

        protected override bool GetIsValidCore() => _signal.IsValid;

        protected override int ReadCore(Span<float> destination) => _signal.Read(destination);

        protected override int GetBacklogBytesCore() => _signal.GetBacklogBytes();

        protected override bool TryCreateRecordingChannelCore(bool withEffects, out int handle) =>
            _signal.TryCreateRecordingChannel(withEffects, out handle);

        protected override void ReleaseRecordingChannelCore(int handle) => _signal.ReleaseRecordingChannel(handle);

        protected override bool ResetToLiveCore() => _signal.ResetToLive();

        protected override bool SetMonitoringLevelCore(float volume)
        {
            _signal.SetMonitoringLevel(volume);
            return true;
        }

        protected override bool SetReverbLevelCore(float wet)
        {
            _signal.SetReverbLevel(wet);
            return true;
        }

        public override bool Reset()
        {
            lock (SyncRoot)
            {
                if (IsDisposed)
                {
                    return true;
                }

                bool discarded = _capture.DiscardBufferedAudio();
                bool monitorReset = _signal.ResetMonitor();
                bool analysisReset = _signal.ResetAnalysis();
                return discarded && monitorReset && analysisReset;
            }
        }

        protected override MicBufferInfo? GetBufferInfoCore() => _capture.GetBufferInfo();

        protected override void DisposeCore()
        {
            _signal.Dispose();
            _onDisposed();
        }

        private void OnMonitorDetached() => RaiseInputChanged();
    }
}