#nullable enable
using System;
using ManagedBass;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     Plays a single-voice sound effect sample directly through the audio router, restarting playback
    ///     from the beginning each time it is triggered (used for simple SFX and UI cues).
    /// </summary>
    internal sealed class BassOneShotSamplePlayer : IDisposable
    {
        private const    BassFlags SAMPLE_CHANNEL_STREAM = (BassFlags) 2;
        private readonly string    _name;

        private readonly BassAudioRouter _router;
        private readonly int             _sampleHandle;

        private bool _disposed;

        public BassOneShotSamplePlayer(BassAudioRouter router, int sampleHandle, string name)
        {
            _router = router;
            _sampleHandle = sampleHandle;
            _name = name;
        }

        public OutputChannel? OutputChannel { get; set; }
        public double         Volume        { get; set; } = 1;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (!Bass.SampleFree(_sampleHandle))
            {
                YargLogger.LogFormatError("Failed to free {0} sample: {1}!", _name, Bass.LastError);
            }
        }

        public bool Play()
        {
            int voice = CreateStream();
            if (voice == 0)
            {
                return false;
            }

            if (!Bass.ChannelSetAttribute(voice, ChannelAttribute.Volume, Volume))
            {
                YargLogger.LogFormatError("Failed to set {0} sample volume: {1}!", _name, Bass.LastError);
            }

            if (_router.PlaySample(voice, OutputChannel))
            {
                return true;
            }

            Bass.StreamFree(voice);
            return false;
        }

        public int CreateStream()
        {
            if (_disposed)
            {
                return 0;
            }

            int stream = Bass.SampleGetChannel(_sampleHandle, BassFlags.Decode | SAMPLE_CHANNEL_STREAM);
            if (stream == 0)
            {
                YargLogger.LogFormatError("Failed to create {0} sample voice: {1}!", _name, Bass.LastError);
            }

            return stream;
        }
    }
}