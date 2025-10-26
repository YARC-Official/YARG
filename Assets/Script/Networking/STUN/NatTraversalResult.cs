using System;
using System.Net;

namespace YARG.Networking.STUN
{
    /// <summary>
    /// Represents the outcome of a STUN probe and follow-up NAT heuristics.
    /// </summary>
    [Serializable]
    public sealed class NatTraversalResult
    {
        public NetworkNatType NatType { get; set; } = NetworkNatType.Unknown;
        public IPEndPoint PublicEndPoint { get; set; }
            = new IPEndPoint(IPAddress.None, 0);
        public IPEndPoint LocalEndPoint { get; set; }
            = new IPEndPoint(IPAddress.None, 0);
        public string StunServer { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
            = DateTimeOffset.UtcNow;
        public bool IsTransportSocketResult { get; set; }
            = false;

        public bool HasPublicAddress =>
            PublicEndPoint != null && !Equals(PublicEndPoint.Address, IPAddress.None);

        public bool IsExpired(TimeSpan ttl)
        {
            if (ttl <= TimeSpan.Zero)
            {
                return false;
            }

            return DateTimeOffset.UtcNow - Timestamp >= ttl;
        }

        public override string ToString() =>
            $"NatTraversalResult(Type={NatType}, Public={PublicEndPoint}, Local={LocalEndPoint}, Server={StunServer}, TransportSocket={IsTransportSocketResult})";
    }
}
