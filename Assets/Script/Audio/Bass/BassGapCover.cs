using System;
using System.Diagnostics;
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
            public int HistoryMs   { get; set; } = 500;

            /// <summary>
            /// Amount of recent audio to replay for gap coverage, in milliseconds.
            /// </summary>
            public int CoverSourceMs { get; set; } = 120;

            /// <summary>
            /// Whether cover playback should synthesize a short granular pad instead of replaying history directly.
            /// </summary>
            public bool UseGranularCover { get; set; } = true;

            /// <summary>
            /// Grain length used by granular cover, in milliseconds.
            /// </summary>
            public int GrainMs { get; set; } = 28;

            /// <summary>
            /// Time between spawned grains used by granular cover, in milliseconds.
            /// </summary>
            public int GrainHopMs { get; set; } = 10;

            /// <summary>
            /// Recent-source range to choose grain starts from, in milliseconds.
            /// </summary>
            public int GrainJitterMs { get; set; } = 60;

            /// <summary>
            /// Gain applied to each granular voice to avoid clipping during overlaps.
            /// </summary>
            public float GrainGain { get; set; } = 0.55f;

            /// <summary>
            /// Duration of the fade from cover stream back to main stream, in milliseconds.
            /// </summary>
            public int CrossfadeMs { get; set; } = 10;

            /// <summary>
            /// Duration of the cover fade-out when using granular cover, in milliseconds.
            /// </summary>
            public int GranularCoverFadeOutMs { get; set; } = 14;

            /// <summary>
            /// Duration of the main fade-in when using granular cover, in milliseconds.
            /// </summary>
            public int GranularMainFadeInMs { get; set; } = 8;

            /// <summary>
            /// Number of volume steps used for the manual equal-gain crossfade.
            /// </summary>
            public int CrossfadeSteps { get; set; } = 8;

            /// <summary>
            /// Duration of the fade into cover playback, in milliseconds.
            /// </summary>
            public int CoverFadeInMs { get; set; } = 3;

            /// <summary>
            /// Amount of main stream data to prime before crossfading back, in milliseconds.
            /// </summary>
            public int PrimeMs     { get; set; } = 30;

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
        private float[] _grainEnvelope = Array.Empty<float>();
        private int     _coverPos;
        private int     _granularFramePos;
        private int     _grainSeed;

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
            }

            lock (_lock)
            {
                _coverSource = SnapshotHistoryLocked();
                _grainEnvelope = BuildGrainEnvelope();
                _coverPos = 0;
                _granularFramePos = 0;
                _grainSeed = unchecked(Environment.TickCount * 397) ^ _coverVersion;
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
            CancelCoverIfActive();

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
                _grainEnvelope = Array.Empty<float>();
                _coverPos = 0;
                _granularFramePos = 0;
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

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                if (_opt.UseGranularCover)
                {
                    FadeGranularCoverBack(coverVersion, coverStream, targetVolume);
                }
                else
                {
                    FadeCoverOut(coverVersion, coverStream, targetVolume, fadeMs, steps);
                    if (!IsCoverVersionCurrent(coverVersion))
                    {
                        return;
                    }

                    Bass.ChannelSetAttribute(coverStream, ChannelAttribute.Volume, 0f);
                    Bass.ChannelSetAttribute(_mainStream, ChannelAttribute.Volume, 0f);

                    FadeMainIn(coverVersion, targetVolume, fadeMs, steps);
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

        private void FadeGranularCoverBack(int coverVersion, int coverStream, float targetVolume)
        {
            int coverFadeMs = Math.Max(1, _opt.GranularCoverFadeOutMs);
            int mainFadeMs = Math.Max(1, _opt.GranularMainFadeInMs);
            int totalMs = Math.Max(coverFadeMs, mainFadeMs);
            int steps = Math.Max(1, _opt.CrossfadeSteps);

            var watch = Stopwatch.StartNew();
            for (int i = 1; i <= steps; ++i)
            {
                if (!IsCoverVersionCurrent(coverVersion))
                {
                    return;
                }

                int targetMs = i * totalMs / steps;
                int sleepMs = targetMs - (int) watch.ElapsedMilliseconds;
                if (sleepMs > 0)
                {
                    System.Threading.Thread.Sleep(sleepMs);
                }

                if (!IsCoverVersionCurrent(coverVersion))
                {
                    return;
                }

                double elapsedMs = watch.Elapsed.TotalMilliseconds;
                double mainT = SmoothStep(Math.Min(1.0, elapsedMs / mainFadeMs));
                double coverT = SmoothStep(Math.Min(1.0, elapsedMs / coverFadeMs));

                Bass.ChannelSetAttribute(_mainStream, ChannelAttribute.Volume, (float) (targetVolume * mainT));
                Bass.ChannelSetAttribute(coverStream, ChannelAttribute.Volume, (float) (targetVolume * (1.0 - coverT)));
            }
        }

        private void FadeCoverOut(int coverVersion, int coverStream, float targetVolume, int fadeMs, int steps)
        {
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

                double t = SmoothStep(Math.Min(1.0, watch.Elapsed.TotalMilliseconds / fadeMs));
                Bass.ChannelSetAttribute(coverStream, ChannelAttribute.Volume, (float) (targetVolume * (1.0 - t)));
            }
        }

        private void FadeMainIn(int coverVersion, float targetVolume, int fadeMs, int steps)
        {
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

                double t = SmoothStep(Math.Min(1.0, watch.Elapsed.TotalMilliseconds / fadeMs));
                Bass.ChannelSetAttribute(_mainStream, ChannelAttribute.Volume, (float) (targetVolume * t));
            }
        }

        private static double SmoothStep(double t)
        {
            return t * t * (3.0 - 2.0 * t);
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
                if (_opt.UseGranularCover)
                {
                    FillGranularCoverLocked(dst);
                }
                else
                {
                    int available = _coverSource.Length - _coverPos;
                    if (available > 0)
                    {
                        int copy = Math.Min(samples, available);
                        _coverSource.AsSpan(_coverPos, copy).CopyTo(dst);
                        _coverPos += copy;
                    }
                }
            }
            return length;
        }

        private void FillGranularCoverLocked(Span<float> dst)
        {
            int channels = Math.Max(1, _opt.Channels);
            int sourceFrames = _coverSource.Length / channels;
            int grainFrames = _grainEnvelope.Length;
            if (sourceFrames == 0 || grainFrames == 0)
            {
                return;
            }

            int hopFrames = Math.Max(1, _opt.SampleRate * _opt.GrainHopMs / 1000);
            int jitterFrames = Math.Max(0, _opt.SampleRate * _opt.GrainJitterMs / 1000);
            int frameCount = dst.Length / channels;
            for (int frame = 0; frame < frameCount; ++frame)
            {
                int absoluteFrame = _granularFramePos + frame;
                int grainIndex = absoluteFrame / hopFrames;
                for (int grain = grainIndex - 3; grain <= grainIndex; ++grain)
                {
                    if (grain < 0)
                    {
                        continue;
                    }

                    int localFrame = absoluteFrame - grain * hopFrames;
                    if ((uint) localFrame >= (uint) grainFrames)
                    {
                        continue;
                    }

                    int sourceFrame = GetGrainSourceFrame(grain, localFrame, sourceFrames, grainFrames, jitterFrames);
                    float gain = _grainEnvelope[localFrame] * _opt.GrainGain;
                    int dstOffset = frame * channels;
                    int sourceOffset = sourceFrame * channels;
                    for (int channel = 0; channel < channels; ++channel)
                    {
                        dst[dstOffset + channel] += _coverSource[sourceOffset + channel] * gain;
                    }
                }
            }

            _granularFramePos += frameCount;
        }

        private int GetGrainSourceFrame(int grain, int localFrame, int sourceFrames, int grainFrames, int jitterFrames)
        {
            int latestStart = Math.Max(0, sourceFrames - grainFrames);
            int earliestStart = Math.Max(0, latestStart - jitterFrames);
            int startRange = latestStart - earliestStart + 1;
            int start = earliestStart;
            if (startRange > 1)
            {
                start += PositiveHash(_grainSeed + grain * 1103515245) % startRange;
            }

            return Math.Min(sourceFrames - 1, start + localFrame);
        }

        private float[] BuildGrainEnvelope()
        {
            int grainFrames = Math.Max(1, _opt.SampleRate * _opt.GrainMs / 1000);
            var envelope = new float[grainFrames];
            if (grainFrames == 1)
            {
                envelope[0] = 1f;
                return envelope;
            }

            for (int i = 0; i < envelope.Length; ++i)
            {
                envelope[i] = (float) (0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / (grainFrames - 1)));
            }

            return envelope;
        }

        private static int PositiveHash(int value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= 0x7feb352d;
                value ^= value >> 15;
                value *= unchecked((int) 0x846ca68b);
                value ^= value >> 16;
                return value & 0x7fffffff;
            }
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

        private bool CancelCoverIfActive()
        {
            int coverStream;
            int slideSyncHandle;
            float volume;
            lock (_lock)
            {
                if (!_hasMainRestoreVolume)
                {
                    return false;
                }

                ++_coverVersion;
                coverStream = _coverStream;
                slideSyncHandle = _slideSyncHandle;
                volume = _mainRestoreVolume;
                _coverStream = 0;
                _slideSyncHandle = 0;
                _coverSource = Array.Empty<float>();
                _grainEnvelope = Array.Empty<float>();
                _coverPos = 0;
                _granularFramePos = 0;
                _hasMainRestoreVolume = false;
                _capturing = true;
            }

            Bass.ChannelSetAttribute(_mainStream, ChannelAttribute.Volume, volume);
            FreeCoverStream(coverStream, slideSyncHandle);
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
            int slideSyncHandle;
            lock (_lock)
            {
                coverStream = _coverStream;
                slideSyncHandle = _slideSyncHandle;
                _coverStream = 0;
                _slideSyncHandle = 0;
                _coverSource = Array.Empty<float>();
                _grainEnvelope = Array.Empty<float>();
                _coverPos = 0;
                _granularFramePos = 0;
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
