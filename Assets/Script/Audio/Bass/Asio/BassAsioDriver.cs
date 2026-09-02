#nullable enable
using System;
using System.Threading;
using ManagedBass.Asio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS.Asio
{
    /// <summary>
    ///     Controls an ASIO hardware driver, managing its lifecycle, format negotiation (sample rate and buffer size),
    ///     and automatic restarts when the driver notifies of hardware or setting changes.
    /// </summary>
    internal sealed class BassAsioDriver : IDisposable
    {
        private const int FIRST_OUTPUT_CHANNEL = 0;
        private const int MASTER_CHANNEL       = -1;
        // BASS speaker pair flags support up to 15 stereo pairs (SpeakerPair15 = channels 29/30).
        private const int MAX_OUTPUT_CHANNELS  = 30;
        private const int PROCESSING_THREADS   = 1;

        private readonly int                 _deviceId;
        private readonly AsioNotifyProcedure _driverNotification;
        private readonly Action              _restartOutput;
        private          bool                _notificationsRegistered;
        private          int                 _restartQueued;
        private          int                 _state = (int) DriverState.Created;

        public BassAsioDriver(int deviceId, Action restartOutput)
        {
            _deviceId = deviceId;
            _restartOutput = restartOutput;
            _driverNotification = OnDriverNotify;
        }

        public  int         SampleRate     { get; private set; }
        public  int         CallbackFrames { get; private set; }
        public  int         InputCount     { get; private set; }
        public  int         OutputCount    { get; private set; }
        public  string      DriverId       { get; private set; } = string.Empty;
        public  string      DriverName     { get; private set; } = string.Empty;
        public  bool        IsStarted      => State == DriverState.Started;
        public  bool        IsDisposed     => State == DriverState.Disposed;
        private DriverState State          => (DriverState) Volatile.Read(ref _state);

        public void Dispose()
        {
            var previousState = (DriverState) Interlocked.Exchange(ref _state, (int) DriverState.Disposed);
            if (previousState is DriverState.Created or DriverState.Disposed)
            {
                return;
            }

            BassAsio.CurrentDevice = _deviceId;
            UnregisterNotify();

            if (previousState == DriverState.Started)
            {
                BassAsio.Stop();
            }

            BassAsio.Free();
        }

        public bool Initialize()
        {
            if (!BassAsio.Init(_deviceId, AsioInitFlags.Thread))
            {
                YargLogger.LogFormatError("Failed to initialize ASIO device: {0}", BassAsio.LastError);
                return false;
            }

            Volatile.Write(ref _state, (int) DriverState.Initialized);
            BassAsio.CurrentDevice = _deviceId;

            var driverInfo = BassAsio.Info;
            if (driverInfo.Outputs < 2)
            {
                YargLogger.LogError("ASIO device does not provide stereo output");
                return false;
            }

            if (!TryReadSampleRate(out int sampleRate))
            {
                return false;
            }

            var deviceInfo = BassAsio.GetDeviceInfo(_deviceId);
            DriverName = string.IsNullOrWhiteSpace(deviceInfo.Name) ? $"ASIO {_deviceId}" : deviceInfo.Name;
            try
            {
                DriverId = string.IsNullOrWhiteSpace(deviceInfo.Driver) ? DriverName : deviceInfo.Driver;
            }
            catch (Exception exception)
            {
                YargLogger.LogException(exception, "Failed to read ASIO driver path; falling back to the driver name");
                DriverId = DriverName;
            }

            SampleRate = sampleRate;
            CallbackFrames = Math.Max(1, driverInfo.PreferredBufferLength);
            InputCount = Math.Max(0, driverInfo.Inputs);
            OutputCount = Math.Min(MAX_OUTPUT_CHANNELS, driverInfo.Outputs);
            return true;
        }

        public bool Start()
        {
            BassAsio.CurrentDevice = _deviceId;
            if (!BassAsio.Start(0, PROCESSING_THREADS))
            {
                YargLogger.LogFormatError("Failed to start ASIO output: {0}", BassAsio.LastError);
                return false;
            }

            Volatile.Write(ref _state, (int) DriverState.Started);
            YargLogger.LogFormatInfo("ASIO processing threads: {0}", PROCESSING_THREADS);
            return true;
        }

        public bool AttachOutput(int mixerHandle)
        {
            BassAsio.CurrentDevice = _deviceId;
            if (BassAsio.ChannelEnableBass(false, FIRST_OUTPUT_CHANNEL, mixerHandle, true))
            {
                return true;
            }

            YargLogger.LogFormatError("Failed to attach ASIO output: {0}", BassAsio.LastError);
            return false;
        }

        public bool SetOutputVolume(double volume)
        {
            BassAsio.CurrentDevice = _deviceId;
            return BassAsio.ChannelSetVolume(false, MASTER_CHANNEL, volume);
        }

        public bool ActivateInput(BassAsioInput input)
        {
            if (input.IsActivated)
            {
                return true;
            }

            BassAsio.CurrentDevice = _deviceId;
            if (!BassAsio.Stop())
            {
                YargLogger.LogFormatError("Failed to stop ASIO while activating input {0}: {1}", input.ChannelIndex,
                    BassAsio.LastError);
                return false;
            }

            Volatile.Write(ref _state, (int) DriverState.Stopped);

            bool inputConfigured = ConfigureInput(input);
            if (!inputConfigured)
            {
                YargLogger.LogFormatError("Failed to activate ASIO input {0}: {1}", input.ChannelIndex,
                    BassAsio.LastError);
                BassAsio.ChannelReset(true, input.ChannelIndex,
                    AsioChannelResetFlags.Enable | AsioChannelResetFlags.Format | AsioChannelResetFlags.Rate);
            }

            if (!Start())
            {
                YargLogger.LogFormatError("Failed to restart ASIO after activating input {0}: {1}", input.ChannelIndex,
                    BassAsio.LastError);
                QueueRestart();
                return false;
            }

            if (!inputConfigured)
            {
                return false;
            }

            input.MarkActivated();
            YargLogger.LogFormatInfo("Activated selected ASIO input {0}", input.ChannelIndex);
            return true;
        }

        public int GetLatencyFrames()
        {
            BassAsio.CurrentDevice = _deviceId;
            return Math.Max(0, BassAsio.GetLatency(false));
        }

        public void RegisterNotify()
        {
            BassAsio.CurrentDevice = _deviceId;
            if (BassAsio.SetNotify(_driverNotification, IntPtr.Zero))
            {
                _notificationsRegistered = true;
                return;
            }

            YargLogger.LogFormatWarning("Failed to register for ASIO driver notifications: {0}", BassAsio.LastError);
        }

        public void Stop()
        {
            var state = State;
            if (state is DriverState.Created or DriverState.Stopped or DriverState.Disposed)
            {
                return;
            }

            BassAsio.CurrentDevice = _deviceId;
            UnregisterNotify();

            if (state == DriverState.Started)
            {
                BassAsio.Stop();
            }

            Volatile.Write(ref _state, (int) DriverState.Stopped);
        }

        private bool ConfigureInput(BassAsioInput input) =>
            BassAsio.ChannelEnableBass(true, input.ChannelIndex, input.RootHandle, false) &&
            BassAsio.ChannelSetFormat(true, input.ChannelIndex, AsioSampleFormat.Float) &&
            BassAsio.ChannelSetRate(true, input.ChannelIndex, SampleRate);

        private static bool TryReadSampleRate(out int sampleRate)
        {
            sampleRate = 0;
            double reportedRate = BassAsio.Rate;
            if (double.IsNaN(reportedRate) || double.IsInfinity(reportedRate))
            {
                YargLogger.LogFormatError("ASIO device reported invalid sample rate: {0}", reportedRate);
                return false;
            }

            double roundedRate = Math.Round(reportedRate);
            if (roundedRate < 1 || roundedRate > int.MaxValue || Math.Abs(reportedRate - roundedRate) > 0.01)
            {
                YargLogger.LogFormatError("ASIO device reported invalid sample rate: {0}", reportedRate);
                return false;
            }

            sampleRate = (int) roundedRate;
            return true;
        }

        private void UnregisterNotify()
        {
            if (!_notificationsRegistered)
            {
                return;
            }

            BassAsio.SetNotify(null, IntPtr.Zero);
            _notificationsRegistered = false;
        }

        private void OnDriverNotify(AsioNotify notification, IntPtr _)
        {
            if (notification is not AsioNotify.Reset and not AsioNotify.Rate)
            {
                return;
            }

            if (State == DriverState.Disposed)
            {
                return;
            }

            QueueRestart();
        }

        private void QueueRestart()
        {
            if (Interlocked.Exchange(ref _restartQueued, 1) == 0)
            {
                UnityMainThreadCallback.QueueEvent(RestartOutput);
            }
        }

        private void RestartOutput()
        {
            Interlocked.Exchange(ref _restartQueued, 0);
            if (State == DriverState.Disposed)
            {
                return;
            }

            YargLogger.LogInfo("Reinitializing ASIO output");
            _restartOutput();
        }

        private enum DriverState
        {
            Created,
            Initialized,
            Started,
            Stopped,
            Disposed,
        }
    }
}