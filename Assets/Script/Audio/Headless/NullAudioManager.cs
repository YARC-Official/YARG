using System;
using System.Collections.Generic;
using YARG.Core.Audio;

namespace YARG.Audio.Headless
{
    internal class NullAudioManager : AudioManager
    {
        private static readonly string[] _emptyFormats = Array.Empty<string>();

        protected override ReadOnlySpan<string> SupportedFormats => _emptyFormats;

        protected override StemMixer? CreateMixer(string name, float speed, double volume, bool clampStemVolume)
        {
            return null;
        }

        protected override MicDevice? GetInputDevice(string name)
        {
            return null;
        }

        protected override List<(int id, string name)> GetAllInputDevices()
        {
            return new List<(int, string)>();
        }

        protected override MicDevice? CreateDevice(int deviceId, string name)
        {
            return null;
        }

        protected override void SetMasterVolume(double volume)
        {
        }

        protected override void ToggleBuffer_Internal(bool enable)
        {
        }

        protected override void SetBufferLength_Internal(int length)
        {
        }
    }
}
