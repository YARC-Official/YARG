#nullable enable
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using ManagedBass;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Settings;

namespace YARG.Audio.BASS
{
    /// <summary>
    /// Queues VOX samples so only one plays at a time.
    /// </summary>
    public sealed class BassVoxSampleChannel : VoxSampleChannel
    {
        private static BassVoxSampleChannel? _currentlyPlaying;
        private static readonly Queue<BassVoxSampleChannel> Queue = new();
        private static bool _queueActive;

        private readonly BassSamplePlayer _samplePlayer;
        private bool _disposed;

        internal static BassVoxSampleChannel? Create(VoxSample sample, string path, BassAudioRouter router,
            OutputChannel? outputChannel)
        {
            int handle = Bass.SampleLoad(path, 0, 0, 1, BassFlags.Default);
            if (handle == 0)
            {
                YargLogger.LogFormatError("Failed to load {0} {1}: {2}!", sample, path, Bass.LastError);
                return null;
            }

            return new BassVoxSampleChannel(handle, sample, path, router, outputChannel);
        }

        private BassVoxSampleChannel(int handle, VoxSample sample, string path, BassAudioRouter router,
            OutputChannel? outputChannel)
            : base(sample, path)
        {
            _samplePlayer = new BassSamplePlayer(router, handle, sample.ToString());
            SetOutputChannel_Internal(outputChannel);
            SetVolume_Internal(GlobalAudioHandler.GetTrueVolume(SongStem.VoxSample));
        }

        protected override void Play_Internal()
        {
            if (!SettingsManager.Settings.EnableVoxSamples.Value)
            {
                return;
            }

            if (IsAnyPlaying())
            {
                QueuePlayback(this);
                return;
            }

            _currentlyPlaying = this;
            if (!_samplePlayer.Play())
            {
                _currentlyPlaying = null;
            }
        }

        private static void QueuePlayback(BassVoxSampleChannel channel)
        {
            Queue.Enqueue(channel);
            if (!_queueActive)
            {
                PlayQueued();
            }
        }

        private static async void PlayQueued()
        {
            _queueActive = true;
            while (Queue.TryDequeue(out var channel))
            {
                await UniTask.WaitUntil(() => !IsAnyPlaying());
                if (!channel._disposed)
                {
                    channel.Play();
                }
            }
            _queueActive = false;
        }

        private static bool IsAnyPlaying()
        {
            return _currentlyPlaying?.IsPlaying() == true;
        }

        protected override void SetVolume_Internal(double volume)
        {
            _samplePlayer.SetVolume(volume * AudioHelpers.VoxSamples[(int) Sample].Volume);
        }

        protected override void SetOutputChannel_Internal(OutputChannel? channel)
        {
            _samplePlayer.SetOutputChannel(channel);
        }

        protected override bool IsPlaying_Internal()
        {
            return _samplePlayer.IsPlaying;
        }

        protected override void DisposeUnmanagedResources()
        {
            _disposed = true;
            if (_currentlyPlaying == this)
            {
                _currentlyPlaying = null;
            }
            _samplePlayer.Dispose();
        }
    }
}
