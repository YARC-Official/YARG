using ManagedBass;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    public sealed class BassMetronomeSampleChannel : MetronomeSampleChannel
    {
        private readonly BassOneShotSamplePlayer _hiPlayer;
        private readonly BassOneShotSamplePlayer _loPlayer;

#nullable enable
        private BassMetronomeSampleChannel(MetronomeSample sample, int hiHandle, string hiPath, int loHandle,
            string loPath, BassAudioRouter router, OutputChannel? outputChannel) : base(sample, hiPath, loPath)
#nullable disable
        {
            _hiPlayer = new BassOneShotSamplePlayer(router, hiHandle, $"{sample} hi");
            _loPlayer = new BassOneShotSamplePlayer(router, loHandle, $"{sample} lo");
            SetOutputChannel_Internal(outputChannel);
            SetVolume_Internal(GlobalAudioHandler.GetTrueVolume(SongStem.Metronome));
        }
#nullable enable
        internal static BassMetronomeSampleChannel? Create(MetronomeSample sample, string hiPath, string loPath,
            BassAudioRouter router, OutputChannel? outputChannel)
#nullable disable
        {
            int hiHandle = Bass.SampleLoad(hiPath, 0, 0, 1, BassFlags.Decode);
            if (hiHandle == 0)
            {
                YargLogger.LogFormatError("Failed to load {0} hi {1}: {2}!", sample, hiPath, Bass.LastError);
                return null;
            }

            int loHandle = Bass.SampleLoad(loPath, 0, 0, 1, BassFlags.Decode);
            if (loHandle == 0)
            {
                Bass.SampleFree(hiHandle);
                YargLogger.LogFormatError("Failed to load {0} lo {1}: {2}!", sample, loPath, Bass.LastError);
                return null;
            }

            return new BassMetronomeSampleChannel(sample, hiHandle, hiPath, loHandle, loPath, router, outputChannel);
        }

        protected override void PlayHi_Internal()
        {
            if (!_hiPlayer.Play())
            {
                YargLogger.LogFormatError("Failed to play {0} hi channel: {1}!", Sample, Bass.LastError);
            }
        }

        protected override void PlayLo_Internal()
        {
            if (!_loPlayer.Play())
            {
                YargLogger.LogFormatError("Failed to play {0} lo channel: {1}!", Sample, Bass.LastError);
            }
        }

        protected override int CreateStream_Internal(MetronomePitch pitch) =>
            pitch == MetronomePitch.Hi ? _hiPlayer.CreateStream() : _loPlayer.CreateStream();

        protected override void SetVolume_Internal(double volume)
        {
            volume *= AudioHelpers.MetronomeSamples[(int) Sample].Volume;

            _hiPlayer.Volume = volume;
            _loPlayer.Volume = volume;
        }

#nullable enable
        protected override void SetOutputChannel_Internal(OutputChannel? channel)
#nullable disable
        {
            _hiPlayer.OutputChannel = channel;
            _loPlayer.OutputChannel = channel;
        }

        protected override void DisposeUnmanagedResources()
        {
            _hiPlayer.Dispose();
            _loPlayer.Dispose();
        }
    }
}