using System;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace YARG.Networking
{
    internal static class StunUtility
    {
        private const ushort BindingRequestType = 0x0001;
        private const ushort BindingSuccessResponseType = 0x0101;
        private const uint MagicCookie = 0x2112A442;
        private const ushort MappedAddressType = 0x0001;
        private const ushort XorMappedAddressType = 0x0020;
        private const int ResolveTimeoutMs = 3000;

        private static readonly (string Host, int Port)[] StunServers =
        {
            ("stun.l.google.com", 19302),
            ("stun1.l.google.com", 19302),
            ("stun2.l.google.com", 19302),
            ("stun3.l.google.com", 19302),
            ("stun4.l.google.com", 19302)
        };

        public static async UniTask<string> TryResolvePublicAddressAsync(CancellationToken token)
        {
            foreach (var (host, port) in StunServers)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    string address = await QueryServerAsync(host, port, token);
                    if (!string.IsNullOrEmpty(address))
                    {
                        return address;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[StunUtility] STUN query to {host}:{port} failed: {ex.Message}");
                }
            }

            return string.Empty;
        }

        private static async UniTask<string> QueryServerAsync(string host, int port, CancellationToken token)
        {
            using var udpClient = new UdpClient();
            udpClient.Client.SendTimeout = ResolveTimeoutMs;
            udpClient.Client.ReceiveTimeout = ResolveTimeoutMs;

            byte[] request = BuildBindingRequest();

            token.ThrowIfCancellationRequested();
            Task<int> sendTask = udpClient.SendAsync(request, request.Length, host, port);
            Task completedSend = await Task.WhenAny(sendTask, Task.Delay(ResolveTimeoutMs));
            if (completedSend != sendTask)
            {
                return string.Empty;
            }

            await sendTask; // propagate exceptions

            token.ThrowIfCancellationRequested();
            Task<UdpReceiveResult> receiveTask = udpClient.ReceiveAsync();
            Task completedReceive = await Task.WhenAny(receiveTask, Task.Delay(ResolveTimeoutMs));
            if (completedReceive != receiveTask)
            {
                return string.Empty;
            }

            UdpReceiveResult response = await receiveTask;
            if (TryParsePublicAddress(response.Buffer, out var address))
            {
                return address.ToString();
            }

            return string.Empty;
        }

        private static byte[] BuildBindingRequest()
        {
            byte[] buffer = new byte[20];
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(0, 2), BindingRequestType);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(2, 2), 0);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(4, 4), MagicCookie);
            RandomNumberGenerator.Fill(buffer.AsSpan(8, 12));
            return buffer;
        }

        private static bool TryParsePublicAddress(ReadOnlySpan<byte> data, out IPAddress address)
        {
            address = null;

            if (data.Length < 20)
            {
                return false;
            }

            ushort messageType = BinaryPrimitives.ReadUInt16BigEndian(data[..2]);
            if (messageType != BindingSuccessResponseType)
            {
                return false;
            }

            uint magicCookie = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4, 4));
            int offset = 20;

            while (offset + 4 <= data.Length)
            {
                ushort attributeType = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2));
                ushort attributeLength = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset + 2, 2));
                offset += 4;

                if (offset + attributeLength > data.Length)
                {
                    break;
                }

                if ((attributeType == XorMappedAddressType || attributeType == MappedAddressType) && attributeLength >= 8)
                {
                    byte family = data[offset + 1];
                    if (family == 0x01)
                    {
                        if (attributeType == XorMappedAddressType)
                        {
                            uint xorAddress = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset + 4, 4));
                            uint addressValue = xorAddress ^ magicCookie;
                            Span<byte> bytes = stackalloc byte[4];
                            BinaryPrimitives.WriteUInt32BigEndian(bytes, addressValue);
                            address = new IPAddress(bytes);
                            return true;
                        }
                        else
                        {
                            Span<byte> bytes = stackalloc byte[4];
                            data.Slice(offset + 4, 4).CopyTo(bytes);
                            address = new IPAddress(bytes);
                            return true;
                        }
                    }
                }

                offset += AlignToWord(attributeLength);
            }

            return false;
        }

        private static int AlignToWord(int length)
        {
            return (length + 3) & ~3;
        }
    }
}
