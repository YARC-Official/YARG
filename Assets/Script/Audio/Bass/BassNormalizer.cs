#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ManagedBass;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     Calculates a normalization gain for songs by analyzing RMS levels.
    ///     Streams are cloned and mixed into a decode-only mixer for background analysis.
    ///     Gain is adjusted incrementally toward the target RMS using clamped relative updates,
    ///     ensuring smooth transitions rather than abrupt volume changes.
    /// </summary>
    public class BassNormalizer : IDisposable
    {
        // Target RMS to normalize to, typically results in around -14 LUFS
        private const float TARGET_RMS = 0.12f;

        // Low initial gain so it typically ramps up instead of ramps down
        internal const float INITIAL_GAIN = 0.3f;

        // Maximum allowed gain to prevent excessive loudness
        private const float MAX_GAIN = 1.5f;

        // The length in ms of the sliding window for RMS calculation
        private const int WINDOW_MS = 100;

        //Maximum per-window gain update, but ensuring that we can still hit max gain in a 2 minute long song
        private const float TWO_MINUTES_MS = 2 * 60 * 1000f;
        private const float MAX_GAIN_STEP  = (MAX_GAIN - INITIAL_GAIN) / (TWO_MINUTES_MS / WINDOW_MS);

        private const    int                     GAIN_CALC_SHUTDOWN_TIMEOUT_MS = 1000;

        private readonly Action<float>           _applyGain;
        private readonly List<Stream>            _streams = new();
        private          float                   _gain    = INITIAL_GAIN;
        private          CancellationTokenSource? _gainCalcCts;
        private          Task                    _gainCalcTask = Task.CompletedTask;

        private BassMixer? _mixer;

        public BassNormalizer(Action<float> applyGain)
        {
            _applyGain = applyGain;
        }

        public float Gain => Volatile.Read(ref _gain);

        /// <summary>
        ///     Adds a stream to the normalization mixer and restarts the background gain calculation.
        ///     Restarting updates with each added stream provides a head start on normalization before playback begins,
        ///     which is especially useful for modes like Practice where the mixer does not play immediately.
        /// </summary>
        public bool AddStream(Stream stream, params StemMixer.StemInfo[] stemInfos)
        {
            if (!StopGainCalculation())
            {
                YargLogger.LogError("Previous gain calculation did not stop; refusing to start another one.");
                return false;
            }

            var clonedStream = CloneStreamToMemory(stream);
            if (clonedStream == null)
            {
                YargLogger.LogError("Failed to clone stream!");
                return false;
            }

            _mixer ??= BassMixer.Create(44100, 2, BassFlags.Decode,
                GlobalAudioHandler.MAX_THREADS);
            if (_mixer == null)
            {
                clonedStream.Dispose();
                return false;
            }

            _streams.Add(clonedStream);
            if (!_mixer.AddStream(clonedStream, stemInfos))
            {
                _streams.Remove(clonedStream);
                clonedStream.Dispose();
                return false;
            }

            StartGainCalculation();
            return true;
        }

        public void Dispose()
        {
            // BASS calls cannot be interrupted mid-call. Do not free handles while the worker is still using them.
            if (!StopGainCalculation())
            {
                return;
            }

            _mixer?.Dispose();
            _mixer = null;

            foreach (var stream in _streams)
            {
                stream.Dispose();
            }

            _streams.Clear();
        }

        private static MemoryStream? CloneStreamToMemory(Stream original)
        {
            if (!original.CanRead || !original.CanSeek)
            {
                return null;
            }

            long originalPosition = original.Position;
            MemoryStream? clonedStream = null;
            try
            {
                original.Position = 0;
                clonedStream = new MemoryStream();
                original.CopyTo(clonedStream);
                clonedStream.Position = originalPosition;
                return clonedStream;
            }
            catch
            {
                clonedStream?.Dispose();
                return null;
            }
            finally
            {
                original.Position = originalPosition;
            }
        }

        private void StartGainCalculation()
        {
            _gainCalcCts = new CancellationTokenSource();
            var token = _gainCalcCts.Token;

            _gainCalcTask = Task.Factory.StartNew(() => RunGainCalculation(token), CancellationToken.None,
                TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void RunGainCalculation(CancellationToken token)
        {
            try
            {
                CalculateRms(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Expected shutdown.
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Gain calculation failed.");
            }
        }

        private bool StopGainCalculation()
        {
            if (_gainCalcCts == null)
            {
                return true;
            }

            _gainCalcCts.Cancel();

            if (!_gainCalcTask.Wait(GAIN_CALC_SHUTDOWN_TIMEOUT_MS))
            {
                YargLogger.LogError(
                    "Gain calculation did not stop during audio teardown; leaving its BASS handles intact.");
                return false;
            }

            _gainCalcCts.Dispose();
            _gainCalcCts = null;
            _gainCalcTask = Task.CompletedTask;
            return true;
        }

        private void CalculateRms(CancellationToken token)
        {
            var mixer = _mixer;
            if (mixer == null || !mixer.SetPositionBytes(0))
            {
                return;
            }

            double cumulativeSumSquares = 0.0;
            long totalSamples = 0;
            float windowSeconds = WINDOW_MS / 1000f;
            long samplesPerWindow = (long) (windowSeconds * mixer.SampleRate);
            long lastPosition = -1;

            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                if (!mixer.TryGetRms(windowSeconds, out float chunkedRms))
                {
                    break;
                }

                long currentPosition = Bass.ChannelGetPosition(mixer.Handle);
                if (currentPosition == lastPosition)
                {
                    break;
                }
                lastPosition = currentPosition;

                if (chunkedRms > 0)
                {
                    double sumSquares = chunkedRms * chunkedRms * samplesPerWindow;
                    cumulativeSumSquares += sumSquares;
                    totalSamples += samplesPerWindow;

                    double rms = Math.Sqrt(cumulativeSumSquares / totalSamples);
                    float targetGain = (float) Math.Min(MAX_GAIN, TARGET_RMS / rms);
                    float gain = Gain;
                    float delta = Math.Clamp(targetGain - gain, -MAX_GAIN_STEP, MAX_GAIN_STEP);
                    gain += delta;
                    Volatile.Write(ref _gain, gain);
                    _applyGain.Invoke(gain);
                }
            }
        }
    }
}
