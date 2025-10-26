using System.Collections.Generic;

namespace YARG.Networking
{
    /// <summary>
    /// Default transport configuration values shared across the networking layer.
    /// </summary>
    public static class NetworkTransportDefaults
    {
        public const ushort DefaultTcpPort = 22023;
        public const ushort DefaultUdpPort = 32023;
        public const int DefaultStunCacheTtlSeconds = 120;
        public const int DefaultPunchRetryWindowSeconds = 15;

        public static readonly IReadOnlyList<string> DefaultStunServers = new[]
        {
            "stun.l.google.com:19302",
            "stun.cloudflare.com:3478"
        };
    }
}
