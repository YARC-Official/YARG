using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace YARG.Networking.STUN
{
    /// <summary>
    /// Minimal STUN RFC 5389 binding probe implementation.
    /// Only extracts the XOR-MAPPED-ADDRESS / MAPPED-ADDRESS attribute.
    /// </summary>
    public static class StunClient
    {
        internal const ushort BindingRequestType = 0x0001;
        internal const ushort BindingSuccessResponseType = 0x0101;
        private const int HeaderLength = 20;
        private const uint MagicCookie = 0x2112A442;

        /// <summary>
        /// Performs a binding request against the supplied STUN server and returns the mapped endpoint if available.
        /// </summary>
        public static async Task<NatTraversalResult> QueryAsync(string stunServer, int timeoutMilliseconds, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(stunServer))
            {
                throw new ArgumentException("STUN server host required", nameof(stunServer));
            }

            var (host, port) = ParseEndpoint(stunServer);
            var result = new NatTraversalResult { StunServer = stunServer };

            using var udpClient = CreateUdpClient(timeoutMilliseconds);

            var request = BuildBindingRequest(out var transactionId);

            try
            {
                await udpClient.SendAsync(request, request.Length, host, port).ConfigureAwait(false);
                result.LocalEndPoint = (IPEndPoint)udpClient.Client.LocalEndPoint;
            }
            catch (Exception ex) when (ex is SocketException || ex is ObjectDisposedException)
            {
                throw new StunException("Failed to dispatch STUN request", ex);
            }

            using var timeoutCts = new CancellationTokenSource(timeoutMilliseconds);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            using var registration = linkedCts.Token.Register(() => udpClient.Close());

            try
            {
                var response = await udpClient.ReceiveAsync().ConfigureAwait(false);
                if (response.Buffer.Length < HeaderLength)
                {
                    throw new StunException("STUN response truncated");
                }

                ParseResponse(response.Buffer, transactionId, out var mapped, out var natType);
                result.PublicEndPoint = mapped ?? new IPEndPoint(IPAddress.None, 0);
                result.NatType = natType;
                result.Timestamp = DateTimeOffset.UtcNow;
            }
            catch (ObjectDisposedException) when (linkedCts.IsCancellationRequested)
            {
                if (timeoutCts.IsCancellationRequested)
                {
                    throw new StunException("STUN response timed out");
                }

                throw new StunException("STUN probe cancelled");
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
            {
                throw new StunException("STUN response timed out", ex);
            }
            catch (SocketException ex)
            {
                throw new StunException("Socket failure while awaiting STUN response", ex);
            }

            return result;
        }

        private static UdpClient CreateUdpClient(int timeoutMilliseconds)
        {
            SocketException lastError = null;

            foreach (var family in new[] { AddressFamily.InterNetwork, AddressFamily.InterNetworkV6 })
            {
                try
                {
                    var client = new UdpClient(family);

                    if (family == AddressFamily.InterNetworkV6)
                    {
                        try
                        {
                            client.Client.DualMode = true;
                        }
                        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationNotSupported)
                        {
                            // Some platforms throw when DualMode is not available; fall back to IPv4 below.
                            client.Dispose();
                            lastError = ex;
                            continue;
                        }
                    }

                    client.Client.ReceiveTimeout = timeoutMilliseconds;
                    client.Client.SendTimeout = timeoutMilliseconds;
                    return client;
                }
                catch (SocketException ex)
                {
                    lastError = ex;
                }
            }

            throw new StunException("Failed to create UDP client for STUN probing", lastError);
        }

        internal static byte[] BuildBindingRequest(out byte[] transactionId)
        {
            var buffer = new byte[HeaderLength];
            buffer[0] = (byte)((BindingRequestType >> 8) & 0xFF);
            buffer[1] = (byte)(BindingRequestType & 0xFF);
            buffer[2] = 0;
            buffer[3] = 0; // no attributes currently being sent

            buffer[4] = (byte)((MagicCookie >> 24) & 0xFF);
            buffer[5] = (byte)((MagicCookie >> 16) & 0xFF);
            buffer[6] = (byte)((MagicCookie >> 8) & 0xFF);
            buffer[7] = (byte)(MagicCookie & 0xFF);

            transactionId = new byte[12];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(transactionId);
            }
            Buffer.BlockCopy(transactionId, 0, buffer, 8, transactionId.Length);

            return buffer;
        }

        private static void ParseResponse(byte[] buffer, byte[] transactionId, out IPEndPoint mappedEndPoint, out NetworkNatType natType)
        {
            if (!TryParseBindingResponse(new ReadOnlySpan<byte>(buffer, 0, buffer.Length), transactionId, out mappedEndPoint, out natType))
            {
                throw new StunException("STUN response could not be parsed or did not match the pending transaction.");
            }
        }

        internal static bool TryParseBindingResponse(ReadOnlySpan<byte> buffer, byte[] transactionId, out IPEndPoint mappedEndPoint, out NetworkNatType natType)
        {
            mappedEndPoint = null;
            natType = NetworkNatType.Unknown;

            if (buffer.Length < HeaderLength)
            {
                return false;
            }

            ushort messageType = (ushort)((buffer[0] << 8) | buffer[1]);
            if (messageType != BindingSuccessResponseType)
            {
                return false;
            }

            int index = HeaderLength;
            while (index + 4 <= buffer.Length)
            {
                ushort attributeType = (ushort)((buffer[index] << 8) | buffer[index + 1]);
                ushort attributeLength = (ushort)((buffer[index + 2] << 8) | buffer[index + 3]);
                index += 4;

                if (index + attributeLength > buffer.Length)
                {
                    break;
                }

                switch (attributeType)
                {
                    case 0x0001: // MAPPED-ADDRESS
                        mappedEndPoint ??= ParseMappedAddress(buffer, index, useXor: false, transactionId);
                        break;
                    case 0x0020: // XOR-MAPPED-ADDRESS
                        mappedEndPoint = ParseMappedAddress(buffer, index, useXor: true, transactionId);
                        break;
                    case 0x8032:
                        natType = NetworkNatType.FullCone;
                        break;
                }

                index += attributeLength;
                int padding = attributeLength % 4;
                if (padding != 0)
                {
                    index += 4 - padding;
                }
            }

            if (mappedEndPoint != null && mappedEndPoint.Port == 0)
            {
                natType = NetworkNatType.Blocked;
            }

            return mappedEndPoint != null;
        }

        private static IPEndPoint ParseMappedAddress(ReadOnlySpan<byte> buffer, int offset, bool useXor, byte[] transactionId)
        {
            if (offset + 4 > buffer.Length)
            {
                return null;
            }

            byte family = buffer[offset + 1];
            ushort port = (ushort)((buffer[offset + 2] << 8) | buffer[offset + 3]);

            if (useXor)
            {
                port ^= (ushort)((MagicCookie >> 16) ^ (MagicCookie & 0xFFFF));
            }

            offset += 4;

            switch (family)
            {
                case 0x01: // IPv4
                {
                    if (offset + 4 > buffer.Length)
                    {
                        return null;
                    }

                    Span<byte> addressBytes = stackalloc byte[4];
                    buffer.Slice(offset, 4).CopyTo(addressBytes);
                    if (useXor)
                    {
                        var cookieBytes = BitConverter.GetBytes(MagicCookie);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(cookieBytes);
                        }

                        for (int i = 0; i < addressBytes.Length; i++)
                        {
                            addressBytes[i] ^= cookieBytes[i];
                        }
                    }

                    var address = new IPAddress(addressBytes.ToArray());
                    return new IPEndPoint(address, port);
                }
                case 0x02: // IPv6
                {
                    if (offset + 16 > buffer.Length)
                    {
                        return null;
                    }

                    Span<byte> addressBytes = stackalloc byte[16];
                    buffer.Slice(offset, 16).CopyTo(addressBytes);
                    if (useXor)
                    {
                        var xorSource = new byte[16];
                        var magicBytes = BitConverter.GetBytes(MagicCookie);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(magicBytes);
                        }

                        Array.Copy(magicBytes, 0, xorSource, 0, 4);
                        Array.Copy(transactionId, 0, xorSource, 4, 12);

                        for (int i = 0; i < addressBytes.Length; i++)
                        {
                            addressBytes[i] ^= xorSource[i];
                        }
                    }

                    var address = new IPAddress(addressBytes.ToArray());
                    return new IPEndPoint(address, port);
                }
                default:
                    return null;
            }
        }

        private static (string host, int port) ParseEndpoint(string endpoint)
        {
            var segments = endpoint.Split(':');
            if (segments.Length == 1)
            {
                return (segments[0], 3478);
            }

            if (segments.Length == 2 && int.TryParse(segments[1], out var parsed))
            {
                return (segments[0], parsed);
            }

            // IPv6 literal with port [addr]:port
            if (endpoint.StartsWith("[", StringComparison.Ordinal))
            {
                var closing = endpoint.IndexOf(']');
                if (closing > 0)
                {
                    var host = endpoint.Substring(1, closing - 1);
                    var remaining = endpoint.Substring(closing + 1);
                    if (remaining.StartsWith(":", StringComparison.Ordinal))
                    {
                        if (int.TryParse(remaining.Substring(1), out var port))
                        {
                            return (host, port);
                        }
                    }

                    return (host, 3478);
                }
            }

            throw new ArgumentException($"Unable to parse STUN endpoint '{endpoint}'", nameof(endpoint));
        }
    }
}
