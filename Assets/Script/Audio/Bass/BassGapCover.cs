using System;
using System.Diagnostics;
using ManagedBass;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    /// Covers short BASS playback gaps while an action updates the main stream.
    /// A DSP callback continuously stores recent float samples in a circular history buffer. <see cref="Cover(Func{int})"/>
    /// freezes and snapshots that history, then replays its recent tail through a temporary BASS stream while the main stream is muted.
    /// After the action completes, the main stream is primed and crossfaded back in before the temporary stream is freed.
    /// Starting another covered operation during a crossfade cancels current cover and runs new action uncovered.
    /// </summary>
    public sealed class BassGapCover : IDisposable
    {
        // Three perceptual tuning values. Other timings derive from these so they cannot drift apart.
        private const int HISTORY_MS = 500;
        private const int COVER_SOURCE_MS = 300;
        private const int CROSSFADE_MS = 20;

        private sealed class CoverState
        {
            public readonly float[] Source;
            public int SourceSamplePos;

            public CoverState(float[] source, int sourceSamplePos)
            {
                Source = source;
                SourceSamplePos = sourceSamplePos;
            }
        }

        private readonly int     _mainStream;
        private readonly int     _sampleRate;
        private readonly int     _channels;
        private readonly object  _lock = new();
        private readonly object  _coverOperationLock = new();
        private readonly float[] _history;

        private readonly StreamProcedure _coverCallback;

        // DSP writes samples before publishing these positions. Snapshot reads may include a
        // partially updated sample only; that is preferable to blocking BASS's audio thread.
        private int _writePos;
        private int _filled;
        private CoverState _coverState;

        private          int  _dspHandle;
        private          int  _coverStream;
        private          int   _coverVersion;
        private          bool  _hasMainRestoreVolume;
        private          float _mainRestoreVolume;
        private volatile bool  _capturing = true;
        private volatile bool  _disposed;

        /// <summary>
        /// Gets or sets whether cover playback is used when running covered actions.
        /// </summary>
        public bool Enabled { get; set; } = true;

        private BassGapCover(int mainStream, int sampleRate, int channels)
        {
            _mainStream = mainStream;
            _sampleRate = sampleRate;
            _channels = channels;

            int capacity = Math.Max(1, sampleRate * channels * HISTORY_MS / 1000);
            _history = new float[capacity];

            DSPProcedure dspCallback = OnDsp;
            _coverCallback = OnCoverStream;

            _dspHandle = Bass.ChannelSetDSP(_mainStream, dspCallback, IntPtr.Zero);
            if (_dspHandle == 0)
            {
                YargLogger.LogFormatError("Failed to add gap cover DSP: {0}", Bass.LastError);
            }
        }

        /// <summary>
        /// Creates a gap cover for a BASS stream and derives its stream format from that channel.
        /// </summary>
        /// <param name="stream">BASS stream handle to monitor and cover.</param>
        /// <returns>Gap cover bound to the supplied stream.</returns>
        public static BassGapCover CreateForChannel(int stream)
        {
            var info = Bass.ChannelGetInfo(stream);
            int sampleRate = info.Frequency > 0 ? info.Frequency : 44100;
            int channels = info.Channels > 0 ? info.Channels : 2;
            return new BassGapCover(stream, sampleRate, channels);
        }

        /// <summary>
        /// Runs an action while replaying recent audio to cover any short playback gap it causes.
        /// The action must leave the main stream in the desired playback state.
        /// </summary>
        /// <param name="action">Function that may interrupt and update the main stream.</param>
        /// <returns>Function result, or a BASS error code if cover playback could not start.</returns>
        public int Cover(Func<int> action)
        {
            lock (_coverOperationLock)
            {
                return CoverLocked(action);
            }
        }

        private int CoverLocked(Func<int> action)
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
            if (CancelCoverIfActive())
            {
                return action();
            }

            // 1. Freeze history and snapshot it
            int coverVersion;
            lock (_lock)
            {
                coverVersion = ++_coverVersion;
                _capturing = false;
                float[] source = SnapshotHistory();
                int sourceFrames = source.Length / _channels;
                int tailFrames = Math.Max(1, _sampleRate * COVER_SOURCE_MS * 2 / 3_000);
                int sourceSamplePos = Math.Max(0, sourceFrames - tailFrames) * _channels;
                System.Threading.Volatile.Write(ref _coverState, new CoverState(source, sourceSamplePos));
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

            if (!Bass.ChannelSetAttribute(_mainStream, ChannelAttribute.Volume, 0f))
            {
                error = (int) Bass.LastError;
                YargLogger.LogFormatError("Failed to mute main stream for gap cover: {0}", Bass.LastError);
                ClearMainRestoreVolume();
                FreeCoverStream();
                _capturing = true;
                action();
                return error;
            }

            int coverFadeInMs = Math.Max(1, CROSSFADE_MS / 3);
            Bass.ChannelSlideAttribute(_coverStream, ChannelAttribute.Volume, oldVolume, coverFadeInMs);

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
            int primeMs = Math.Max(1, COVER_SOURCE_MS / 2);
            Bass.ChannelUpdate(_mainStream, primeMs);

            StartFadeBack(coverVersion, oldVolume);

            return result;
        }

        /// <summary>
        /// Stops cover playback and removes DSP hooks from the main stream.
        /// </summary>
        public void Dispose()
        {
            lock (_coverOperationLock)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;

                _capturing = false;
                CancelCoverIfActive();

                if (_dspHandle != 0)
                {
                    Bass.ChannelRemoveDSP(_mainStream, _dspHandle);
                    _dspHandle = 0;
                }
            }
        }

        private void FinishCover(int coverVersion)
        {
            int coverStream;
            bool restoreMainVolume;
            float mainRestoreVolume;
            lock (_lock)
            {
                if (_disposed || coverVersion != _coverVersion)
                {
                    return;
                }

                coverStream = _coverStream;
                restoreMainVolume = _hasMainRestoreVolume;
                mainRestoreVolume = _mainRestoreVolume;
                _coverStream = 0;
                System.Threading.Volatile.Write(ref _coverState, null);
                _hasMainRestoreVolume = false;
                _capturing = true;
            }

            // Snap main stream to its pre-cover volume when cover fade completes. Otherwise BASS
            // may leave it mid-slide when repeated covered ops cancel/restart fades quickly.
            if (restoreMainVolume)
            {
                Bass.ChannelSetAttribute(_mainStream, ChannelAttribute.Volume, mainRestoreVolume);
            }

            FreeCoverStream(coverStream);
        }

        private void StartFadeBack(int coverVersion, float targetVolume)
        {
            int coverStream;
            lock (_lock)
            {
                coverStream = _coverStream;
            }

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                FadeCoverBack(coverVersion, coverStream, targetVolume);
                if (!IsCoverVersionCurrent(coverVersion))
                {
                    return;
                }

                Bass.ChannelSetAttribute(_mainStream, ChannelAttribute.Volume, targetVolume);
                Bass.ChannelSetAttribute(coverStream, ChannelAttribute.Volume, 0f);
                FinishCover(coverVersion);
            });
        }

        private void FadeCoverBack(int coverVersion, int coverStream, float targetVolume)
        {
            int fadeMs = Math.Max(1, CROSSFADE_MS);
            int steps = Math.Min(8, fadeMs);
            var watch = Stopwatch.StartNew();
            for (int i = 1; i <= steps; ++i)
            {
                if (!IsCoverVersionCurrent(coverVersion))
                {
                    return;
                }

                int targetMs = i * fadeMs / steps;
                int sleepMs = targetMs - (int) watch.ElapsedMilliseconds;
                if (sleepMs > 0)
                {
                    System.Threading.Thread.Sleep(sleepMs);
                }

                if (!IsCoverVersionCurrent(coverVersion))
                {
                    return;
                }

                double t = Math.Min(1.0, watch.Elapsed.TotalMilliseconds / fadeMs);
                double fadeAngle = t * Math.PI / 2.0;
                float mainVolume = (float) (targetVolume * Math.Sin(fadeAngle));
                float coverVolume = (float) (targetVolume * Math.Cos(fadeAngle));
                Bass.ChannelSetAttribute(_mainStream, ChannelAttribute.Volume, mainVolume);
                Bass.ChannelSetAttribute(coverStream, ChannelAttribute.Volume, coverVolume);
            }
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
                if (!Bass.ChannelSetAttribute(_coverStream, ChannelAttribute.Volume, 0f))
                {
                    error = (int) Bass.LastError;
                    YargLogger.LogFormatError("Failed to initialize gap cover volume: {0}", Bass.LastError);
                    FreeCoverStream();
                    return false;
                }

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
                return Bass.CreateStream(_sampleRate, _channels, BassFlags.Float, _coverCallback, IntPtr.Zero);
            }

            int previousDevice = Bass.CurrentDevice;
            try
            {
                Bass.CurrentDevice = device;
                return Bass.CreateStream(_sampleRate, _channels, BassFlags.Float, _coverCallback, IntPtr.Zero);
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

            int capacity = _history.Length;
            if (samples >= capacity)
            {
                src[^capacity..].CopyTo(_history);
                System.Threading.Volatile.Write(ref _writePos, 0);
                System.Threading.Volatile.Write(ref _filled, capacity);
                return;
            }

            int writePos = System.Threading.Volatile.Read(ref _writePos);
            int first = Math.Min(samples, capacity - writePos);
            src[..first].CopyTo(_history.AsSpan(writePos, first));
            if (first < samples)
            {
                src[first..].CopyTo(_history.AsSpan(0, samples - first));
            }

            System.Threading.Volatile.Write(ref _writePos, (writePos + samples) % capacity);
            int filled = System.Threading.Volatile.Read(ref _filled);
            System.Threading.Volatile.Write(ref _filled, Math.Min(capacity, filled + samples));
        }

        private unsafe int OnCoverStream(int handle, IntPtr buffer, int length, IntPtr user)
        {
            int samples = length / sizeof(float);
            var dst = new Span<float>((void*)buffer, samples);
            dst.Clear();

            CoverState coverState = System.Threading.Volatile.Read(ref _coverState);
            if (coverState == null)
            {
                return length;
            }

            FillCover(dst, coverState);
            return length;
        }

        private static void FillCover(Span<float> dst, CoverState coverState)
        {
            int sourceSamplePos = coverState.SourceSamplePos;
            int availableSamples = coverState.Source.Length - sourceSamplePos;
            if (availableSamples <= 0)
            {
                return;
            }

            int copyCount = Math.Min(dst.Length, availableSamples);
            coverState.Source.AsSpan(sourceSamplePos, copyCount).CopyTo(dst);
            coverState.SourceSamplePos += copyCount;
        }

        private float[] SnapshotHistory()
        {
            int count = Math.Min(_history.Length, System.Threading.Volatile.Read(ref _filled));
            int coverSamples = Math.Max(1, _sampleRate * _channels * COVER_SOURCE_MS / 1000);
            count = Math.Min(count, coverSamples);
            if (count == 0)
            {
                return Array.Empty<float>();
            }

            var snap = new float[count];
            int writePos = System.Threading.Volatile.Read(ref _writePos);
            int start = (writePos - count + _history.Length) % _history.Length;

            int first = Math.Min(count, _history.Length - start);
            Array.Copy(_history, start, snap, 0, first);
            if (first < count)
            {
                Array.Copy(_history, 0, snap, first, count - first);
            }

            return snap;
        }

        private bool CancelCoverIfActive()
        {
            int coverStream;
            float volume;
            lock (_lock)
            {
                if (!_hasMainRestoreVolume)
                {
                    return false;
                }

                ++_coverVersion;
                coverStream = _coverStream;
                volume = _mainRestoreVolume;
                _coverStream = 0;
                System.Threading.Volatile.Write(ref _coverState, null);
                _hasMainRestoreVolume = false;
                _capturing = true;
            }

            Bass.ChannelSetAttribute(_mainStream, ChannelAttribute.Volume, volume);
            FreeCoverStream(coverStream);
            return true;
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
            lock (_lock)
            {
                coverStream = _coverStream;
                _coverStream = 0;
                System.Threading.Volatile.Write(ref _coverState, null);
            }

            FreeCoverStream(coverStream);
        }

        private static void FreeCoverStream(int coverStream)
        {
            if (coverStream == 0)
            {
                return;
            }

            Bass.ChannelStop(coverStream);
            Bass.StreamFree(coverStream);
        }
    }
}
