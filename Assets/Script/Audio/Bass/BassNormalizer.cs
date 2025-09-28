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
    public class BassNormalizer : IDisposable
    {
        private const int   SAMPLE_COUNT            = 256 * 1024;
        private const float TARGET_RMS              = 0.175f;
        private const float MAX_GAIN                = 1.5f;

        private          int                     _mixer;
        private readonly List<Stream>            _streams = new();
        private readonly List<int>               _handles = new();
        private          CancellationTokenSource _gainCalcCts = new();

        public float               Gain { get; private set; } = 1.0f;
        public event Action<float> OnGainAdjusted;

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
                var volumeMatrix = stemInfo.VolumeMatrix;
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

            Task.Run(() => CalculateRms(progress), _gainCalcCts.Token);
        }

        private void CalculateRms(IProgress<Double> progress)
        {
            double cumulativeSumSquares = 0.0;
            long totalSamples = 0;
            foreach (var audioBytes in ReadAudioBytes())
            {
                var bufferSeconds = Bass.ChannelBytes2Seconds(_mixer, audioBytes.Length);
                float[] level = new float[1];
                bool didGetLevel = Bass.ChannelGetLevel(_mixer, level, (float) bufferSeconds,
                    LevelRetrievalFlags.Mono | LevelRetrievalFlags.RMS);

                var chunkedRms = level[0];
                if (didGetLevel && chunkedRms > 0)
                {
                    long numSamples = audioBytes.Length / sizeof(short);
                    double sumSquares = chunkedRms * chunkedRms * numSamples;
                    cumulativeSumSquares += sumSquares;
                    totalSamples += numSamples;
                }
                else
                {
                    continue;
                }

                double rms = Math.Sqrt(cumulativeSumSquares / totalSamples);
                double gain = Math.Min(MAX_GAIN, TARGET_RMS / rms);
                Gain = (float) gain;
                progress?.Report(gain);
            }
        }

        private IEnumerable<byte[]> ReadAudioBytes()
        {
            Bass.ChannelSetPosition(_mixer, 0);
            byte[] buffer = new byte[SAMPLE_COUNT * sizeof(short)];
            int data;
            while ((data = Bass.ChannelGetData(_mixer, buffer, buffer.Length)) > 0)
            {
                if (data == buffer.Length)
                {
                    yield return buffer;
                    buffer = new byte[SAMPLE_COUNT * sizeof(short)];
                }
                else
                {
                    var bytes = new byte[data];
                    Array.Copy(buffer, bytes, data);
                    yield return bytes;
                }
            }

            Bass.ChannelSetPosition(_mixer, 0);
        }

        public void Dispose()
        {
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