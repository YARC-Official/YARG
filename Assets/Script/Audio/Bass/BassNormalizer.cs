using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ManagedBass;
using ManagedBass.Fx;
using ManagedBass.Mix;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    /// Calculates a normalization gain for songs by analyzing RMS levels.
    /// Streams are cloned and mixed into a decode-only mixer for background analysis.
    /// Gain is adjusted incrementally toward the target RMS using clamped relative updates,
    /// ensuring smooth transitions rather than abrupt volume changes.
    /// </summary>
    public class BassNormalizer : IDisposable
    {
        // Target RMS to normalize to, typically results in around -14 LUFS
        private const float TARGET_RMS         = 0.12f;

        // Low initial gain so it typically ramps up instead of ramps down
        private const float INITIAL_GAIN       = 0.3f;

        // Maximum allowed gain to prevent excessive loudness
        private const float MAX_GAIN           = 1.5f;

        // The length in ms of the sliding window for RMS calculation
        private const int   WINDOW_MS          = 100;

        //Maximum per-window gain update, but ensuring that we can still hit max gain in a 2 minute long song
        private const float MAX_GAIN_STEP      = (MAX_GAIN - INITIAL_GAIN) / (TWO_MINUTES_MS / WINDOW_MS);
        private const float TWO_MINUTES_MS = 2 * 60 * 1000f;

        // Undocumented BASS attribute to set max processing threads for a mixer
        private const int   MAX_THREADS_ATTRIB = 86017;

        private          int                     _mixer;
        private readonly List<Stream>            _streams = new();
        private readonly List<int>               _handles = new();
        private          CancellationTokenSource _gainCalcCts = new();
        public float               Gain { get; private set; } = INITIAL_GAIN;
        public event Action<float> OnGainAdjusted;

        /// <summary>
        /// Adds a stream to the normalization mixer and restarts the background gain calculation.
        /// Restarting updates with each added stream provides a head start on normalization before playback begins,
        /// which is especially useful for modes like Practice where the mixer does not play immediately.
        /// </summary>
        public bool AddStream(Stream stream, params StemMixer.StemInfo[] stemInfos)
        {
            if (_mixer == 0)
            {
                if (!CreateMixer(out _mixer))
                {
                    return false;
                }
            }

            if (!CloneStreamToMemory(stream, out var clonedStream))
            {
                YargLogger.LogError("Failed to clone stream!");
                return false;
            }

            if (!BassAudioManager.CreateSourceStream(clonedStream, out int sourceStream))
            {
                YargLogger.LogFormatError("Failed to load stem source stream: {0}!", Bass.LastError);
                return false;
            }
            _handles.Add(sourceStream);

            foreach (var stemInfo in stemInfos)
            {
                var volumeMatrix = stemInfo.GetVolumeMatrix();
                if (volumeMatrix != null)
                {
                    int[] channelMap = stemInfo.Indices.Append(-1).ToArray();
                    int streamSplit = BassMix.CreateSplitStream(sourceStream, BassFlags.Decode, channelMap);
                    if (streamSplit == 0)
                    {
                        YargLogger.LogFormatError("Failed to create split stream: {0}!", Bass.LastError);
                        return false;
                    }
                    _handles.Add(streamSplit);

                    if (!BassMix.MixerAddChannel(_mixer, streamSplit, BassFlags.MixerChanMatrix))
                    {
                        Bass.StreamFree(streamSplit);
                        YargLogger.LogFormatError("Failed to add channel {0} to mixer: {1}!", stemInfo.Stem,
                            Bass.LastError);
                        return false;
                    }

                    if (!BassMix.ChannelSetMatrix(streamSplit, volumeMatrix))
                    {
                        YargLogger.LogFormatError("Failed to set {stem} matrices: {0}!", stemInfo.Stem, Bass.LastError);
                        return false;
                    }
                }
                else
                {
                    if (!BassMix.MixerAddChannel(_mixer, sourceStream, BassFlags.Default))
                    {
                        YargLogger.LogFormatError("Failed to add channel {0} to mixer: {1}!", stemInfo.Stem,
                            Bass.LastError);
                        return false;
                    }
                }
            }

            StartGainCalculation();
            return true;
        }

        private bool CreateMixer(out int mixerHandle)
        {
            mixerHandle = BassMix.CreateMixerStream(44100, 2, BassFlags.Decode);
            if (mixerHandle == 0)
            {
                YargLogger.LogFormatError("Failed to create mixer: {0}!", Bass.LastError);
                return false;
            }

            if (!Bass.ChannelSetAttribute(mixerHandle, (ChannelAttribute) MAX_THREADS_ATTRIB, GlobalAudioHandler.MAX_THREADS))
            {
                YargLogger.LogFormatError("Failed to set mixer processing threads: {0}!", Bass.LastError);
            }

            _handles.Add(mixerHandle);
            return true;
        }

        private bool CloneStreamToMemory(Stream original, out MemoryStream clonedStream)
        {
            clonedStream = null;
            if (!original.CanRead || !original.CanSeek)
                return false;

            var originalPosition = original.Position;
            try
            {
                original.Position = 0;
                clonedStream = new MemoryStream();
                original.CopyTo(clonedStream);
                clonedStream.Position = originalPosition;
                _streams.Add(clonedStream);
                return true;
            }
            catch
            {
                clonedStream?.Dispose();
                clonedStream = null;
                return false;
            }
            finally
            {
                original.Position = originalPosition;
            }
        }

        private void StartGainCalculation()
        {
            _gainCalcCts.Cancel();
            _gainCalcCts.Dispose();
            _gainCalcCts = new CancellationTokenSource();

            var progress = new Progress<double>(gain =>
            {
                OnGainAdjusted?.Invoke((float) gain);
            });

            Task.Run(() => CalculateRms(progress, _gainCalcCts.Token), _gainCalcCts.Token);
        }

        private void CalculateRms(IProgress<double> progress, CancellationToken token)
        {
            double cumulativeSumSquares = 0.0;
            long totalSamples = 0;
            Bass.ChannelSetPosition(_mixer, 0);
            var info = Bass.ChannelGetInfo(_mixer);
            float windowSeconds = WINDOW_MS / 1000f;
            long samplesPerWindow = (long) (windowSeconds * info.Frequency);
            float[] level = new float[1];

            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                bool didGetLevel = Bass.ChannelGetLevel(_mixer, level, windowSeconds,
                    LevelRetrievalFlags.Mono | LevelRetrievalFlags.RMS);

                if (!didGetLevel)
                {
                    break;
                }

                var chunkedRms = level[0];
                if (chunkedRms > 0)
                {
                    double sumSquares = chunkedRms * chunkedRms * samplesPerWindow;
                    cumulativeSumSquares += sumSquares;
                    totalSamples += samplesPerWindow;

                    double rms = Math.Sqrt(cumulativeSumSquares / totalSamples);
                    float targetGain = (float) Math.Min(MAX_GAIN, TARGET_RMS / rms);
                    float delta = Math.Clamp(targetGain - Gain, -MAX_GAIN_STEP, MAX_GAIN_STEP);
                    Gain += delta;
                    progress?.Report(Gain);
                }
            }
        }

        public void Dispose()
        {
            _gainCalcCts.Cancel();
            _gainCalcCts.Dispose();

            foreach (var stream in _streams)
            {
                stream.Dispose();
            }
            foreach (var handle in _handles)
            {
                if (!Bass.StreamFree(handle))
                {
                    if (Bass.LastError != Errors.Handle)
                    {
                        YargLogger.LogFormatError("Failed to free stream (THIS WILL LEAK MEMORY!): {0}!",
                            Bass.LastError);
                    }
                }
            }
            _mixer = 0;
            _streams.Clear();
            _handles.Clear();
        }
    }
}