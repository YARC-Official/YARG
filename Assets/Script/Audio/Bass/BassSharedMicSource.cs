#nullable enable
using System;
using YARG.Core.Audio;
using YARG.Settings;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     Represents a single microphone input channel captured from a standard BASS recording device.
    /// </summary>
    internal sealed class BassSharedMicSource : BassMicSourceBase
    {
        private readonly BassMicrophoneCapture _capture;
        private readonly Action _onDisposed;
        private BassMicSignal _signal;

        public BassSharedMicSource(BassMicrophoneCapture capture, string baseName, string displayName,
            int channel, BassAudioRouter router, Action onDisposed)
            : base(baseName, displayName, channel)
        {
            _capture = capture;
            _onDisposed = onDisposed;

            int[] channelMap = { channel, -1 };
            float monitoringLevel = SettingsManager.Settings.VocalMonitoring.Value;
            _signal = BassMicSignal.Create(
                sourceHandle: capture.ReadHandle,
                channelMap: channelMap,
                sampleRate: capture.SampleRate,
                name: displayName,
                router: router,
                monitoringLevel: monitoringLevel,
                applyAnalysisEq: true,
                attached: OnMonitorAttached,
                detached: OnMonitorDetached)
                ?? throw new InvalidOperationException($"Failed to create mic signal for '{displayName}'");
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
                bool analysisReset = _signal.ResetAnalysis();
                bool resumed = _capture.Resume();
                return discarded && monitorReset && analysisReset && resumed;
            }
        }

        protected override MicBufferInfo? GetBufferInfoCore() => _capture.GetBufferInfo();

        protected override void DisposeCore()
        {
            _signal.Dispose();
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
