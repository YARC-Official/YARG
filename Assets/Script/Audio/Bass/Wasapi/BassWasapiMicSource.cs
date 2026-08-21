#nullable enable
using System;
using YARG.Core.Audio;
using YARG.Settings;

namespace YARG.Audio.BASS.Wasapi
{
    /// <summary>
    ///     Represents a single microphone input channel captured from a WASAPI Exclusive recording device.
    /// </summary>
    internal sealed class BassWasapiMicSource : BassMicSourceBase
    {
        private readonly BassWasapiMicrophoneCapture _capture;
        private readonly Action                      _onDisposed;
        private readonly BassMicSignal?              _signal;

        private BassWasapiMicSource(BassWasapiMicrophoneCapture capture, string baseName, string displayName,
            int channel, BassAudioRouter router, Action onDisposed)
            : base(baseName, displayName, channel)
        {
            _capture = capture;
            _onDisposed = onDisposed;

            int[]? channelMap = capture.Channels > 1 ? new[] { channel, -1 } : null;
            _signal = BassMicSignal.Create(capture.ReadHandle, channelMap, capture.SampleRate, displayName, router,
                SettingsManager.Settings.VocalMonitoring.Value, true, OnMonitorAttached, OnMonitorDetached);
        }

        public static BassWasapiMicSource? Create(BassWasapiMicrophoneCapture capture, string baseName,
            string displayName, int channel, BassAudioRouter router, Action onDisposed)
        {
            var source = new BassWasapiMicSource(capture, baseName, displayName, channel, router, onDisposed);
            if (source._signal == null || !source.IsValid)
            {
                source.Dispose();
                return null;
            }

            return source;
        }

        protected override int GetSampleRateCore() => _capture.SampleRate;

        protected override bool GetIsValidCore() => _signal?.IsValid == true;

        protected override int ReadCore(Span<float> destination) => _signal?.Read(destination) ?? -1;

        protected override int GetBacklogBytesCore() => _signal?.GetBacklogBytes() ?? -1;

        protected override bool TryCreateRecordingChannelCore(bool withEffects, out int handle)
        {
            if (_signal != null)
            {
                return _signal.TryCreateRecordingChannel(withEffects, out handle);
            }

            handle = 0;
            return false;
        }

        protected override void ReleaseRecordingChannelCore(int handle) => _signal?.ReleaseRecordingChannel(handle);

        protected override bool ResetToLiveCore() => _signal?.ResetToLive() == true;

        protected override bool SetMonitoringLevelCore(float volume)
        {
            _signal?.SetMonitoringLevel(volume);
            return true;
        }

        protected override bool SetReverbLevelCore(float wet)
        {
            _signal?.SetReverbLevel(wet);
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
                bool monitorReset = _signal?.ResetMonitor() == true;
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
