#nullable enable
using System;
using ManagedBass;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Input;
using YARG.Settings;

namespace YARG.Audio.BASS
{
    public sealed class BassMicDevice : MicDevice
    {
        private readonly string                _baseName;
        private readonly BassMicrophoneCapture _capture;
        private readonly object                _lifecycleLock = new();
        private readonly BassAudioRouter       _router;
        private          BassMicAnalyzer?      _analyzer;
        private          BassMicSignal?        _signal;

        private BassMicDevice(BassMicrophoneCapture capture, string baseName, string displayName, int captureChannel,
            BassAudioRouter router) : base(displayName)
        {
            _capture = capture;
            _baseName = baseName;
            CaptureChannel = captureChannel;
            _router = router;
        }

        internal int CaptureChannel { get; }

        internal static BassMicDevice? Create(BassMicrophoneCapture capture, string baseName, int captureChannel,
            BassAudioRouter router)
        {
            string displayName = capture.Channels > 1 ? $"{baseName} - Channel {captureChannel + 1}" : baseName;
            var device = new BassMicDevice(capture, baseName, displayName, captureChannel, router);
            if (device.Initialize())
            {
                return device;
            }

            device.Dispose();
            return null;
        }

        internal event Action? Disposed;

        private bool Initialize()
        {
            int[] channelMap =
            {
                CaptureChannel,
                -1,
            };
            _signal = BassMicSignal.Create(_capture.ReadHandle, channelMap, _capture.SampleRate, DisplayName, _router,
                SettingsManager.Settings.VocalMonitoring.Value, true, OnMonitorAttached, OnMonitorDetached);
            if (_signal == null)
            {
                return false;
            }

            try
            {
                _analyzer = new BassMicAnalyzer(_signal, () => IsRecordingOutput, () => InputManager.CurrentInputTime);
            }
            catch (Exception exception)
            {
                YargLogger.LogException(exception, $"Failed to initialize microphone '{DisplayName}'");
                return false;
            }

            return _capture.Start();
        }

        public override int Reset()
        {
            lock (_lifecycleLock)
            {
                if (_signal == null || _analyzer == null)
                {
                    return 0;
                }

                if (!_capture.PauseAndDiscardBufferedAudio())
                {
                    return (int) Bass.LastError;
                }

                bool analysisReset = _analyzer.Reset();
                bool monitorReset = _signal.ResetMonitor();
                bool resumed = _capture.Resume();
                return analysisReset && monitorReset && resumed ? 0 : (int) Bass.LastError;
            }
        }

        public override bool DequeueOutputFrame(out MicOutputFrame frame)
        {
            lock (_lifecycleLock)
            {
                if (_analyzer != null)
                {
                    return _analyzer.DequeueOutputFrame(out frame);
                }
            }

            frame = default;
            return false;
        }

        public override void ClearOutputQueue()
        {
            lock (_lifecycleLock)
            {
                _analyzer?.ClearOutputQueue();
            }
        }

        public override void SetMonitoringLevel(float volume)
        {
            lock (_lifecycleLock)
            {
                _signal?.SetMonitoringLevel(volume);
            }
        }

        public override SerializedMic Serialize() => new(_baseName, CaptureChannel);

        private void OnMonitorAttached() => _capture.AddListener();

        private void OnMonitorDetached()
        {
            _capture.RemoveListener();

            BassMicAnalyzer? analyzer;
            lock (_lifecycleLock)
            {
                analyzer = _analyzer;
            }

            try
            {
                analyzer?.Reset();
            }
            catch (Exception exception)
            {
                YargLogger.LogException(exception, "Failed to reset microphone analysis after monitor detach");
            }
        }

        protected override void DisposeUnmanagedResources()
        {
            BassMicAnalyzer? analyzer;
            lock (_lifecycleLock)
            {
                analyzer = _analyzer;
            }

            analyzer?.StopAndJoin();

            BassMicSignal? signal;
            lock (_lifecycleLock)
            {
                _analyzer = null;
                signal = _signal;
                _signal = null;
            }

            signal?.Dispose();
            Disposed?.Invoke();
        }
    }
}
