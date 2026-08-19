#nullable enable
using ManagedBass;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    public sealed class BassVenueSampleChannel : VenueSampleChannel
    {
        private readonly BassSamplePlayer _samplePlayer;

        internal byte[] SampleData { get; }
        internal OutputChannel? OutputChannel { get; private set; }

        internal static BassVenueSampleChannel? Create(string name, byte[] sampleData, BassAudioRouter router,
            OutputChannel? outputChannel)
        {
            int handle = Bass.SampleLoad(sampleData, 0, sampleData.Length, 1, BassFlags.Default);
            if (handle == 0)
            {
                YargLogger.LogFormatError("Failed to load venue sample {0}: {1}", name, Bass.LastError);
                return null;
            }

            return new BassVenueSampleChannel(handle, name, sampleData, router, outputChannel);
        }

        private BassVenueSampleChannel(int handle, string name, byte[] sampleData, BassAudioRouter router,
            OutputChannel? outputChannel)
            : base(name, sampleData)
        {
            SampleData = sampleData;
            _samplePlayer = new BassSamplePlayer(router, handle, name);
            SetOutputChannel_Internal(outputChannel);
            SetVolume_Internal(GlobalAudioHandler.GetTrueVolume(SongStem.VenueSample));
        }

        protected override void Play_Internal()
        {
            _samplePlayer.Stop();
            _samplePlayer.Play();
        }

        protected override void Pause_Internal()
        {
            _samplePlayer.Pause();
        }

        protected override void Resume_Internal()
        {
            _samplePlayer.Resume();
        }

        protected override void Stop_Internal()
        {
            _samplePlayer.Stop();
        }

        protected override void SetVolume_Internal(double volume)
        {
            _samplePlayer.SetVolume(volume);
        }

        protected override void SetOutputChannel_Internal(OutputChannel? channel)
        {
            OutputChannel = channel;
            _samplePlayer.SetOutputChannel(channel);
        }

        protected override bool IsPlaying_Internal()
        {
            return _samplePlayer.IsPlaying;
        }

        protected override bool IsPaused_Internal()
        {
            return _samplePlayer.IsPaused;
        }

        protected override void DisposeUnmanagedResources()
        {
            _samplePlayer.Dispose();
        }
    }
}
