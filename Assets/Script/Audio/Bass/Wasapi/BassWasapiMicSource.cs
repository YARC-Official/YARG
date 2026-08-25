#nullable enable
using System;
using YARG.Core.Audio;
using YARG.Settings;

namespace YARG.Audio.BASS.Wasapi
{
    internal sealed class BassWasapiMicSource : BassMicSourceBase
    {
        private readonly BassWasapiMicrophoneCapture _capture;
        private readonly BassMicSignal               _signal;
        private readonly Action                      _onDisposed;

        private BassWasapiMicSource(BassWasapiMicrophoneCapture capture, BassMicSignal signal,
            InputDeviceInfo device, Action onDisposed)
            : base(device.Name, device.DisplayName, device.Channel)
        {
            _capture = capture;
            _signal = signal;
            _onDisposed = onDisposed;
        }

        public static BassWasapiMicSource? Create(BassWasapiMicrophoneCapture capture, InputDeviceInfo device,
            BassAudioRouter router, Action onDisposed)
        {
            int[]? channelMap = capture.Channels > 1 ? new[] { device.Channel, -1 } : null;
            float monitoringLevel = SettingsManager.Settings.VocalMonitoring.Value;
            BassWasapiMicSource? source = null;

            var signal = BassMicSignal.Create(
                sourceHandle: capture.ReadHandle,
                channelMap: channelMap,
                sampleRate: capture.SampleRate,
                name: device.DisplayName,
                router: router,
                monitoringLevel: monitoringLevel,
                applyAnalysisEq: true,
                attached: () => capture.AddListener(),
                detached: () =>
                {
                    capture.RemoveListener();
                    source?.RaiseInputChanged();
                });

            if (signal == null)
            {
                return null;
            }

            source = new BassWasapiMicSource(capture, signal, device, onDisposed);
            return source;
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
    }
}
