using YARG.Settings;

namespace YARG.Networking
{
    /// <summary>
    /// Default transport configuration values shared across the networking layer.
    /// </summary>
    public static class NetworkTransportDefaults
    {
        public const ushort DefaultTcpPort = 22023;

        private const ushort FALLBACK_UDP_PORT = 7777;

        public static ushort DefaultUdpPort
        {
            get
            {
                var settingsContainer = SettingsManager.Settings;

                if (settingsContainer?.NetworkPort is null)
                {
                    return FALLBACK_UDP_PORT;
                }

                return (ushort) settingsContainer.NetworkPort.Value;
            }
        }
    }
}
