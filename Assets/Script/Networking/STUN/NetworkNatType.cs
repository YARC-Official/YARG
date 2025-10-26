using System;

namespace YARG.Networking.STUN
{
    /// <summary>
    /// Enumerates the NAT styles we care about for connection diagnostics.
    /// </summary>
    [Serializable]
    public enum NetworkNatType : byte
    {
        Unknown = 0,
        OpenInternet = 1,
        FullCone = 2,
        RestrictedCone = 3,
        PortRestrictedCone = 4,
        Symmetric = 5,
        Blocked = 6
    }
}
