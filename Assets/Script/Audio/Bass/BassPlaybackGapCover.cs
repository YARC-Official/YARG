using System;
using System.Diagnostics;
using ManagedBass;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    /// Covers short BASS playback gaps while an action updates the main stream.
    /// A DSP callback continuously stores recent float samples in a circular history buffer. <see cref="CoverPlaybackGap(Func{int})"/>
    /// freezes and snapshots that history, then replays its recent tail through a temporary BASS stream while the main stream is muted.
    /// After the action completes, the main stream is primed and crossfaded back in before the temporary stream is freed.
    /// Starting another covered operation during a crossfade cancels current cover and runs new action uncovered.
    /// </summary>
    public sealed class BassPlaybackGapCover : IDisposable
    {
        // Three perceptual tuning values. Other timings derive from these so they cannot drift apart.
        private const int HISTORY_MS = 500;
        private const int COVER_SOURCE_MS = 300;
        private const int CROSSFADE_MS = 20;

        private sealed class FrozenAudio
        {
            public readonly float[] Samples;
            public int ReadSamplePosition;

            public FrozenAudio(float[] samples, int readSamplePosition)
            {
                Samples = samples;
                ReadSamplePosition = readSamplePosition;
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
        private int _historyWritePosition;
        private int _recordedSampleCount;
        private FrozenAudio _frozenAudio;

        private          int  _dspHandle;
        private          int  _coverStream;
        private          int  _coverGeneration;
        private          bool _hasMainRestoreVolume;
        private          float _mainRestoreVolume;
        private volatile bool  _capturing = true;
        private volatile bool  _disposed;

        /// <summary>
        /// Gets or sets whether cover playback is used when running covered actions.
        /// </summary>
        public bool Enabled { get; set; } = true;

        private BassPlaybackGapCover(int mainStream, int sampleRate, int channels)
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
        public static BassPlaybackGapCover CreateForChannel(int stream)
        {
            var info = Bass.ChannelGetInfo(stream);
            int sampleRate = info.Frequency > 0 ? info.Frequency : 44100;
            int channels = info.Channels > 0 ? info.Channels : 2;
            return new BassPlaybackGapCover(stream, sampleRate, channels);
        }

        /// <summary>
        /// Runs an action while replaying recent audio to cover any short playback gap it causes.
        /// The action must leave the main stream in the desired playback state.
        /// </summary>
        /// <param name="action">Function that may interrupt and update the main stream.</param>
        /// <returns>Function result, or a BASS error code if cover playback could not start.</returns>
        public int CoverPlaybackGap(Func<int> action)
        {
            lock (_coverOperationLock)
            {
                return CoverPlaybackGapLocked(action);
            }
        }

        private int CoverPlaybackGapLocked(Func<int> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (!Enabled || _disposed || _dspHandle == 0)
            {
                return action();
            }

            // Freeze recent audio → play it while main stream updates → crossfade main stream back in.
            // A new operation during an active crossfade cancels cover and runs uncovered.
            if (CancelActivePlaybackCover())
            {
                return action();
            }

            if (!TryBeginPlaybackCover(out int coverGeneration, out float mainStreamVolume, out int error))
            {
                action();
                return error;
            }

            return RunCoveredAction(action, coverGeneration, mainStreamVolume);
        }

        private bool TryBeginPlaybackCover(out int coverGeneration, out float mainStreamVolume, out int error)
        {
            coverGeneration = FreezeRecentAudioForCover();
            if (!TryStartCoverStream(out mainStreamVolume, out error))
            {
                _capturing = true;
                return false;
            }

            lock (_lock)
            {
                _mainRestoreVolume = mainStreamVolume;
                _hasMainRestoreVolume = true;
            }

            Bass.ChannelUpdate(_coverStream, 0);
            if (!Bass.ChannelSetAttribute(_mainStream, ChannelAttribute.Volume, 0f))
            {
                error = (int) Bass.LastError;
                YargLogger.LogFormatError("Failed to mute main stream for gap cover: {0}", Bass.LastError);
                ClearMainStreamRestoreVolume();
                FreePlaybackCoverStream();
                _capturing = true;
                return false;
            }

            int coverFadeInMs = Math.Max(1, CROSSFADE_MS / 3);
            Bass.ChannelSlideAttribute(_coverStream, ChannelAttribute.Volume, mainStreamVolume, coverFadeInMs);
            return true;
        }

        private int RunCoveredAction(Func<int> action, int coverGeneration, float mainStreamVolume)
        {
            try
            {
                int result = action();
                PrimeMainStreamAndStartCrossfade(coverGeneration, mainStreamVolume);
                return result;
            }
            catch
            {
                Bass.ChannelSetAttribute(_mainStream, ChannelAttribute.Volume, mainStreamVolume);
                ClearMainStreamRestoreVolume();
                FreePlaybackCoverStream();
                _capturing = true;
                throw;
            }
        }

        private int FreezeRecentAudioForCover()
        {
            lock (_lock)
            {
                int coverGeneration = ++_coverGeneration;
                _capturing = false;
                float[] audioSamples = FreezeRecentAudio();
                int audioFrames = audioSamples.Length / _channels;
                int tailFrames = Math.Max(1, _sampleRate * COVER_SOURCE_MS * 2 / 3_000);
                int readSamplePosition = Math.Max(0, audioFrames - tailFrames) * _channels;
                System.Threading.Volatile.Write(ref _frozenAudio, new FrozenAudio(audioSamples, readSamplePosition));
                return coverGeneration;
            }
        }

        private void PrimeMainStreamAndStartCrossfade(int coverGeneration, float mainStreamVolume)
        {
            int primeMs = Math.Max(1, COVER_SOURCE_MS / 2);
            Bass.ChannelUpdate(_mainStream, primeMs);
            StartCrossfadeToMainStream(coverGeneration, mainStreamVolume);
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
                CancelActivePlaybackCover();

                if (_dspHandle != 0)
                {
                    Bass.ChannelRemoveDSP(_mainStream, _dspHandle);
                    _dspHandle = 0;
                }
            }
        }

        private void FinishPlaybackCover(int coverGeneration)
        {
            int coverStream;
            bool restoreMainVolume;
            float mainRestoreVolume;
            lock (_lock)
            {
                if (_disposed || coverGeneration != _coverGeneration)
                {
                    return;
                }

                coverStream = _coverStream;
                restoreMainVolume = _hasMainRestoreVolume;
                mainRestoreVolume = _mainRestoreVolume;
                _coverStream = 0;
                System.Threading.Volatile.Write(ref _frozenAudio, null);
                _hasMainRestoreVolume = false;
                _capturing = true;
            }

            // Snap main stream to its pre-cover volume when cover fade completes. Otherwise BASS
            // may leave it mid-slide when repeated covered ops cancel/restart fades quickly.
            if (restoreMainVolume)
            {
                Bass.ChannelSetAttribute(_mainStream, ChannelAttribute.Volume, mainRestoreVolume);
            }

            FreePlaybackCoverStream(coverStream);
        }

        private void StartCrossfadeToMainStream(int coverGeneration, float targetVolume)
        {
            int coverStream;
            lock (_lock)
            {
                coverStream = _coverStream;
            }

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                CrossfadeToMainStream(coverGeneration, coverStream, targetVolume);
                if (!IsCoverGenerationCurrent(coverGeneration))
                {
                    return;
                }

                Bass.ChannelSetAttribute(_mainStream, ChannelAttribute.Volume, targetVolume);
                Bass.ChannelSetAttribute(coverStream, ChannelAttribute.Volume, 0f);
                FinishPlaybackCover(coverGeneration);
            });
        }

        private void CrossfadeToMainStream(int coverGeneration, int coverStream, float targetVolume)
        {
            int fadeMs = Math.Max(1, CROSSFADE_MS);
            int steps = Math.Min(8, fadeMs);
            var watch = Stopwatch.StartNew();
            for (int i = 1; i <= steps; ++i)
            {
                if (!IsCoverGenerationCurrent(coverGeneration))
                {
                    return;
                }

                int targetMs = i * fadeMs / steps;
                int sleepMs = targetMs - (int) watch.ElapsedMilliseconds;
                if (sleepMs > 0)
                {
                    System.Threading.Thread.Sleep(sleepMs);
                }

                if (!IsCoverGenerationCurrent(coverGeneration))
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

        private bool IsCoverGenerationCurrent(int coverGeneration)
        {
            lock (_lock)
            {
                return !_disposed && coverGeneration == _coverGeneration && _hasMainRestoreVolume;
            }
        }

        private bool TryStartCoverStream(out float mainStreamVolume, out int error)
        {
            Bass.ChannelGetAttribute(_mainStream, ChannelAttribute.Volume, out mainStreamVolume);

            _coverStream = CreateCoverStreamOnMainDevice();
            if (_coverStream != 0)
            {
                if (!Bass.ChannelSetAttribute(_coverStream, ChannelAttribute.Volume, 0f))
                {
                    error = (int) Bass.LastError;
                    YargLogger.LogFormatError("Failed to initialize gap cover volume: {0}", Bass.LastError);
                    FreePlaybackCoverStream();
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
            FreePlaybackCoverStream();
            return false;
        }

        private int CreateCoverStreamOnMainDevice()
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
                System.Threading.Volatile.Write(ref _historyWritePosition, 0);
                System.Threading.Volatile.Write(ref _recordedSampleCount, capacity);
                return;
            }

            int historyWritePosition = System.Threading.Volatile.Read(ref _historyWritePosition);
            int firstCopyCount = Math.Min(samples, capacity - historyWritePosition);
            src[..firstCopyCount].CopyTo(_history.AsSpan(historyWritePosition, firstCopyCount));
            if (firstCopyCount < samples)
            {
                src[firstCopyCount..].CopyTo(_history.AsSpan(0, samples - firstCopyCount));
            }

            int nextHistoryWritePosition = (historyWritePosition + samples) % capacity;
            System.Threading.Volatile.Write(ref _historyWritePosition, nextHistoryWritePosition);
            int recordedSampleCount = System.Threading.Volatile.Read(ref _recordedSampleCount);
            int newRecordedSampleCount = Math.Min(capacity, recordedSampleCount + samples);
            System.Threading.Volatile.Write(ref _recordedSampleCount, newRecordedSampleCount);
        }

        private unsafe int OnCoverStream(int handle, IntPtr buffer, int length, IntPtr user)
        {
            int samples = length / sizeof(float);
            var dst = new Span<float>((void*)buffer, samples);
            dst.Clear();

            FrozenAudio frozenAudio = System.Threading.Volatile.Read(ref _frozenAudio);
            if (frozenAudio == null)
            {
                return length;
            }

            FillFrozenAudio(dst, frozenAudio);
            return length;
        }

        private static void FillFrozenAudio(Span<float> destination, FrozenAudio frozenAudio)
        {
            int readSamplePosition = frozenAudio.ReadSamplePosition;
            int availableSamples = frozenAudio.Samples.Length - readSamplePosition;
            if (availableSamples <= 0)
            {
                return;
            }

            int copyCount = Math.Min(destination.Length, availableSamples);
            frozenAudio.Samples.AsSpan(readSamplePosition, copyCount).CopyTo(destination);
            frozenAudio.ReadSamplePosition += copyCount;
        }

        private float[] FreezeRecentAudio()
        {
            int count = Math.Min(_history.Length, System.Threading.Volatile.Read(ref _recordedSampleCount));
            int coverSamples = Math.Max(1, _sampleRate * _channels * COVER_SOURCE_MS / 1000);
            count = Math.Min(count, coverSamples);
            if (count == 0)
            {
                return Array.Empty<float>();
            }

            var audioSamples = new float[count];
            int historyWritePosition = System.Threading.Volatile.Read(ref _historyWritePosition);
            int startPosition = (historyWritePosition - count + _history.Length) % _history.Length;

            int firstCopyCount = Math.Min(count, _history.Length - startPosition);
            Array.Copy(_history, startPosition, audioSamples, 0, firstCopyCount);
            if (firstCopyCount < count)
            {
                Array.Copy(_history, 0, audioSamples, firstCopyCount, count - firstCopyCount);
            }

            return audioSamples;
        }

        private bool CancelActivePlaybackCover()
        {
            int coverStream;
            float volume;
            lock (_lock)
            {
                if (!_hasMainRestoreVolume)
                {
                    return false;
                }

                ++_coverGeneration;
                coverStream = _coverStream;
                volume = _mainRestoreVolume;
                _coverStream = 0;
                System.Threading.Volatile.Write(ref _frozenAudio, null);
                _hasMainRestoreVolume = false;
                _capturing = true;
            }

            Bass.ChannelSetAttribute(_mainStream, ChannelAttribute.Volume, volume);
            FreePlaybackCoverStream(coverStream);
            return true;
        }

        private void ClearMainStreamRestoreVolume()
        {
            lock (_lock)
            {
                _hasMainRestoreVolume = false;
            }
        }

        private void FreePlaybackCoverStream()
        {
            int coverStream;
            lock (_lock)
            {
                coverStream = _coverStream;
                _coverStream = 0;
                System.Threading.Volatile.Write(ref _frozenAudio, null);
            }

            FreePlaybackCoverStream(coverStream);
        }

        private static void FreePlaybackCoverStream(int coverStream)
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
