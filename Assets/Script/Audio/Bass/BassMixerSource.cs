#nullable enable
using System;
using System.Collections.Generic;
using ManagedBass;
using ManagedBass.Mix;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     Represents an individual audio channel plugged into a BASS mixer, managing its volume,
    ///     channel matrix/downmixing, and split stream creation.
    /// </summary>
    internal sealed class BassMixerSource : IDisposable
    {
        private readonly BassMixer _mixer;
        private readonly List<StreamHandle> _splits = new();

        private bool _disposed;

        internal BassMixerSource(BassMixer mixer, int handle)
        {
            _mixer = mixer;
            Handle = handle;
        }

        public int Handle { get; }

        public StreamHandle? CreateSplit(int[] indices)
        {
            if (_disposed)
            {
                return null;
            }

            int[]? channelMap = null;
            if (indices.Length > 0)
            {
                channelMap = new int[indices.Length + 1];
                Array.Copy(indices, channelMap, indices.Length);
                channelMap[^1] = -1;
            }

            const BassFlags FLAGS = BassFlags.Decode | BassFlags.SplitPosition;
            int splitHandle = BassMix.CreateSplitStream(Handle, FLAGS, channelMap);
            if (splitHandle == 0)
            {
                YargLogger.LogFormatError("Failed to create split stream: {0}", Bass.LastError);
                return null;
            }

            var split = new StreamHandle(splitHandle);
            _splits.Add(split);
            return split;
        }

        public (StreamHandle Stream, StreamHandle Reverb)? CreateSplitPair(int[] indices)
        {
            var stream = CreateSplit(indices);
            if (stream == null)
            {
                return null;
            }

            var reverb = CreateSplit(indices);
            if (reverb != null)
            {
                return (stream, reverb);
            }

            FreeSplit(stream);
            return null;
        }

        public void Release(StreamHandle stream)
        {
            if (!FreeSplit(stream))
            {
                return;
            }

            if (_splits.Count == 0)
            {
                Dispose();
            }
        }

        internal void SetDevice(int deviceId)
        {
            if (_disposed)
            {
                return;
            }

            foreach (var split in _splits)
            {
                BassX.Check(Bass.ChannelSetDevice(split.Stream, deviceId),
                    $"move split {split.Stream} to device {deviceId}");
            }

            BassX.Check(Bass.ChannelSetDevice(Handle, deviceId),
                $"move source {Handle} to device {deviceId}");
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            for (int i = _splits.Count - 1; i >= 0; i--)
            {
                _splits[i].Free();
            }

            _splits.Clear();
            BassX.Check(Bass.StreamFree(Handle), $"free source stream {Handle}");
            _mixer.RemoveSource(this);
        }

        private bool FreeSplit(StreamHandle stream)
        {
            if (!_splits.Remove(stream))
            {
                return false;
            }

            stream.Free();
            return true;
        }
    }

    internal readonly struct BassMixerChannel
    {
        public readonly int       Handle;
        public readonly float[,]? VolumeMatrix;
        public readonly double    DelaySeconds;

        public BassMixerChannel(int handle, float[,]? volumeMatrix = null, double delaySeconds = 0)
        {
            Handle = handle;
            VolumeMatrix = volumeMatrix;
            DelaySeconds = delaySeconds;
        }
    }
}
