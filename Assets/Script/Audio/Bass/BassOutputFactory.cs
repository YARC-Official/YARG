#nullable enable
using System;
using System.Collections.Generic;
using YARG.Audio.BASS.Asio;
using YARG.Core.Audio;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     Creates shared or ASIO outputs from the device name selected in settings.
    /// </summary>
    internal sealed class BassOutputFactory
    {
        private readonly BassAsioMics _asioMics = new();

        private readonly BassAudioRouter _router;

        public BassOutputFactory(BassAudioRouter router)
        {
            _router = router;
        }

        private static bool IsAsioSupported
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
            if (IsAsioSupported && name.StartsWith(BassAsioOutput.DEVICE_PREFIX, StringComparison.Ordinal))
            {
                return BassAsioOutput.Find(name, _router, _asioMics);
            }

            return BassSharedOutput.Find(name, _router);
        }

        public List<(int id, string name)> GetAllDevices()
        {
            var devices = BassSharedOutput.GetDevices();
            if (IsAsioSupported)
            {
                devices.AddRange(BassAsioOutput.GetDevices());
            }

            return devices;
        }

        public AudioOutputMode ModeFor(string name)
        {
            if (name.StartsWith(BassAsioOutput.DEVICE_PREFIX, StringComparison.Ordinal))
            {
                return AudioOutputMode.Asio;
            }

            return AudioOutputMode.Shared;
        }
    }
}