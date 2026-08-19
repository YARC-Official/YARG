using ManagedBass;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    public sealed class BassDrumSampleChannel : DrumSampleChannel
    {
        private readonly BassOneShotSamplePlayer _samplePlayer;

#nullable enable
        private BassDrumSampleChannel(int handle, DrumSfxSample sample, string path, BassAudioRouter router,
            OutputChannel? outputChannel) : base(sample, path)
#nullable disable
        {
            _samplePlayer = new BassOneShotSamplePlayer(router, handle, sample.ToString());
            SetOutputChannel_Internal(outputChannel);
        }
#nullable enable
        internal static BassDrumSampleChannel? Create(DrumSfxSample sample, string path, BassAudioRouter router,
            OutputChannel? outputChannel)
#nullable disable
        {
            int handle = Bass.SampleLoad(path, 0, 0, 1, BassFlags.Default);
            if (handle == 0)
            {
                YargLogger.LogFormatError("Failed to load {0} {1}: {2}!", sample, path, Bass.LastError);
                return null;
            }

            return new BassDrumSampleChannel(handle, sample, path, router, outputChannel);
        }

        protected override void Play_Internal()
        {
            _samplePlayer.Play();
        }

        protected override void SetVolume_Internal(double volume)
        {
            _samplePlayer.Volume = volume;
        }

#nullable enable
        protected override void SetOutputChannel_Internal(OutputChannel? channel)
#nullable disable
        {
            _samplePlayer.OutputChannel = channel;
        }

        protected override void DisposeUnmanagedResources()
        {
            _samplePlayer.Dispose();
        }
    }
}