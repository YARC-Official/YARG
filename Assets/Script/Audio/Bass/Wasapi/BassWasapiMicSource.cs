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

        private BassWasapiMicSource(BassWasapiMicrophoneCapture capture, InputDeviceInfo device,
            BassAudioRouter router, Action onDisposed)
            : base(device.Name, device.DisplayName, device.Channel)
        {
            _capture = capture;
            _onDisposed = onDisposed;

            int[]? channelMap = capture.Channels > 1 ? new[] { device.Channel, -1 } : null;
            _signal = BassMicSignal.Create(capture.ReadHandle, channelMap, capture.SampleRate, device.DisplayName,
                router, SettingsManager.Settings.VocalMonitoring.Value, true, OnMonitorAttached, OnMonitorDetached)!;
        }

        public static BassWasapiMicSource? Create(BassWasapiMicrophoneCapture capture, InputDeviceInfo device,
            BassAudioRouter router, Action onDisposed)
        {
            var source = new BassWasapiMicSource(capture, device, router, onDisposed);
            if (source._signal == null || !source.IsValid)
            {
                source.Dispose();
                return null;
            }

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

                bool discarded = _capture.PauseAndDiscardBufferedAudio();
                bool monitorReset = _signal.ResetMonitor();
                bool resumed = _capture.Resume();
                return discarded && monitorReset && resumed;
            }
        }

        protected override MicBufferInfo? GetBufferInfoCore() => _capture.GetBufferInfo();

        protected override void DisposeCore()
        {
            _signal?.Dispose();
            _onDisposed();
        }

        private void OnMonitorAttached()
        {
            _capture.AddListener();
        }

        private void OnMonitorDetached()
        {
            _capture.RemoveListener();
            RaiseInputChanged();
        }
    }
}
