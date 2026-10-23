using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using YARG.Core.Logging;
using static YARG.Integration.SetlistProtocol;

namespace YARG.Integration
{
    /// <summary>
    /// One run of the setlist socket: a loopback listener and the background thread that serves it. Stopping
    /// abandons the instance, and a restart creates a new one, so an exiting thread never shares state with its
    /// successor.
    /// </summary>
    /// <remarks>
    /// Steady state allocates nothing once clients are connected; only accepting a connection and logging do.
    /// </remarks>
    internal sealed class SetlistSocketServer
    {
        private const int MAX_CLIENTS        = 4;
        private const int MAX_PENDING        = 4;
        private const int ACCEPTS_PER_SECOND = 4;
        private const int SEND_BUFFER_SIZE   = 256 * 1024;
        private const int WAKE_INTERVAL_MS   = 10;

        private static readonly long HandshakeTimeout = Stopwatch.Frequency * 2;
        private static readonly long AckTimeout       = Stopwatch.Frequency * 10;
        private static readonly long LingerTimeout    = Stopwatch.Frequency;

        private enum ClientState
        {
            Free,
            Handshake,
            Ready,

            /// <summary>
            /// Sent a non-ok hello and shut down sending; waiting for the client to close, so the client reads the
            /// status instead of a reset.
            /// </summary>
            Closing,
        }

        private sealed class Client
        {
            public Socket      Socket;
            public ClientState State;
            public long        Deadline;

            public readonly byte[] Received = new byte[FRAME_HEADER_SIZE + MAX_CLIENT_FRAME];
            public int ReceivedCount;

            public uint SentRevision;
            public bool AwaitingAck;
            public long SentAt;
        }

        public int Port { get; }

        private readonly Socket         _listener;
        private readonly Thread         _thread;
        private readonly AutoResetEvent _wake = new(false);
        private volatile bool           _stopping;

        private readonly Client[] _clients = new Client[MAX_CLIENTS + MAX_PENDING];
        private readonly ulong    _sessionNonce;
        private readonly byte[]   _helloBuffer    = new byte[SERVER_HELLO_SIZE];
        private readonly byte[]   _snapshotBuffer = new byte[MAX_SNAPSHOT_BYTES];
        private int  _snapshotLength;
        private uint _snapshotRevision;

        private long _acceptWindowStart;
        private int  _acceptsInWindow;
        private long _lastLogAt;

        // A triple buffer: the main thread fills _writing, the socket thread encodes _reading, and _latest is
        // swapped between them under the lock, so neither side ever waits for the other's work.
        private readonly object _swapLock = new();
        private SetlistSnapshot _writing = new();
        private SetlistSnapshot _latest  = new();
        private SetlistSnapshot _reading = new();
        private bool _hasLatest;
        private uint _publishedRevision;

        /// <summary>
        /// Binds 127.0.0.1:<paramref name="port"/> on the calling thread, so a port in use throws here.
        /// </summary>
        /// <exception cref="SocketException">The port couldn't be bound.</exception>
        public static SetlistSocketServer Start(int port)
        {
            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                listener.Bind(new IPEndPoint(IPAddress.Loopback, port));
                listener.Listen(MAX_PENDING);
            }
            catch
            {
                listener.Dispose();
                throw;
            }

            var server = new SetlistSocketServer(listener, port);
            server._thread.Start();
            return server;
        }

        private SetlistSocketServer(Socket listener, int port)
        {
            _listener = listener;
            Port = port;
            _sessionNonce = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0);

            for (int i = 0; i < _clients.Length; i++)
            {
                _clients[i] = new Client();
            }

            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = "YARG Setlist Socket",
            };
        }

        /// <summary>
        /// The snapshot to fill before calling <see cref="Publish"/>. Main thread only.
        /// </summary>
        public SetlistSnapshot PendingSnapshot => _writing;

        /// <summary>
        /// Hands <see cref="PendingSnapshot"/> to the socket thread. Main thread only.
        /// </summary>
        public void Publish()
        {
            _writing.Revision = ++_publishedRevision;
            lock (_swapLock)
            {
                (_writing, _latest) = (_latest, _writing);
                _hasLatest = true;
            }

            _wake.Set();
        }

        /// <summary>
        /// Returns at once; the socket thread closes its clients and exits within a few milliseconds.
        /// </summary>
        public void Stop()
        {
            _stopping = true;
            _wake.Set();

            // Frees the port now, so a restart can bind it straight away
            _listener.Close();
        }

        private void Run()
        {
            try
            {
                while (!_stopping)
                {
                    _wake.WaitOne(WAKE_INTERVAL_MS);
                    if (_stopping)
                    {
                        break;
                    }

                    long now = Stopwatch.GetTimestamp();
                    AcceptClients(now);
                    ServiceClients(now);

                    if (TakeLatestSnapshot())
                    {
                        _snapshotLength = WriteSnapshot(_snapshotBuffer, _reading);
                        _snapshotRevision = _reading.Revision;
                    }

                    SendSnapshots(now);
                }
            }
            catch (Exception) when (_stopping)
            {
                // Stop closed the listener under us
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Setlist socket failed");
            }
            finally
            {
                foreach (var client in _clients)
                {
                    if (client.State != ClientState.Free)
                    {
                        Drop(client);
                    }
                }
            }
        }

        private void AcceptClients(long now)
        {
            if (now - _acceptWindowStart >= Stopwatch.Frequency)
            {
                _acceptWindowStart = now;
                _acceptsInWindow = 0;
            }

            // Past these limits connections wait in the listen backlog, and the OS refuses them once it's full, so a
            // connect loop costs at most a few accepted sockets a second.
            while (_acceptsInWindow < ACCEPTS_PER_SECOND && CountClients(ClientState.Handshake) < MAX_PENDING &&
                _listener.Poll(0, SelectMode.SelectRead))
            {
                var client = FindFreeClient();
                if (client is null)
                {
                    return;
                }

                Socket socket;
                try
                {
                    socket = _listener.Accept();
                }
                catch (SocketException)
                {
                    // The connection was reset before we got to it
                    continue;
                }

                _acceptsInWindow++;
                socket.NoDelay = true;
                socket.SendBufferSize = SEND_BUFFER_SIZE;

                client.Socket = socket;
                client.State = ClientState.Handshake;
                client.Deadline = now + HandshakeTimeout;
                client.ReceivedCount = 0;
                client.SentRevision = 0;
                client.AwaitingAck = false;
            }
        }

        private void ServiceClients(long now)
        {
            foreach (var client in _clients)
            {
                if (client.State == ClientState.Free)
                {
                    continue;
                }

                bool expired = client.State switch
                {
                    ClientState.Handshake or ClientState.Closing => now > client.Deadline,
                    ClientState.Ready => client.AwaitingAck && client.SentRevision != _snapshotRevision &&
                        now - client.SentAt > AckTimeout,
                    _ => false,
                };

                if (expired || !Receive(client))
                {
                    Drop(client);
                    continue;
                }

                switch (client.State)
                {
                    case ClientState.Handshake:
                        HandleHello(client, now);
                        break;
                    case ClientState.Ready:
                        if (!HandleFrames(client))
                        {
                            Drop(client);
                        }
                        break;
                    case ClientState.Closing:
                        client.ReceivedCount = 0;
                        break;
                }
            }
        }

        /// <summary>
        /// Reads whatever has arrived without blocking. Returns false when the client has gone.
        /// </summary>
        private static bool Receive(Client client)
        {
            if (!client.Socket.Poll(0, SelectMode.SelectRead))
            {
                return true;
            }

            // The buffer holds the largest allowed frame, and complete frames are consumed after every read
            int space = client.Received.Length - client.ReceivedCount;
            int read = client.Socket.Receive(client.Received, client.ReceivedCount, space, SocketFlags.None,
                out var error);
            if (error != SocketError.Success || read == 0)
            {
                return false;
            }

            client.ReceivedCount += read;
            return true;
        }

        private void HandleHello(Client client, long now)
        {
            var received = client.Received.AsSpan(0, client.ReceivedCount);
            if (!IsHelloPrefix(received))
            {
                Drop(client);
                return;
            }

            if (received.Length < CLIENT_HELLO_SIZE)
            {
                return;
            }

            ReadClientHello(received, out ushort minVersion, out ushort maxVersion);
            var status = HelloStatus.Ok;
            if (minVersion > VERSION || maxVersion < VERSION)
            {
                status = HelloStatus.NoCommonVersion;
            }
            else if (CountClients(ClientState.Ready) >= MAX_CLIENTS)
            {
                status = HelloStatus.Busy;
            }

            ushort version = status == HelloStatus.Ok ? VERSION : (ushort) 0;
            int length = WriteServerHello(_helloBuffer, version, status, _sessionNonce);
            client.Socket.Send(_helloBuffer, 0, length, SocketFlags.None, out var error);
            if (error != SocketError.Success)
            {
                Drop(client);
                return;
            }

            if (status != HelloStatus.Ok)
            {
                try
                {
                    client.Socket.Shutdown(SocketShutdown.Send);
                }
                catch (SocketException)
                {
                    Drop(client);
                    return;
                }

                client.State = ClientState.Closing;
                client.Deadline = now + LingerTimeout;
                client.ReceivedCount = 0;
                return;
            }

            client.State = ClientState.Ready;
            Log("Setlist socket client connected");

            int extra = client.ReceivedCount - CLIENT_HELLO_SIZE;
            Buffer.BlockCopy(client.Received, CLIENT_HELLO_SIZE, client.Received, 0, extra);
            client.ReceivedCount = extra;
            if (!HandleFrames(client))
            {
                Drop(client);
            }
        }

        /// <summary>
        /// Consumes every complete frame received. Returns false on a frame that breaks the protocol.
        /// </summary>
        private static bool HandleFrames(Client client)
        {
            int offset = 0;
            while (true)
            {
                var remaining = client.Received.AsSpan(offset, client.ReceivedCount - offset);
                int size = ReadClientFrame(remaining, out var type, out var payload);
                if (size < 0)
                {
                    return false;
                }

                if (size == 0)
                {
                    break;
                }

                if (type == FrameType.Ack && TryReadAck(payload, out uint revision) && revision == client.SentRevision)
                {
                    client.AwaitingAck = false;
                }

                offset += size;
            }

            Buffer.BlockCopy(client.Received, offset, client.Received, 0, client.ReceivedCount - offset);
            client.ReceivedCount -= offset;
            return true;
        }

        private bool TakeLatestSnapshot()
        {
            lock (_swapLock)
            {
                if (!_hasLatest)
                {
                    return false;
                }

                (_reading, _latest) = (_latest, _reading);
                _hasLatest = false;
            }

            return true;
        }

        private void SendSnapshots(long now)
        {
            if (_snapshotLength == 0)
            {
                return;
            }

            foreach (var client in _clients)
            {
                if (client.State != ClientState.Ready || client.AwaitingAck || client.SentRevision == _snapshotRevision)
                {
                    continue;
                }

                // Mono's Send doesn't return until every byte is handed to the OS, so a send that doesn't fit would
                // block this thread for as long as the client stalls. The client has acknowledged everything sent
                // before, so its send buffer is empty, and a snapshot is well under SEND_BUFFER_SIZE.
                client.Socket.Send(_snapshotBuffer, 0, _snapshotLength, SocketFlags.None, out var error);
                if (error != SocketError.Success)
                {
                    Drop(client);
                    continue;
                }

                client.SentRevision = _snapshotRevision;
                client.AwaitingAck = true;
                client.SentAt = now;
            }
        }

        private void Drop(Client client)
        {
            if (client.State == ClientState.Ready)
            {
                Log("Setlist socket client disconnected");
            }

            client.Socket.Close();
            client.Socket = null;
            client.State = ClientState.Free;
        }

        private Client FindFreeClient()
        {
            foreach (var client in _clients)
            {
                if (client.State == ClientState.Free)
                {
                    return client;
                }
            }

            return null;
        }

        private int CountClients(ClientState state)
        {
            int count = 0;
            foreach (var client in _clients)
            {
                if (client.State == state)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// At most one line a second, so a client that connects in a loop can't flood the log.
        /// </summary>
        private void Log(string message)
        {
            long now = Stopwatch.GetTimestamp();
            if (now - _lastLogAt < Stopwatch.Frequency)
            {
                return;
            }

            _lastLogAt = now;
            YargLogger.LogInfo(message);
        }
    }
}
