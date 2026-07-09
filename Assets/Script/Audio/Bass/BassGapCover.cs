using System;
using ManagedBass;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    /// Covers short BASS playback gaps by replaying recent audio history while the main stream is interrupted.
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
            public int HistoryMs   { get; set; } = 500;

            /// <summary>
            /// Duration of the fade from cover stream back to main stream, in milliseconds.
            /// </summary>
            public int CrossfadeMs { get; set; } = 250;

            /// <summary>
            /// Amount of main stream data to prime before crossfading back, in milliseconds.
            /// </summary>
            public int PrimeMs     { get; set; } = 100;

            /// <summary>
            /// Creates a shallow copy of these options.
            /// </summary>
            /// <returns>Cloned options instance.</returns>
            public Options Clone() => (Options) MemberwiseClone();
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
        private          int  _slideSyncHandle;
        private          int  _coverVersion;
        private volatile bool _capturing = true;
        private          bool _disposed;

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
        /// <param name="options">Optional base options. Values are cloned before use.</param>
        /// <returns>Gap cover bound to the supplied stream.</returns>
        public static BassGapCover CreateForChannel(int stream, Options options = null)
        {
            var opt = options?.Clone()?? new Options();
            var info = Bass.ChannelGetInfo(stream);
            if (info.Frequency > 0)
            {
                opt.SampleRate = info.Frequency;
            }

            if (info.Channels > 0)
            {
                opt.Channels = info.Channels;
            }
            return new BassGapCover(stream, opt);
        }

        /// <summary>
        /// Runs an action while replaying recent audio to cover any short playback gap it causes.
        /// </summary>
        /// <param name="action">Action that may interrupt the main stream.</param>
        /// <returns>Zero when the action completes, or a BASS error code if cover playback could not start.</returns>
        public int Cover(Action action) => Cover(() => { action(); return 0; });

        /// <summary>
        /// Runs an action while replaying recent audio to cover any short playback gap it causes.
        /// </summary>
        /// <param name="action">Function that may interrupt the main stream.</param>
        /// <param name="actionRestartsPlayback">Whether the action restarts main stream playback itself.</param>
        /// <returns>Function result, or a BASS error code if cover playback could not start.</returns>
        public int Cover(Func<int> action, bool actionRestartsPlayback = false)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (!Enabled || _disposed || _dspHandle == 0)
            {
                return action();
            }

            // 1. Freeze history and snapshot it
            int coverVersion;
            lock (_lock)
            {
                coverVersion = ++_coverVersion;
                _capturing = false;
            }

            FreeCoverStream();

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

            Bass.ChannelUpdate(_coverStream, 0);
            Bass.ChannelSetAttribute(_mainStream, ChannelAttribute.Volume, 0f);
            Bass.ChannelPause(_mainStream);

            // 3. Do the thing that would cause a short gap in the audio (ie a seek)
            int result;
            try
            {
                result = action();
            }
            catch
            {
                Bass.ChannelSetAttribute(_mainStream, ChannelAttribute.Volume, oldVolume);
                FreeCoverStream();
                _capturing = true;
                throw;
            }

            // 4. Crossfade back to main stream
            if (!actionRestartsPlayback &&!Bass.ChannelPlay(_mainStream, true))
            {
                YargLogger.LogFormatError("Failed to restart main stream: {0}", Bass.LastError);
            }

            Bass.ChannelUpdate(_mainStream, _opt.PrimeMs);

            _slideSyncHandle = Bass.ChannelSetSync(_coverStream, SyncFlags.Slided, 0, _slideEndCallback, (IntPtr) coverVersion);
            if (_slideSyncHandle == 0)
            {
                YargLogger.LogFormatError("Failed to set cover stream slide sync: {0}", Bass.LastError);
            }

            if (!Bass.ChannelSlideAttribute(_coverStream, ChannelAttribute.Volume, 0f, _opt.CrossfadeMs))
            {
                YargLogger.LogFormatError("Failed to fade cover stream: {0}", Bass.LastError);
                FinishCover(coverVersion);
            }

            if (!Bass.ChannelSlideAttribute(_mainStream, ChannelAttribute.Volume, oldVolume, _opt.CrossfadeMs))
            {
                YargLogger.LogFormatError("Failed to fade main stream: {0}", Bass.LastError);
            }

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
            FreeCoverStream();

            if (_dspHandle!= 0)
            {
                Bass.ChannelRemoveDSP(_mainStream, _dspHandle);
                _dspHandle = 0;
            }
        }

        private void OnSlideEnded(int handle, int channel, int data, IntPtr user)
        {
            FinishCover(user.ToInt32());
        }

        private void FinishCover(int coverVersion)
        {
            lock (_lock)
            {
                if (_disposed || coverVersion != _coverVersion)
                {
                    return;
                }

                FreeCoverStream();
                _coverSource = Array.Empty<float>();
                _coverPos = 0;
                _capturing = true;
            }
        }

        private bool TryStartCover(out float oldVolume, out int error)
        {
            Bass.ChannelGetAttribute(_mainStream, ChannelAttribute.Volume, out oldVolume);

            _coverStream = Bass.CreateStream(_opt.SampleRate, _opt.Channels, BassFlags.Float, _coverCallback, IntPtr.Zero);
            if (_coverStream!= 0 && Bass.ChannelPlay(_coverStream))
            {
                Bass.ChannelSetAttribute(_coverStream, ChannelAttribute.Volume, oldVolume);
                error = 0;
                return true;
            }

            error = (int)Bass.LastError;
            YargLogger.LogFormatError("Failed to create/play cover stream: {0}", Bass.LastError);
            FreeCoverStream();
            return false;
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

        private void FreeCoverStream()
        {
            if (_slideSyncHandle != 0 && _coverStream != 0)
            {
                Bass.ChannelRemoveSync(_coverStream, _slideSyncHandle);
                _slideSyncHandle = 0;
            }

            if (_coverStream == 0)
            {
                return;
            }
            Bass.ChannelStop(_coverStream);
            Bass.StreamFree(_coverStream);
            _coverStream = 0;
        }
    }
}