using System;
using ManagedBass;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    public sealed class BassSampleChannel : SampleChannel
    {
        private readonly bool             _canLoop;
        private readonly BassSamplePlayer _samplePlayer;

#nullable enable
        private BassSampleChannel(int handle, SfxSample sample, string path, BassAudioRouter router,
            OutputChannel? outputChannel, bool canLoop) : base(sample, path)
#nullable disable
        {
            _canLoop = canLoop;
            _samplePlayer = new BassSamplePlayer(router, handle, sample.ToString(), OnPlaybackEnded);
            SetOutputChannel_Internal(outputChannel);
            SetVolume_Internal(GlobalAudioHandler.GetTrueVolume(SongStem.Sfx));
        }
#nullable enable
        internal static BassSampleChannel? Create(SfxSample sample, string path, BassAudioRouter router,
            OutputChannel? outputChannel, bool loop = false)
#nullable disable
        {
            int handle = Bass.SampleLoad(path, 0, 0, 1, BassFlags.Default);
            if (handle == 0)
            {
                YargLogger.LogFormatError("Failed to load {0} {1}: {2}!", sample, path, Bass.LastError);
                return null;
            }

            var info = new SampleInfo();
            if (Bass.SampleGetInfo(handle, info))
            {
                info.MinGap = (int) Math.Round(PLAYBACK_SUPPRESS_THRESHOLD * 1000);
                if (!Bass.SampleSetInfo(handle, info))
                {
                    YargLogger.LogFormatError("Failed to set {0} sample playback gap: {1}!", sample, Bass.LastError);
                }
            }
            else
            {
                YargLogger.LogFormatError("Failed to get {0} sample info: {1}!", sample, Bass.LastError);
            }

            return new BassSampleChannel(handle, sample, path, router, outputChannel, loop);
        }

        protected override void Play_Internal(double duration)
        {
            int fadeInMilliseconds = duration > 0 ? (int) Math.Round(duration * 1000) : 0;
            if (!_samplePlayer.Play(_canLoop, fadeInMilliseconds))
            {
                return;
            }

            AudioHelpers.SfxSamples[(int) Sample].IsPlaying = true;
        }

        protected override int CreateStream_Internal() => _samplePlayer.CreateStream();

        protected override void Stop_Internal(double duration)
        {
            int fadeOutMilliseconds = duration > 0 ? (int) Math.Round(duration * 1000) : 0;
            _samplePlayer.Stop(fadeOutMilliseconds);
            AudioHelpers.SfxSamples[(int) Sample].IsPlaying = false;
        }

        protected override void Pause_Internal()
        {
            _samplePlayer.Pause();
        }

        protected override void Resume_Internal()
        {
            if (AudioHelpers.SfxSamples[(int) Sample].IsPlaying)
            {
                _samplePlayer.Resume();
            }
        }

        protected override void SetVolume_Internal(double volume)
        {
            _samplePlayer.SetVolume(volume * AudioHelpers.SfxSamples[(int) Sample].Volume);
        }

#nullable enable
        protected override void SetOutputChannel_Internal(OutputChannel? channel)
#nullable disable
        {
            _samplePlayer.SetOutputChannel(channel);
        }

        private void OnPlaybackEnded()
        {
            AudioHelpers.SfxSamples[(int) Sample].IsPlaying = false;
        }

        protected override void DisposeUnmanagedResources()
        {
            _samplePlayer.Dispose();
        }
    }
}