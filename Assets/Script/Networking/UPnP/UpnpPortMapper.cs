using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace YARG.Networking.UPnP
{
    /// <summary>
    /// Lightweight stub for UPnP/NAT-PMP port mapping. Provides graceful fallback when
    /// native support is unavailable on the current platform.
    /// </summary>
    public sealed class UpnpPortMapper
    {
        private static bool s_hasLoggedUnavailability;

        public UniTask<UpnpPortMappingHandle> TryAddMappingAsync(int port, string protocol, string description, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (!s_hasLoggedUnavailability)
            {
                Debug.Log("[UPnP] Port mapping helpers are not implemented on this platform. Continuing without automatic port mapping.");
                s_hasLoggedUnavailability = true;
            }

            return UniTask.FromResult<UpnpPortMappingHandle>(null);
        }

        public UniTask RemoveMappingAsync(UpnpPortMappingHandle handle, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return UniTask.CompletedTask;
        }
    }

    /// <summary>
    /// Represents an acquired port mapping. This is a placeholder to satisfy existing
    /// interfaces; future implementations can populate the metadata fields.
    /// </summary>
    public sealed class UpnpPortMappingHandle
    {
        public int ExternalPort { get; }
        public int InternalPort { get; }
        public string LocalAddress { get; }
        public string Protocol { get; }

        public UpnpPortMappingHandle(int externalPort, int internalPort, string localAddress, string protocol)
        {
            ExternalPort = externalPort;
            InternalPort = internalPort;
            LocalAddress = localAddress;
            Protocol = protocol;
        }
    }
}
