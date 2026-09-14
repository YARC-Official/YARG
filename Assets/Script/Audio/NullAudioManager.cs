using System;
using System.Collections.Generic;
using YARG.Core.Audio;

namespace YARG.Audio
{
    // Silent fallback for platforms where the BASS natives are unavailable
    // (currently iOS, until BASS iOS binaries are integrated). Keeps the game
    // bootable with audio features disabled.
    public class NullAudioManager : AudioManager
    {
        private static readonly string[] FORMATS =
        {
            ".ogg",
            ".mogg",
            ".wav",
            ".mp3",
            ".aiff",
            ".opus",
        };

        public NullAudioManager()
        {
            MinimumBufferLength = 0;
            MaximumBufferLength = 5000;
        }

        protected override ReadOnlySpan<string> SupportedFormats => FORMATS;

        protected override StemMixer? CreateMixer(string name, float speed, double volume, bool clampStemVolume,
            bool normalize) => null;

        protected override List<InputDeviceInfo> GetAllInputDevices() => new();

        protected override MicDevice? CreateInputDevice(InputDeviceInfo device) => null;

        protected override OutputChannel? CreateOutputChannel(int channelId) => null;

        protected override List<(int id, string name)> GetAllOutputDevices() => new();

        protected override int GetOutputChannelCount() => 0;

        protected override void SetMasterVolume(double volume) { }

        public override void LoadVenueSample(string name, byte[] sampleData, OutputChannel? outputChannel = null) { }

        public override void ClearVenueSamples() { }

        protected override void PlayMetronomeSoundEffectToChannel(MetronomeSample sample, MetronomePitch pitch,
            int channelId) { }

        protected override bool SetOutputDevice(string name) => false;

        protected override void SetBufferLength_Internal(int length) { }
    }
}
