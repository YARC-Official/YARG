using System;
using ManagedBass;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    /// Covers short BASS playback gaps by replaying recent audio history while an action updates the main stream.
    /// </summary>
    public sealed class BassGapCover : IDisposable
    {
        /// <summary>
        /// Configures history capture, cover stream format, and crossfade timing for <see cref="BassGapCover"/>.
        /// </summary>
        public sealed class Options
        {
            /// <summary>
            /// Sample rate used by the cover stream, in Hz.
            /// </summary>
            public int SampleRate  { get; set; } = 44100;

            /// <summary>
            /// Channel count used by the cover stream.
            /// </summary>
            public int Channels    { get; set; } = 2;

            /// <summary>
            /// Amount of recent audio to keep for gap coverage, in milliseconds.
            /// </summary>
            public int HistoryMs   { get; set; } = 75;

            /// <summary>
            /// Amount of recent audio to replay for gap coverage, in milliseconds.
            /// </summary>
            public int CoverSourceMs { get; set; } = 45;

            /// <summary>
            /// Duration of the fade from cover stream back to main stream, in milliseconds.
            /// </summary>
            public int CrossfadeMs { get; set; } = 45;

            /// <summary>
            /// Number of volume steps used for the manual equal-power crossfade.
            /// </summary>
            public int CrossfadeSteps { get; set; } = 12;

            /// <summary>
            /// Duration of the fade into cover playback, in milliseconds.
            /// </summary>
            public int CoverFadeInMs { get; set; } = 5;

            /// <summary>
            /// Amount of main stream data to prime before crossfading back, in milliseconds.
            /// </summary>
            public int PrimeMs     { get; set; } = 35;

            /// <summary>
            /// Output device used by the cover stream. Negative values use the current BASS device.
            /// </summary>
            public int Device      { get; set; } = -1;

        }

        private readonly int     _mainStream;
        private readonly Options _opt;
        private readonly object  _lock = new();
        private readonly float[] _history;

        private readonly StreamProcedure _coverCallback;
        private readonly SyncProcedure   _slideEndCallback;

        private int     _writePos;
        private int     _filled;
        private float[] _coverSource = Array.Empty<float>();
        private int     _coverPos;

        private          int  _dspHandle;
        private          int  _coverStream;
        private          int   _slideSyncHandle;
        private          int   _coverVersion;
        private          bool  _hasMainRestoreVolume;
        private          float _mainRestoreVolume;
        private volatile bool  _capturing = true;
        private          bool  _disposed;

        /// <summary>
        /// Gets or sets whether cover playback is used when running covered actions.
        /// </summary>
        public bool Enabled { get; set; } = true;

        private BassGapCover(int mainStream, Options opt)
        {
            _mainStream = mainStream;
            _opt = opt;

            int capacity = Math.Max(1, opt.SampleRate * opt.Channels * opt.HistoryMs / 1000);
            _history = new float[capacity];

            DSPProcedure dspCallback = OnDsp;
            _coverCallback = OnCoverStream;
            _slideEndCallback = OnSlideEnded;

            _dspHandle = Bass.ChannelSetDSP(_mainStream, dspCallback, IntPtr.Zero);
            if (_dspHandle == 0)
            {
                YargLogger.LogFormatError("Failed to add gap cover DSP: {0}", Bass.LastError);
            }
        }

        /// <summary>
        /// Creates a gap cover for a BASS stream and derives stream format options from that channel.
        /// </summary>
        /// <param name="stream">BASS stream handle to monitor and cover.</param>
        /// <returns>Gap cover bound to the supplied stream.</returns>
        public static BassGapCover CreateForChannel(int stream)
        {
            var opt = new Options();
            var info = Bass.ChannelGetInfo(stream);
            if (info.Frequency > 0)
            {
                opt.SampleRate = info.Frequency;
            }

            if (info.Channels > 0)
            {
                opt.Channels = info.Channels;
            }

            int device = Bass.ChannelGetDevice(stream);
            if (device >= 0)
            {
                opt.Device = device;
            }

            return new BassGapCover(stream, opt);
        }

        /// <summary>
        /// Runs an action while replaying recent audio to cover any short playback gap it causes.
        /// The action must leave the main stream in the desired playback state.
        /// </summary>
        /// <param name="action">Action that may interrupt and update the main stream.</param>
        /// <returns>Zero when the action completes, or a BASS error code if cover playback could not start.</returns>
        public int Cover(Action action) => Cover(() => { action(); return 0; });

        /// <summary>
        /// Runs an action while replaying recent audio to cover any short playback gap it causes.
        /// The action must leave the main stream in the desired playback state.
        /// </summary>
        /// <param name="action">Function that may interrupt and update the main stream.</param>
        /// <returns>Function result, or a BASS error code if cover playback could not start.</returns>
        public int Cover(Func<int> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (!Enabled || _disposed || _dspHandle == 0)
            {
                return action();
            }

            // If another covered operation starts before the previous crossfade finishes, do
            // not replay the same frozen history again. Cancel the old cover and run uncovered.
            if (IsCovering())
            {
                RestoreMainVolumeIfCovering();
                FreeCoverStream();
                lock (_lock)
                {
                    ++_coverVersion;
                    _coverSource = Array.Empty<float>();
                    _coverPos = 0;
                    _capturing = true;
                }
                return action();
            }

            // 1. Freeze history and snapshot it
            int coverVersion;
            lock (_lock)
            {
                coverVersion = ++_coverVersion;
                _capturing = false;
            }

            lock (_lock)
            {
                _coverSource = SnapshotHistoryLocked();
                _coverPos = 0;
            }

            // 2. Start cover stream to fill the gap
            if (!TryStartCover(out float oldVolume, out int error))
            {
                _capturing = true;
                action();
                return error;
            }

            lock (_lock)
            {
                _mainRestoreVolume = oldVolume;
                _hasMainRestoreVolume = true;
            }

            Bass.ChannelUpdate(_coverStream, 0);

            Bass.ChannelSetAttribute(_mainStream, ChannelAttribute.Volume, 0f);
            if (_opt.CoverFadeInMs > 0)
            {
                Bass.ChannelSlideAttribute(_coverStream, ChannelAttribute.Volume, oldVolume, _opt.CoverFadeInMs);
            }
            else
            {
                Bass.ChannelSetAttribute(_coverStream, ChannelAttribute.Volume, oldVolume);
            }

            // 3. Do the thing that would cause a short gap in the audio (ie a seek).
            // The action owns main stream pause/play state.
            int result;
            try
            {
                result = action();
            }
            catch
            {
                Bass.ChannelSetAttribute(_mainStream, ChannelAttribute.Volume, oldVolume);
                ClearMainRestoreVolume();
                FreeCoverStream();
                _capturing = true;
                throw;
            }

            // 4. Crossfade back to main stream
            Bass.ChannelUpdate(_mainStream, _opt.PrimeMs);

            StartEqualPowerFadeBack(coverVersion, oldVolume);

            return result;
        }

        /// <summary>
        /// Stops cover playback and removes DSP hooks from the main stream.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            _capturing = false;
            RestoreMainVolumeIfCovering();
            FreeCoverStream();

            if (_dspHandle!= 0)
            {
                Bass.ChannelRemoveDSP(_mainStream, _dspHandle);
                _dspHandle = 0;
            }
        }

        private void OnSlideEnded(int handle, int channel, int data, IntPtr user)
        {
            FinishCover(user.ToInt32(), deferFree: true);
        }

        private void FinishCover(int coverVersion, bool deferFree = false)
        {
            int coverStream;
            int slideSyncHandle;
            bool restoreMainVolume;
            float mainRestoreVolume;
            lock (_lock)
            {
                if (_disposed || coverVersion != _coverVersion)
                {
                    return;
                }

                coverStream = _coverStream;
                slideSyncHandle = _slideSyncHandle;
                restoreMainVolume = _hasMainRestoreVolume;
                mainRestoreVolume = _mainRestoreVolume;
                _coverStream = 0;
                _slideSyncHandle = 0;
                _coverSource = Array.Empty<float>();
                _coverPos = 0;
                _hasMainRestoreVolume = false;
                _capturing = true;
            }

            // Snap main stream to its pre-cover volume when cover fade completes. Otherwise BASS
            // may leave it mid-slide when repeated covered ops cancel/restart fades quickly.
            if (restoreMainVolume)
            {
                Bass.ChannelSetAttribute(_mainStream, ChannelAttribute.Volume, mainRestoreVolume);
            }

            if (deferFree)
            {
                // Do not stop/free a BASS stream from its own sync callback. Some drivers wait for
                // the callback to exit, which can deadlock when seeks restart cover playback quickly.
                System.Threading.ThreadPool.QueueUserWorkItem(_ => FreeCoverStream(coverStream, slideSyncHandle));
                return;
            }

            FreeCoverStream(coverStream, slideSyncHandle);
        }

        private void StartEqualPowerFadeBack(int coverVersion, float targetVolume)
        {
            int coverStream = _coverStream;
            int fadeMs = Math.Max(1, _opt.CrossfadeMs);
            int steps = Math.Max(1, _opt.CrossfadeSteps);
            int stepMs = Math.Max(1, fadeMs / steps);

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                for (int i = 1; i <= steps; ++i)
                {
                    if (!IsCoverVersionCurrent(coverVersion))
                    {
                        return;
                    }

                    double t = (double) i / steps;
                    double angle = t * Math.PI * 0.5;
                    float mainVolume = (float) (targetVolume * Math.Sin(angle));
                    float coverVolume = (float) (targetVolume * Math.Cos(angle));

                    Bass.ChannelSetAttribute(_mainStream, ChannelAttribute.Volume, mainVolume);
                    Bass.ChannelSetAttribute(coverStream, ChannelAttribute.Volume, coverVolume);
                    System.Threading.Thread.Sleep(stepMs);
                }

                if (!IsCoverVersionCurrent(coverVersion))
                {
                    return;
                }

                Bass.ChannelSetAttribute(_mainStream, ChannelAttribute.Volume, targetVolume);
                Bass.ChannelSetAttribute(coverStream, ChannelAttribute.Volume, 0f);
                FinishCover(coverVersion);
            });
        }

        private bool IsCoverVersionCurrent(int coverVersion)
        {
            lock (_lock)
            {
                return !_disposed && coverVersion == _coverVersion && _hasMainRestoreVolume;
            }
        }

        private bool TryStartCover(out float oldVolume, out int error)
        {
            Bass.ChannelGetAttribute(_mainStream, ChannelAttribute.Volume, out oldVolume);

            _coverStream = CreateCoverStream();
            if (_coverStream != 0)
            {
                Bass.ChannelSetAttribute(_coverStream, ChannelAttribute.Volume, 0f);
                if (Bass.ChannelPlay(_coverStream))
                {
                    error = 0;
                    return true;
                }
            }

            error = (int)Bass.LastError;
            YargLogger.LogFormatError("Failed to create/play cover stream: {0}", Bass.LastError);
            FreeCoverStream();
            return false;
        }

        private int CreateCoverStream()
        {
            int device = Bass.ChannelGetDevice(_mainStream);
            if (device < 0)
            {
                device = _opt.Device;
            }

            if (device < 0)
            {
                return Bass.CreateStream(_opt.SampleRate, _opt.Channels, BassFlags.Float, _coverCallback, IntPtr.Zero);
            }

            int previousDevice = Bass.CurrentDevice;
            try
            {
                Bass.CurrentDevice = device;
                return Bass.CreateStream(_opt.SampleRate, _opt.Channels, BassFlags.Float, _coverCallback, IntPtr.Zero);
            }
            finally
            {
                if (previousDevice >= 0)
                {
                    Bass.CurrentDevice = previousDevice;
                }
            }
        }

        private unsafe void OnDsp(int handle, int channel, IntPtr buffer, int length, IntPtr user)
        {
            if (!_capturing || _disposed || _history.Length == 0)
            {
                return;
            }

            int samples = length / sizeof(float);
            if (samples <= 0)
            {
                return;
            }

            var src = new ReadOnlySpan<float>((void*)buffer, samples);

            lock (_lock)
            {
                int first = Math.Min(samples, _history.Length - _writePos);
                src[..first].CopyTo(_history.AsSpan(_writePos, first));

                if (first < samples)
                {
                    src[first..].CopyTo(_history.AsSpan(0, samples - first));
                }

                _writePos = (_writePos + samples) % _history.Length;
                _filled = Math.Min(_history.Length, _filled + samples);
            }
        }

        private unsafe int OnCoverStream(int handle, IntPtr buffer, int length, IntPtr user)
        {
            int samples = length / sizeof(float);
            var dst = new Span<float>((void*)buffer, samples);
            dst.Clear();

            lock (_lock)
            {
                int available = _coverSource.Length - _coverPos;
                if (available > 0)
                {
                    int copy = Math.Min(samples, available);
                    _coverSource.AsSpan(_coverPos, copy).CopyTo(dst);
                    _coverPos += copy;
                }
            }
            return length;
        }

        private float[] SnapshotHistoryLocked()
        {
            int count = Math.Min(_history.Length, _filled);
            int coverSamples = Math.Max(1, _opt.SampleRate * _opt.Channels * _opt.CoverSourceMs / 1000);
            count = Math.Min(count, coverSamples);
            if (count == 0)
            {
                return Array.Empty<float>();
            }

            var snap = new float[count];
            int start = (_writePos - count + _history.Length) % _history.Length;

            int first = Math.Min(count, _history.Length - start);
            Array.Copy(_history, start, snap, 0, first);
            if (first < count)
            {
                Array.Copy(_history, 0, snap, first, count - first);
            }

            return snap;
        }

        private bool IsCovering()
        {
            lock (_lock)
            {
                return _hasMainRestoreVolume;
            }
        }

        private void RestoreMainVolumeIfCovering()
        {
            float volume;
            lock (_lock)
            {
                if (!_hasMainRestoreVolume)
                {
                    return;
                }

                volume = _mainRestoreVolume;
                _hasMainRestoreVolume = false;
            }

            Bass.ChannelSetAttribute(_mainStream, ChannelAttribute.Volume, volume);
        }

        private void ClearMainRestoreVolume()
        {
            lock (_lock)
            {
                _hasMainRestoreVolume = false;
            }
        }

        private void FreeCoverStream()
        {
            int coverStream;
            int slideSyncHandle;
            lock (_lock)
            {
                coverStream = _coverStream;
                slideSyncHandle = _slideSyncHandle;
                _coverStream = 0;
                _slideSyncHandle = 0;
            }

            FreeCoverStream(coverStream, slideSyncHandle);
        }

        private static void FreeCoverStream(int coverStream, int slideSyncHandle)
        {
            if (coverStream == 0)
            {
                return;
            }

            if (slideSyncHandle != 0)
            {
                Bass.ChannelRemoveSync(coverStream, slideSyncHandle);
            }

            Bass.ChannelStop(coverStream);
            Bass.StreamFree(coverStream);
        }
    }
}
