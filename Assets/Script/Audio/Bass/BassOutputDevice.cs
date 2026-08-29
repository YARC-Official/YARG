#nullable enable
using System;
using System.Collections.Generic;
using ManagedBass;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    public sealed class BassOutputDevice : OutputDevice
    {
        private const           int                  DEFAULT_SAMPLE_RATE = 44_100;
        private const           int                  NO_SOUND_DEVICE     = 0;
        private static readonly object               _lock               = new();
        private static readonly Dictionary<int, int> _users              = new();

        private BassOutputDevice(int deviceId, string name) : base(name)
        {
            DeviceId = deviceId;
            Use();
        }

        public int DeviceId { get; }

        internal static BassOutputDevice? Create(int deviceId, string name)
        {
            lock (_lock)
            {
                return TryCreate(deviceId, name, DeviceInitFlags.Default | DeviceInitFlags.Latency);
            }
        }

        internal static BassOutputDevice? CreateAsio(string name)
        {
            lock (_lock)
            {
                return TryCreate(NO_SOUND_DEVICE, name, DeviceInitFlags.Default);
            }
        }

        internal static BassOutputDevice? CreateWasapi(string name)
        {
            lock (_lock)
            {
                return TryCreate(NO_SOUND_DEVICE, name, DeviceInitFlags.Default);
            }
        }

        public void Use()
        {
            Bass.CurrentDevice = DeviceId;
        }

        protected override void DisposeUnmanagedResources()
        {
            lock (_lock)
            {
                if (!_users.TryGetValue(DeviceId, out int users))
                {
                    return;
                }

                if (users > 1)
                {
                    _users[DeviceId] = users - 1;
                    return;
                }

                _users.Remove(DeviceId);
                Use();
                Bass.Free();
            }
        }

        private static BassOutputDevice? TryCreate(int requestedDevice, string name, DeviceInitFlags flags)
        {
            try
            {
                if (!TryInitializeBass(requestedDevice, name, flags))
                {
                    return null;
                }

                int deviceId = requestedDevice == NO_SOUND_DEVICE ? NO_SOUND_DEVICE : Bass.CurrentDevice;
                if (_users.TryGetValue(deviceId, out int users))
                {
                    _users[deviceId] = users + 1;
                }
                else
                {
                    _users.Add(deviceId, 1);
                }

                return new BassOutputDevice(deviceId, name);
            }
            catch (BassException exception)
            {
                YargLogger.LogException(exception);
                return null;
            }
        }

        private static bool TryInitializeBass(int deviceId, string name, DeviceInitFlags flags)
        {
            if (deviceId != NO_SOUND_DEVICE)
            {
                int devPeriod = Math.Max(1, Bass.GetConfig(Configuration.DevicePeriod));
                if (Bass.DeviceBufferLength <= 0)
                {
                    Bass.DeviceBufferLength = 2 * devPeriod;
                }
            }

            if (Bass.Init(deviceId, DEFAULT_SAMPLE_RATE, flags, IntPtr.Zero))
            {
                return true;
            }

            if (Bass.LastError == Errors.Already)
            {
                return true;
            }

            YargLogger.LogFormatError("Failed to initialize BASS device '{0}': {1}!", name, Bass.LastError);
            return false;
        }

        public static void ResetForEditor()
        {
            lock (_lock)
            {
                _users.Clear();
            }
        }
    }
}