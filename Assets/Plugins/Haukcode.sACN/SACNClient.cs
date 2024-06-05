using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Haukcode.sACN.Model;
using UniRx;

namespace Haukcode.sACN
{
    public class SACNClient : IDisposable
    {
        public class SendSocketData
        {
            public Socket Socket;

            public IPEndPoint Destination;

            public Memory<byte> SendBufferMem;

        }

        public class SendData
        {
            public ushort UniverseId;

            public IPEndPoint Destination;

            public IMemoryOwner<byte> Data;

            public int DataLength;

            public Stopwatch Enqueued;

            public double AgeMS => Enqueued.Elapsed.TotalMilliseconds;

            public SendData()
            {
                Enqueued = Stopwatch.StartNew();
            }
        }
        private const int ReceiveBufferSize = 20480;
        private const int SendBufferSize = 1024;
        private static readonly IPEndPoint _blankEndpoint = new(IPAddress.Any, 0);

        private readonly Socket listenSocket;
        private readonly ISubject<Exception> errorSubject;
        private readonly Dictionary<ushort, byte> sequenceIds = new();
        private readonly Dictionary<ushort, byte> sequenceIdsSync = new();
        private readonly object lockObject = new();
        private readonly HashSet<ushort> dmxUniverses = new();
        private readonly Memory<byte> receiveBufferMem;
        private readonly Stopwatch clock = new();
        private readonly Task receiveTask;
        private readonly Task sendTask;
        private readonly CancellationTokenSource shutdownCTS = new();
        private readonly Dictionary<IPAddress, IPEndPoint> endPointCache = new();
        private readonly ConcurrentDictionary<ushort, SendSocketData> universeSockets = new();
        private readonly IPEndPoint localEndPoint;
        private readonly BlockingCollection<SendData> sendQueue = new();
        private readonly MemoryPool<byte> memoryPool = MemoryPool<byte>.Shared;
        private int droppedPackets;
        private int slowSends;
        private readonly HashSet<(IPAddress Destination, ushort UniverseId)> usedDestinations = new();

        public SACNClient(Guid senderId, string senderName, IPAddress localAddress, int port = 5568)
        {
            if (senderId == Guid.Empty)
            {
                throw new ArgumentException("Invalid sender Id", nameof(senderId));
            }

            SenderId = senderId;

            SenderName = senderName;

            if (port <= 0)
            {
                throw new ArgumentException("Invalid port", nameof(port));
            }

            Port = port;
            localEndPoint = new IPEndPoint(localAddress, port);
            var receiveBuffer = new byte[ReceiveBufferSize];
            receiveBufferMem = receiveBuffer.AsMemory();
            errorSubject = new Subject<Exception>();
            listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            // Set the SIO_UDP_CONNRESET ioctl to true for this UDP socket. If this UDP socket
            //    ever sends a UDP packet to a remote destination that exists but there is
            //    no socket to receive the packet, an ICMP port unreachable message is returned
            //    to the sender. By default, when this is received the next operation on the
            //    UDP socket that send the packet will receive a SocketException. The native
            //    (Winsock) error that is received is WSAECONNRESET (10054). Since we don't want
            //    to wrap each UDP socket operation in a try/except, we'll disable this error
            //    for the socket with this ioctl call.
            try
            {
                uint IOC_IN = 0x80000000;
                uint IOC_VENDOR = 0x18000000;
                uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;

                byte[] optionInValue = { Convert.ToByte(false) };
                byte[] optionOutValue = new byte[4];
                listenSocket.IOControl((int)SIO_UDP_CONNRESET, optionInValue, optionOutValue);
            }
            catch
            {
                Debug.WriteLine("Unable to set SIO_UDP_CONNRESET, maybe not supported.");
            }

            listenSocket.ExclusiveAddressUse = false;
            listenSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            // Only join local LAN group
            listenSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);

            sendTask = Task.Run(Sender);
        }

        public bool IsOperational => !this.shutdownCTS.IsCancellationRequested;

        private void ConfigureSendSocket(Socket socket)
        {
            socket.SendBufferSize = 1400;

            // Set the SIO_UDP_CONNRESET ioctl to true for this UDP socket. If this UDP socket
            //    ever sends a UDP packet to a remote destination that exists but there is
            //    no socket to receive the packet, an ICMP port unreachable message is returned
            //    to the sender. By default, when this is received the next operation on the
            //    UDP socket that send the packet will receive a SocketException. The native
            //    (Winsock) error that is received is WSAECONNRESET (10054). Since we don't want
            //    to wrap each UDP socket operation in a try/except, we'll disable this error
            //    for the socket with this ioctl call.
            try
            {
                uint IOC_IN = 0x80000000;
                uint IOC_VENDOR = 0x18000000;
                uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;

                byte[] optionInValue = { Convert.ToByte(false) };
                byte[] optionOutValue = new byte[4];
                socket.IOControl((int)SIO_UDP_CONNRESET, optionInValue, optionOutValue);
            }
            catch
            {
                Debug.WriteLine("Unable to set SIO_UDP_CONNRESET, maybe not supported.");
            }

            socket.ExclusiveAddressUse = false;
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            socket.Bind(this.localEndPoint);

            // Multicast socket settings
            socket.DontFragment = true;
            socket.MulticastLoopback = false;

            // Only join local LAN group
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
        }

        public int Port { get; }

        public Guid SenderId { get; }

        public string SenderName { get; }

        public UniRx.IObservable<Exception> OnError => this.errorSubject.AsObservable();

        public SendStatistics SendStatistics
        {
            get
            {
                var sendStatistics = new SendStatistics
                {
                    DroppedPackets = this.droppedPackets,
                    QueueLength = this.sendQueue.Count,
                    SlowSends = this.slowSends,
                    DestinationCount = this.usedDestinations.Count
                };

                // Reset
                this.droppedPackets = 0;
                this.slowSends = 0;
                this.usedDestinations.Clear();

                return sendStatistics;
            }
        }

        /// <summary>
        /// Gets a list of dmx universes this socket has joined to
        /// </summary>
        public IReadOnlyCollection<ushort> DMXUniverses => dmxUniverses.ToList();
        private async Task Sender()
        {
            while (!shutdownCTS.IsCancellationRequested)
            {
                var sendData = sendQueue.Take(shutdownCTS.Token);

                try
                {
                    if (sendData.AgeMS > 100)
                    {
                        // Old, discard
                        droppedPackets++;
                        //Console.WriteLine($"Age {sendData.Enqueued.Elapsed.TotalMilliseconds:N2}   queue length = {this.sendQueue.Count}   Dropped = {this.droppedPackets}");
                        continue;
                    }

                    var socketData = GetSendSocket(sendData.UniverseId);

                    var watch = Stopwatch.StartNew();
                    byte[] dataBytes = sendData.Data.Memory[..sendData.DataLength].ToArray();
                    await socketData.Socket.SendToAsync(dataBytes, SocketFlags.None, sendData.Destination ?? socketData.Destination);
                    watch.Stop();

                    if (watch.ElapsedMilliseconds > 20)
                    {
                        slowSends++;
                    }
                }
                catch (Exception ex)
                {
                    if (ex is OperationCanceledException)
                        continue;

                    errorSubject.OnNext(ex);

                    if (ex is System.Net.Sockets.SocketException)
                    {
                        // Network unreachable
                        shutdownCTS.Cancel();
                        break;
                    }
                }
                finally
                {
                    // Return to pool
                    sendData.Data.Dispose();
                }
            }
        }

        private SendSocketData GetSendSocket(ushort universeId)
        {
            if (!universeSockets.TryGetValue(universeId, out var socketData))
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                ConfigureSendSocket(socket);

                var sendBuffer = new byte[SendBufferSize];

                socketData = new SendSocketData
                {
                    Socket = socket,
                    Destination = new IPEndPoint(SACNCommon.GetMulticastAddress(universeId), Port),
                    SendBufferMem = sendBuffer.AsMemory()
                };
                universeSockets.TryAdd(universeId, socketData);
            }

            return socketData;
        }

        public void JoinDMXUniverse(ushort universeId)
        {
            if (dmxUniverses.Contains(universeId))
                throw new InvalidOperationException($"You have already joined the DMX Universe {universeId}");

            // Join group
            var option = new MulticastOption(SACNCommon.GetMulticastAddress(universeId), localEndPoint.Address);
            listenSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, option);

            // Add to the list of universes we have joined
            dmxUniverses.Add(universeId);
        }

        public void DropDMXUniverse(ushort universeId)
        {
            if (!dmxUniverses.Contains(universeId))
                throw new InvalidOperationException($"You are trying to drop the DMX Universe {universeId} but you are not a member");

            // Drop group
            var option = new MulticastOption(SACNCommon.GetMulticastAddress(universeId), this.localEndPoint.Address);
            listenSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.DropMembership, option);

            // Remove from the list of universes we have joined
            dmxUniverses.Remove(universeId);
        }

        /// <summary>
        /// Multicast send data
        /// </summary>
        /// <param name="universeId">The universe Id to multicast to</param>
        /// <param name="data">Up to 512 bytes of DMX data</param>
        /// <param name="priority">Priority (default 100)</param>
        /// <param name="syncAddress">Sync universe id</param>
        /// <param name="startCode">Start code (default 0)</param>
        public void SendMulticast(ushort universeId, ReadOnlyMemory<byte> data, byte priority = 100, ushort syncAddress = 0, byte startCode = 0)
        {
            byte sequenceId = GetNewSequenceId(universeId);

            var packet = new SACNDataPacket(universeId, SenderName, SenderId, sequenceId, data, priority, syncAddress, startCode);

            SendPacket(universeId, packet);
        }

        /// <summary>
        /// Unicast send data
        /// </summary>
        /// <param name="address">The address to unicast to</param>
        /// <param name="universeId">The Universe ID</param>
        /// <param name="data">Up to 512 bytes of DMX data</param>
        /// <param name="syncAddress">Sync universe id</param>
        /// <param name="startCode">Start code (default 0)</param>
        public void SendUnicast(IPAddress address, ushort universeId, ReadOnlyMemory<byte> data, byte priority = 100, ushort syncAddress = 0, byte startCode = 0)
        {
            byte sequenceId = GetNewSequenceId(universeId);

            var packet = new SACNDataPacket(universeId, SenderName, SenderId, sequenceId, data, priority, syncAddress, startCode);

            SendPacket(universeId, address, packet);
        }

        /// <summary>
        /// Multicast send sync
        /// </summary>
        /// <param name="syncAddress">Sync universe id</param>
        public void SendMulticastSync(ushort syncAddress)
        {
            byte sequenceId = GetNewSequenceIdSync(syncAddress);

            var packet = new SACNPacket(new RootLayer
            {
                UUID = SenderId,
                FramingLayer = new SyncFramingLayer
                {
                    SequenceId = sequenceId,
                    SyncAddress = syncAddress
                }
            });

            SendPacket(syncAddress, packet);
        }

        /// <summary>
        /// Unicast send sync
        /// </summary>
        /// <param name="syncAddress">Sync universe id</param>
        public void SendUnicastSync(IPAddress address, ushort syncAddress)
        {
            byte sequenceId = GetNewSequenceIdSync(syncAddress);

            var packet = new SACNPacket(new RootLayer
            {
                UUID = SenderId,
                FramingLayer = new SyncFramingLayer
                {
                    SequenceId = sequenceId,
                    SyncAddress = syncAddress
                }
            });
            SendPacket(syncAddress, address, packet);
        }

        /// <summary>
        /// Send packet
        /// </summary>
        /// <param name="universeId">Universe Id</param>
        /// <param name="destination">Destination</param>
        /// <param name="packet">Packet</param>
        public void SendPacket(ushort universeId, IPAddress destination, SACNPacket packet)
        {
            if (!endPointCache.TryGetValue(destination, out var ipEndPoint))
            {
                ipEndPoint = new IPEndPoint(destination, Port);
                endPointCache.Add(destination, ipEndPoint);
            }

            var memory = memoryPool.Rent(packet.Length);

            int packetLength = packet.WriteToBuffer(memory.Memory);

            var newSendData = new SendData
            {
                Data = memory,
                UniverseId = universeId,
                DataLength = packetLength,
                Destination = ipEndPoint
            };

            usedDestinations.Add((destination, universeId));

            if (IsOperational)
                sendQueue.Add(newSendData);
        }

        /// <summary>
        /// Send packet
        /// </summary>
        /// <param name="universeId">Universe Id</param>
        /// <param name="packet">Packet</param>
        public void SendPacket(ushort universeId, SACNPacket packet)
        {
            var memory = memoryPool.Rent(packet.Length);

            int packetLength = packet.WriteToBuffer(memory.Memory);

            var newSendData = new SendData
            {
                Data = memory,
                UniverseId = universeId,
                DataLength = packetLength
            };

            usedDestinations.Add((null, universeId));
            if (IsOperational)
            {
                sendQueue.Add(newSendData);
            }
            else
            {
                // Clear queue
                while (sendQueue.TryTake(out _));
            }
        }

        public void WarmUpSockets(IEnumerable<ushort> universeIds)
        {
            foreach (ushort universeId in universeIds)
            {
                GetSendSocket(universeId);
            }
        }

        private byte GetNewSequenceId(ushort universeId)
        {
            lock (lockObject)
            {
                sequenceIds.TryGetValue(universeId, out byte sequenceId);

                sequenceId++;

                sequenceIds[universeId] = sequenceId;

                return sequenceId;
            }
        }

        private byte GetNewSequenceIdSync(ushort syncAddress)
        {
            lock (lockObject)
            {
                sequenceIdsSync.TryGetValue(syncAddress, out byte sequenceId);

                sequenceId++;

                sequenceIdsSync[syncAddress] = sequenceId;

                return sequenceId;
            }
        }

        public void Dispose()
        {
            shutdownCTS.Cancel();

            foreach (var kvp in universeSockets)
            {
                try
                {
                    kvp.Value.Socket.Shutdown(SocketShutdown.Both);
                    kvp.Value.Socket.Close();
                    kvp.Value.Socket.Dispose();
                }
                catch
                {
                    // Ignore errors
                }
            }

            try
            {
                listenSocket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
                // Ignore errors
            }

            if (receiveTask?.IsCanceled == false)
            {
                receiveTask?.Wait();
            }

            receiveTask?.Dispose();

            if (sendTask?.IsCanceled == false)
            {
                sendTask?.Wait();
            }

            sendTask?.Dispose();

            listenSocket.Close();
            listenSocket.Dispose();
        }
    }
}
