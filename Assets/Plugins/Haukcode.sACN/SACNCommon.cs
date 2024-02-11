using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;

namespace Haukcode.sACN
{
    public static class SACNCommon
    {
        // multicast address for sACN
        // 239.255.x.x where .x.x is the universe
        static byte MULTICAST_BYTE_1 = (byte)239;
        static byte MULTICAST_BYTE_2 = (byte)255;
        public static int SACN_PORT = 5568;

        public class Adapter
        {
            private readonly NetworkInterface networkInterface;

            public NetworkInterfaceType Type => networkInterface.NetworkInterfaceType;

            public string Id => networkInterface.Id;

            public string Name => networkInterface.Name;

            public string Description => networkInterface.Description;

            public byte[] PhysicalAddress { get; private set; }

            public string DisplayName
            {
                get
                {
                    if (Name == Description)
                    {
                        return Name;
                    }
                    else
                    {
                        return $"{Name} ({Description})";
                    }
                }
            }

            public bool IsHyperV => PhysicalAddress?.Length == 6 && PhysicalAddress[0] == 0x00 && PhysicalAddress[1] == 0x15 && PhysicalAddress[2] == 0x5D;

            public IList<(IPAddress IP, IPAddress NetMask)> AllIpv4Addresses => networkInterface.GetIPProperties().UnicastAddresses
                .Where(x => x.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(x => (x.Address, x.IPv4Mask))
                .ToList();

            public Adapter(NetworkInterface input)
            {
                networkInterface = input;

                PhysicalAddress = input.GetPhysicalAddress().GetAddressBytes();
            }
        }

        /// <summary>
        /// Get Multicast address from universe id
        /// </summary>
        /// <param name="universeId">Universe Id</param>
        /// <returns></returns>
        public static IPAddress GetMulticastAddress(ushort universeId)
        {
            byte highUniverseId = (byte)(universeId >> 8);
            byte lowUniverseId = (byte)(universeId & 0xFF);
            var multicastAddress = new IPAddress(new byte[] { MULTICAST_BYTE_1, MULTICAST_BYTE_2, highUniverseId, lowUniverseId });

            return multicastAddress;
        }

        public static IEnumerable<Adapter> GetAddressesFromInterfaceType(NetworkInterfaceType[] interfaceTypes, bool excludeHyperV = true)
        {
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.SupportsMulticast && interfaceTypes.Contains(adapter.NetworkInterfaceType) &&
                    (adapter.OperationalStatus == OperationalStatus.Up || adapter.OperationalStatus == OperationalStatus.Unknown))
                {
#if DEBUG
                    if (adapter.Name.Contains("Docker"))
                        // Skip Docker virtual adapters
                        continue;
#endif
                    var result = new Adapter(adapter);
                    if (excludeHyperV && result.IsHyperV)
                        continue;

                    // Only include adapters with IPv4 address(es)
                    if (!result.AllIpv4Addresses.Any())
                        continue;

                    yield return result;
                }
            }
        }

        /// <summary>
        /// Return list of ethernet and WiFi network adapters
        /// </summary>
        /// <returns>List of name and IPAddress</returns>
        public static IList<(string AdapterName, string Description, IPAddress IPAddress, IPAddress NetMask)> GetCommonInterfaces(bool excludeHyperV = true)
        {
            var list = new List<(string AdapterName, string Description, IPAddress IPAddress, IPAddress NetMask)>();

            foreach (var adapter in GetCommonAdapters(excludeHyperV))
            {
                list.AddRange(adapter.AllIpv4Addresses.Select(x => (adapter.Name, adapter.Description, x.IP, x.NetMask)));
            }

            return list;
        }

        /// <summary>
        /// Return list of ethernet and WiFi network adapters
        /// </summary>
        /// <returns>List of name and IPAddress</returns>
        public static IList<Adapter> GetCommonAdapters(bool excludeHyperV = true)
        {
            var adapters = GetAddressesFromInterfaceType(new NetworkInterfaceType[] { NetworkInterfaceType.Ethernet, NetworkInterfaceType.Wireless80211 }, excludeHyperV);

            return adapters.ToList();
        }

        /// <summary>
        /// Find first matching local IPAddress, first ethernet, then WiFi
        /// </summary>
        /// <returns>Local IPAddress, netmask, mac</returns>
        public static (IPAddress IPAddress, IPAddress NetMask, byte[] MacAddress) GetFirstBindAddress()
        {
            var adapters = GetCommonAdapters();

            // Try Ethernet first
            var firstEthernetAdapter = adapters.FirstOrDefault(x => x.Type == NetworkInterfaceType.Ethernet);
            if (firstEthernetAdapter != null)
            {
                var firstIpv4 = firstEthernetAdapter.AllIpv4Addresses.First();

                return (firstIpv4.IP, firstIpv4.NetMask, firstEthernetAdapter.PhysicalAddress);
            }

            // Then Wifi
            var firstWifiAdapter = adapters.FirstOrDefault(x => x.Type == NetworkInterfaceType.Wireless80211);
            if (firstWifiAdapter != null)
            {
                var firstIpv4 = firstWifiAdapter.AllIpv4Addresses.First();

                return (firstIpv4.IP, firstIpv4.NetMask, firstWifiAdapter.PhysicalAddress);
            }

            return (null, null, null);
        }
    }
}
