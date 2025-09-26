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
        private          int              _mixer;
        private          BassAudioManager _manager;
        private const    int              SAMPLE_COUNT    = 256 * 1024;
        private const    float            TARGET_RMS      = 0.15f; // Usually ends up ~ -14 LUFS
        private const    float            MAX_GAIN        = 1.3f;
        private readonly List<Stream>     _streams        = new();
        private readonly List<int>        _handles = new();

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
            _streams.Add(clonedStream);

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

            return true;
        }

        public void CalculateGain(Action<float> onGain)
        {
            var progress = new Progress<double>(gain => onGain?.Invoke((float) gain));
            Task.Run(() => CalculateRms(progress));
        }

        private bool CreateMixer(out int mixerHandle)
        {
            mixerHandle = BassMix.CreateMixerStream(44100, 2, BassFlags.Decode | BassFlags.Float);
            if (mixerHandle == 0)
            {
                YargLogger.LogFormatError("Failed to create mixer: {0}!", Bass.LastError);
                return false;
            }

            return true;
        }

        private static bool CloneStreamToMemory(Stream original, out MemoryStream clonedStream)
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

        private void CalculateRms(IProgress<Double> progress)
        {
            const float targetRms = 0.15f;
            const float maxGainFactor = 1.3f;

            double cumulativeSumSquares = 0.0;
            long totalSamples = 0;
            foreach (var audioSamples in ReadAudioSamples())
            {
                long bufferBytes = audioSamples.Length * sizeof(float);
                var bufferSeconds = Bass.ChannelBytes2Seconds(_mixer, bufferBytes);

                float[] level = new float[1];
                bool status = Bass.ChannelGetLevel(_mixer, level, (float) bufferSeconds,
                    LevelRetrievalFlags.Mono | LevelRetrievalFlags.RMS);

                var chunkedRms = level[0];
                if (status && chunkedRms > 0)
                {
                    double sumSquares = chunkedRms * chunkedRms;
                    cumulativeSumSquares += sumSquares * audioSamples.Length;
                    totalSamples += audioSamples.Length;
                }
                else
                {
                    continue;
                }

                double rms = Math.Sqrt(cumulativeSumSquares / totalSamples);
                double gain = Math.Min(maxGainFactor, targetRms / rms);
                progress?.Report(gain);
            }
        }

        private IEnumerable<float[]> ReadAudioSamples()
        {
            Bass.ChannelSetPosition(_mixer, 0);
            float[] buffer = new float[SAMPLE_COUNT];
            int data;
            while ((data = Bass.ChannelGetData(_mixer, buffer, buffer.Length * sizeof(float))) > 0)
            {
                var numSamplesRead = data / sizeof(float);
                if (numSamplesRead == buffer.Length)
                {
                    yield return buffer;
                    buffer = new float[SAMPLE_COUNT];
                }
                else
                {
                    var samples = new float[numSamplesRead];
                    Array.Copy(buffer, samples, numSamplesRead);
                    yield return samples;
                }
            }

            Bass.ChannelSetPosition(_mixer, 0);
        }

        public void Dispose()
        {
            if (_mixer != 0)
            {
                if (!Bass.StreamFree(_mixer))
                {
                    YargLogger.LogFormatError("Failed to free mixer stream (THIS WILL LEAK MEMORY!): {0}!",
                        Bass.LastError);
                }
            }
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