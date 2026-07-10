using System;
using System.Diagnostics;
using System.Threading;
using ManagedBass;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    /// Hides brief audio dropouts when changing a playing BASS stream, such as seeking.
    /// <para>
    /// While audio plays, this keeps a short recording of what was heard. <see cref="CoverPlaybackGap(Func{int})"/>
    /// plays that recent audio from a temporary stream while the action, such as a seek, changes the main stream.
    /// It then fades back to the main stream.
    /// </para>
    /// <para>
    /// Only one cover can run at once. A newer request stops current cover and changes stream normally, so latest stream
    /// change always wins.
    /// </para>
    /// </summary>
    public sealed class BassPlaybackGapCover : IDisposable
    {
        // Three perceptual tuning values. Other timings derive from these so they cannot drift apart.
        private const int HISTORY_MS = 500;
        private const int COVER_SOURCE_MS = 300;
        private const int CROSSFADE_MS = 20;

        private enum CoverState
        {
            CAPTURING,
            COVERING,
            CROSSFADING,
            DISPOSED,
        }

        private sealed class FrozenAudio
        {
            public readonly float[] Samples;

            // Only BASS cover callback advances this after session publication.
            public int ReadSamplePosition;

            public FrozenAudio(float[] samples, int readSamplePosition)
            {
                Samples = samples;
                ReadSamplePosition = readSamplePosition;
            }
        }

        private sealed class PlaybackCover
        {
            public readonly int Generation;
            public readonly int Stream;
            public readonly float MainRestoreVolume;
            public readonly FrozenAudio Audio;

            public PlaybackCover(int generation, int stream, float mainRestoreVolume, FrozenAudio audio)
            {
                Generation = generation;
                Stream = stream;
                MainRestoreVolume = mainRestoreVolume;
                Audio = audio;
            }
        }

        private readonly int     _mainStream;
        private readonly int     _sampleRate;
        private readonly int     _channels;
        private readonly object             _lock = new();
        private readonly object             _coverOperationLock = new();
        private readonly AudioHistoryBuffer _audioHistory;
        private readonly StreamProcedure    _coverCallback;

        // Protected by _lock. Callback reads use Volatile.Read and never take this lock.
        private PlaybackCover _activeCover;
        private int _coverGeneration;
        private int _state = (int) CoverState.CAPTURING;
        private int _dspHandle;
        private volatile bool _capturing = true;

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
            _audioHistory = new AudioHistoryBuffer(capacity);

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
        /// <returns>Gap cover bound to supplied stream.</returns>
        public static BassPlaybackGapCover CreateForChannel(int stream)
        {
            var info = Bass.ChannelGetInfo(stream);
            int sampleRate = info.Frequency > 0 ? info.Frequency : 44100;
            int channels = info.Channels > 0 ? info.Channels : 2;
            return new BassPlaybackGapCover(stream, sampleRate, channels);
        }

        /// <summary>
        /// Runs an action while replaying recent audio to cover any short playback gap it causes.
        /// Action must leave main stream in desired playback state.
        /// </summary>
        /// <param name="action">Function that may interrupt and update main stream.</param>
        /// <returns>Function result, or a BASS error code if cover playback could not start.</returns>
        public int CoverPlaybackGap(Func<int> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            lock (_coverOperationLock)
            {
                if (!Enabled || IsDisposed || _dspHandle == 0)
                {
                    return action();
                }

                // Do not stack stale delayed audio. Latest operation runs uncovered after cancellation.
                if (CancelActiveCover())
                {
                    return action();
                }

                if (!TryStartCover(out PlaybackCover cover, out int error))
                {
                    action();
                    return error;
                }

                return RunCoveredAction(action, cover);
            }
        }

        // Cover lifecycle

        private bool TryStartCover(out PlaybackCover cover, out int error)
        {
            FrozenAudio audio = CreateFrozenCoverAudio();
            Bass.ChannelGetAttribute(_mainStream, ChannelAttribute.Volume, out float mainStreamVolume);

            int coverStream = CreateCoverStreamOnMainDevice();
            if (coverStream == 0)
            {
                error = (int) Bass.LastError;
                YargLogger.LogFormatError("Failed to create/play cover stream: {0}", Bass.LastError);
                ResumeCapture();
                cover = null;
                return false;
            }

            if (!Bass.ChannelSetAttribute(coverStream, ChannelAttribute.Volume, 0f))
            {
                error = (int) Bass.LastError;
                YargLogger.LogFormatError("Failed to initialize gap cover volume: {0}", Bass.LastError);
                FreeCoverStream(coverStream);
                ResumeCapture();
                cover = null;
                return false;
            }

            cover = PublishCover(coverStream, mainStreamVolume, audio);
            // Publish before play: BASS may invoke cover callback immediately after ChannelPlay.
            if (!Bass.ChannelPlay(coverStream))
            {
                error = (int) Bass.LastError;
                YargLogger.LogFormatError("Failed to create/play cover stream: {0}", Bass.LastError);
                RestoreMainStreamAndFreeCover(DetachActiveCover(CoverState.CAPTURING, cover.Generation, true));
                cover = null;
                return false;
            }

            Bass.ChannelUpdate(coverStream, 0);
            if (!Bass.ChannelSetAttribute(_mainStream, ChannelAttribute.Volume, 0f))
            {
                error = (int) Bass.LastError;
                YargLogger.LogFormatError("Failed to mute main stream for gap cover: {0}", Bass.LastError);
                RestoreMainStreamAndFreeCover(DetachActiveCover(CoverState.CAPTURING, cover.Generation, true));
                cover = null;
                return false;
            }

            int coverFadeInMs = Math.Max(1, CROSSFADE_MS / 3);
            Bass.ChannelSlideAttribute(coverStream, ChannelAttribute.Volume, mainStreamVolume, coverFadeInMs);
            error = 0;
            return true;
        }

        private FrozenAudio CreateFrozenCoverAudio()
        {
            lock (_lock)
            {
                // DSP callback must not lock. Stop capture before taking lock-free history snapshot.
                SetStateLocked(CoverState.COVERING);
                float[] audioSamples = FreezeRecentAudio();
                int audioFrames = audioSamples.Length / _channels;
                int tailFrames = Math.Max(1, _sampleRate * COVER_SOURCE_MS * 2 / 3_000);
                int readSamplePosition = Math.Max(0, audioFrames - tailFrames) * _channels;
                return new FrozenAudio(audioSamples, readSamplePosition);
            }
        }

        private PlaybackCover PublishCover(int coverStream, float mainStreamVolume, FrozenAudio audio)
        {
            lock (_lock)
            {
                int generation = ++_coverGeneration;
                var cover = new PlaybackCover(generation, coverStream, mainStreamVolume, audio);
                Volatile.Write(ref _activeCover, cover);
                SetStateLocked(CoverState.COVERING);
                return cover;
            }
        }

        private int RunCoveredAction(Func<int> action, PlaybackCover cover)
        {
            try
            {
                int result = action();
                StartCrossfadeToMainStream(cover);
                return result;
            }
            catch
            {
                RestoreMainStreamAndFreeCover(DetachActiveCover(CoverState.CAPTURING, cover.Generation, true));
                throw;
            }
        }

        // Crossfade lifecycle

        private void StartCrossfadeToMainStream(PlaybackCover cover)
        {
            int primeMs = Math.Max(1, COVER_SOURCE_MS / 2);
            Bass.ChannelUpdate(_mainStream, primeMs);

            lock (_lock)
            {
                if (!IsCurrentCoverLocked(cover.Generation))
                {
                    return;
                }

                SetStateLocked(CoverState.CROSSFADING);
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                CrossfadeToMainStream(cover);
                FinishPlaybackCover(cover);
            });
        }

        private void CrossfadeToMainStream(PlaybackCover cover)
        {
            int fadeMs = Math.Max(1, CROSSFADE_MS);
            int steps = Math.Min(8, fadeMs);
            var watch = Stopwatch.StartNew();
            for (int i = 1; i <= steps; ++i)
            {
                if (!IsCurrentCrossfade(cover.Generation))
                {
                    return;
                }

                int targetMs = i * fadeMs / steps;
                int sleepMs = targetMs - (int) watch.ElapsedMilliseconds;
                if (sleepMs > 0)
                {
                    Thread.Sleep(sleepMs);
                }

                if (!IsCurrentCrossfade(cover.Generation))
                {
                    return;
                }

                double t = Math.Min(1.0, watch.Elapsed.TotalMilliseconds / fadeMs);
                double fadeAngle = t * Math.PI / 2.0;
                float mainVolume = (float) (cover.MainRestoreVolume * Math.Sin(fadeAngle));
                float coverVolume = (float) (cover.MainRestoreVolume * Math.Cos(fadeAngle));
                Bass.ChannelSetAttribute(_mainStream, ChannelAttribute.Volume, mainVolume);
                Bass.ChannelSetAttribute(cover.Stream, ChannelAttribute.Volume, coverVolume);
            }
        }

        private void FinishPlaybackCover(PlaybackCover cover)
        {
            PlaybackCover detachedCover = DetachActiveCover(CoverState.CAPTURING, cover.Generation);
            if (detachedCover == null)
            {
                return;
            }

            // Snap volume: BASS may otherwise leave it mid-slide after rapid cancellation/restart.
            RestoreMainStreamAndFreeCover(detachedCover);
        }

        private bool IsCurrentCrossfade(int generation)
        {
            lock (_lock)
            {
                return (CoverState) _state == CoverState.CROSSFADING && IsCurrentCoverLocked(generation);
            }
        }

        // Cancellation and disposal

        private bool CancelActiveCover()
        {
            CoverState nextState = IsDisposed ? CoverState.DISPOSED : CoverState.CAPTURING;
            PlaybackCover cover = DetachActiveCover(nextState, invalidate: true);
            if (cover == null)
            {
                return false;
            }

            RestoreMainStreamAndFreeCover(cover);
            return true;
        }

        /// <summary>
        /// Stops cover playback and removes DSP hooks from main stream.
        /// </summary>
        public void Dispose()
        {
            lock (_coverOperationLock)
            {
                if (IsDisposed)
                {
                    return;
                }

                RestoreMainStreamAndFreeCover(DetachActiveCover(CoverState.DISPOSED, invalidate: true));

                if (_dspHandle != 0)
                {
                    Bass.ChannelRemoveDSP(_mainStream, _dspHandle);
                    _dspHandle = 0;
                }
            }
        }

        // Detaches managed ownership before any BASS call; BASS callbacks may block or re-enter.
        private PlaybackCover DetachActiveCover(CoverState nextState, int expectedGeneration = -1, bool invalidate = false)
        {
            lock (_lock)
            {
                PlaybackCover cover = _activeCover;
                if (cover == null || (expectedGeneration >= 0 && cover.Generation != expectedGeneration))
                {
                    if (nextState == CoverState.DISPOSED)
                    {
                        SetStateLocked(CoverState.DISPOSED);
                    }

                    return null;
                }

                Volatile.Write(ref _activeCover, null);
                if (invalidate)
                {
                    ++_coverGeneration;
                }

                SetStateLocked(nextState);
                return cover;
            }
        }

        private void RestoreMainStreamAndFreeCover(PlaybackCover cover)
        {
            if (cover == null)
            {
                return;
            }

            Bass.ChannelSetAttribute(_mainStream, ChannelAttribute.Volume, cover.MainRestoreVolume);
            FreeCoverStream(cover.Stream);
        }

        private void ResumeCapture()
        {
            lock (_lock)
            {
                if (!IsDisposed)
                {
                    SetStateLocked(CoverState.CAPTURING);
                }
            }
        }

        private bool IsDisposed => Volatile.Read(ref _state) == (int) CoverState.DISPOSED;

        private bool IsCurrentCoverLocked(int generation)
        {
            PlaybackCover cover = _activeCover;
            return cover != null && cover.Generation == generation;
        }

        private void SetStateLocked(CoverState state)
        {
            Volatile.Write(ref _state, (int) state);
            _capturing = state == CoverState.CAPTURING;
        }

        // BASS stream helpers

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

        private static void FreeCoverStream(int coverStream)
        {
            if (coverStream == 0)
            {
                return;
            }

            Bass.ChannelStop(coverStream);
            Bass.StreamFree(coverStream);
        }

        // BASS callbacks

        /// <summary>
        /// BASS DSP callback. Copies output float samples into history ring on BASS audio thread.
        /// Publishes ring positions after writes without locking; snapshots may contain a partially updated sample rather than
        /// blocking audio playback.
        /// </summary>
        private unsafe void OnDsp(int handle, int channel, IntPtr buffer, int length, IntPtr user)
        {
            if (!_capturing || IsDisposed)
            {
                return;
            }

            // BASS supplies byte length; history stores interleaved float samples.
            int samples = length / sizeof(float);
            if (samples <= 0)
            {
                return;
            }

            // Wrap BASS-owned buffer without allocating or copying before ring write.
            var src = new ReadOnlySpan<float>((void*) buffer, samples);
            _audioHistory.Write(src);
        }

        private unsafe int OnCoverStream(int handle, IntPtr buffer, int length, IntPtr user)
        {
            int samples = length / sizeof(float);
            var dst = new Span<float>((void*) buffer, samples);
            dst.Clear();

            PlaybackCover cover = Volatile.Read(ref _activeCover);
            if (cover == null || cover.Stream != handle)
            {
                return length;
            }

            FillFrozenAudio(dst, cover.Audio);
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
            int coverSamples = Math.Max(1, _sampleRate * _channels * COVER_SOURCE_MS / 1000);
            return _audioHistory.CopyLast(coverSamples);
        }

        /// <summary>
        /// Stores recent interleaved audio samples in a circular buffer for non-blocking snapshots.
        /// </summary>
        private sealed class AudioHistoryBuffer
        {
            private readonly float[] _samples;

            // Writer publishes positions after samples. Readers may include a partially updated sample rather than block audio.
            private int _writePosition;
            private int _recordedSampleCount;

            public AudioHistoryBuffer(int capacity)
            {
                _samples = new float[capacity];
            }

            /// <summary>
            /// Appends samples, retaining only newest samples when input exceeds capacity.
            /// Called only from BASS DSP callback.
            /// </summary>
            public void Write(ReadOnlySpan<float> source)
            {
                int capacity = _samples.Length;
                if (source.Length >= capacity)
                {
                    source[^capacity..].CopyTo(_samples);
                    Volatile.Write(ref _writePosition, 0);
                    Volatile.Write(ref _recordedSampleCount, capacity);
                    return;
                }

                int writePosition = Volatile.Read(ref _writePosition);
                int firstPartLength = Math.Min(source.Length, capacity - writePosition);
                source[..firstPartLength].CopyTo(_samples.AsSpan(writePosition, firstPartLength));

                int secondPartLength = source.Length - firstPartLength;
                if (secondPartLength > 0)
                {
                    source[firstPartLength..].CopyTo(_samples.AsSpan(0, secondPartLength));
                }

                int nextWritePosition = (writePosition + source.Length) % capacity;
                Volatile.Write(ref _writePosition, nextWritePosition);

                int recordedSampleCount = Volatile.Read(ref _recordedSampleCount);
                int newRecordedSampleCount = Math.Min(capacity, recordedSampleCount + source.Length);
                Volatile.Write(ref _recordedSampleCount, newRecordedSampleCount);
            }

            /// <summary>
            /// Returns up to <paramref name="maximumSampleCount"/> newest samples in playback order.
            /// Does not block writer; returned data can contain a partially written sample.
            /// </summary>
            public float[] CopyLast(int maximumSampleCount)
            {
                int recordedSampleCount = Volatile.Read(ref _recordedSampleCount);
                int sampleCount = Math.Min(maximumSampleCount, Math.Min(recordedSampleCount, _samples.Length));
                if (sampleCount <= 0)
                {
                    return Array.Empty<float>();
                }

                var copiedSamples = new float[sampleCount];
                int writePosition = Volatile.Read(ref _writePosition);
                int startPosition = (writePosition - sampleCount + _samples.Length) % _samples.Length;

                int firstPartLength = Math.Min(sampleCount, _samples.Length - startPosition);
                Array.Copy(_samples, startPosition, copiedSamples, 0, firstPartLength);

                int secondPartLength = sampleCount - firstPartLength;
                if (secondPartLength > 0)
                {
                    Array.Copy(_samples, 0, copiedSamples, firstPartLength, secondPartLength);
                }

                return copiedSamples;
            }
        }
    }
}
