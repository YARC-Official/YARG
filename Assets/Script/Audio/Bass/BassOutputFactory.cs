#nullable enable
using System;
using System.Collections.Generic;
using YARG.Audio.BASS.Asio;
using YARG.Audio.BASS.Wasapi;
using YARG.Core.Audio;

namespace YARG.Audio.BASS
{
    internal sealed class BassOutputFactory
    {
        private readonly BassAsioMics _asioMics = new();

        private readonly BassAudioRouter _router;

        public BassOutputFactory(BassAudioRouter router)
        {
            _router = router;
        }

        private static bool IsWindows
        {
            get
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                return true;
#else
                return false;
#endif
            }
        }

        public BassOutput? Create(string name)
        {
            if (IsWindows)
            {
                if (name.StartsWith(BassWasapiOutput.DEVICE_PREFIX, StringComparison.Ordinal))
                {
                    return BassWasapiOutput.Find(name, _router);
                }

                if (name.StartsWith(BassAsioOutput.DEVICE_PREFIX, StringComparison.Ordinal))
                {
                    return BassAsioOutput.Find(name, _router, _asioMics);
                }
            }

            return BassSharedOutput.Find(name, _router);
        }

        public List<(int id, string name)> GetAllDevices()
        {
            var devices = BassSharedOutput.GetDevices();
            if (IsWindows)
            {
                devices.AddRange(BassAsioOutput.GetDevices());
                devices.AddRange(BassWasapiOutput.GetDevices());
            }

            return devices;
        }

        public AudioOutputMode ModeFor(string name)
        {
            if (name.StartsWith(BassWasapiOutput.DEVICE_PREFIX, StringComparison.Ordinal))
            {
                return AudioOutputMode.WasapiExclusive;
            }

            if (name.StartsWith(BassAsioOutput.DEVICE_PREFIX, StringComparison.Ordinal))
            {
                return AudioOutputMode.Asio;
            }

            return AudioOutputMode.Shared;
        }
    }
}