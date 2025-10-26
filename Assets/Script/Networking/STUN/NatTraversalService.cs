using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Networking;
using kcp2k;
using Mirror;

namespace YARG.Networking.STUN
{
    /// <summary>
    /// Coordinates STUN probing, NAT punching, and lightweight relay responses for the active transport.
    /// </summary>
    public sealed class NatTraversalService : MonoBehaviour
    {
        public static NatTraversalService Instance { get; private set; }

        [SerializeField]
        private ushort udpPunchPort = NetworkTransportDefaults.DefaultUdpPort;

        [SerializeField]
        private List<string> stunServers = new();

        [SerializeField]
        private int stunTimeoutMilliseconds = 2_500;

        [SerializeField]
        private int cacheTtlSeconds = NetworkTransportDefaults.DefaultStunCacheTtlSeconds;

        private NatTraversalResult _cachedResult;
        private CancellationTokenSource _keepAliveCts;
        private CancellationTokenSource _punchSendCts;
        private KcpTransport _kcpTransport;
        private readonly Dictionary<string, PendingStunRequest> _pendingServerStun = new();
        private readonly object _stunSync = new();

        private static readonly byte[] PunchPayload = Encoding.ASCII.GetBytes("YARG_PUNCH");
        private const int RESPONSE_BURST_COUNT = 10;
        private static readonly TimeSpan RESPONSE_INTERVAL = TimeSpan.FromMilliseconds(200);

        public ushort PunchPort => udpPunchPort;
        public NatTraversalResult CachedResult => _cachedResult;

        public event Action<IPEndPoint, byte[]> PunchPacketReceived;
        public event Action<NatTraversalResult> PublicEndpointChanged;

        private sealed class PendingStunRequest
        {
            public PendingStunRequest(byte[] transactionId, string server, UniTaskCompletionSource<NatTraversalResult> completion)
            {
                TransactionId = transactionId;
                Server = server;
                Completion = completion;
            }

            public byte[] TransactionId { get; }
            public string Server { get; }
            public UniTaskCompletionSource<NatTraversalResult> Completion { get; }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (stunServers.Count == 0)
            {
                stunServers.AddRange(NetworkTransportDefaults.DefaultStunServers);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            StopKeepAlive();
            StopHolePunch();
            AttachTransport(null);
        }

        public async UniTask<NatTraversalResult> ProbeAsync(bool forceRefresh, CancellationToken cancellationToken)
        {
            if (!forceRefresh && _cachedResult != null && !_cachedResult.IsExpired(TimeSpan.FromSeconds(cacheTtlSeconds)))
            {
                return _cachedResult;
            }

            if (stunServers.Count == 0)
            {
                throw new StunException("No STUN servers configured");
            }

            StunException lastError = null;
            foreach (var server in stunServers)
            {
                if (string.IsNullOrWhiteSpace(server))
                {
                    continue;
                }

                string trimmed = server.Trim();

                try
                {
                    NatTraversalResult result;
                    if (NetworkServer.active && _kcpTransport != null)
                    {
                        try
                        {
                            result = await ProbeWithTransportSocketAsync(trimmed, cancellationToken);
                        }
                        catch (StunException transportEx)
                        {
                            Debug.LogWarning($"[NatTraversalService] Transport STUN probe via {trimmed} failed ({transportEx.Message}). Falling back to standalone socket.");
                            result = await StunClient.QueryAsync(trimmed, stunTimeoutMilliseconds, cancellationToken);
                        }
                    }
                    else
                    {
                        result = await StunClient.QueryAsync(trimmed, stunTimeoutMilliseconds, cancellationToken);
                    }

                    if (result != null)
                    {
                        if (NetworkServer.active && _kcpTransport != null)
                        {
                            result.LocalEndPoint = new IPEndPoint(IPAddress.Any, _kcpTransport.Port);
                        }

                        _cachedResult = result;
                        NotifyPublicEndpointChanged();
                        return _cachedResult;
                    }
                }
                catch (StunException ex)
                {
                    lastError = ex;
                    Debug.LogWarning($"[NatTraversalService] STUN probe failed via {trimmed}: {ex.Message}");
                }
            }

            throw lastError ?? new StunException("STUN probing failed");
        }

        private void NotifyPublicEndpointChanged()
        {
            if (_cachedResult == null)
            {
                return;
            }

            try
            {
                PublicEndpointChanged?.Invoke(_cachedResult);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NatTraversalService] PublicEndpointChanged invocation threw: {ex}");
            }
        }

        public void BeginKeepAlive(TimeSpan? interval = null)
        {
            if (_keepAliveCts != null)
            {
                return;
            }

            TimeSpan cadence = interval ?? TimeSpan.FromSeconds(Mathf.Max(5, cacheTtlSeconds / 2));
            _keepAliveCts = new CancellationTokenSource();
            KeepAliveLoop(cadence, _keepAliveCts.Token).Forget();
        }

        public void StopKeepAlive()
        {
            if (_keepAliveCts == null)
            {
                return;
            }

            _keepAliveCts.Cancel();
            _keepAliveCts.Dispose();
            _keepAliveCts = null;
        }

        private async UniTask KeepAliveLoop(TimeSpan interval, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await ProbeAsync(true, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[NatTraversalService] STUN keep-alive failed: {ex.Message}");
                }

                try
                {
                    await UniTask.Delay(interval, cancellationToken: token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        public void AttachTransport(KcpTransport transport)
        {
            if (_kcpTransport == transport)
            {
                return;
            }

            if (_kcpTransport != null)
            {
                _kcpTransport.ServerRawPacket -= OnServerRawPacket;
                _kcpTransport.ClientRawPacket -= OnClientRawPacket;
            }

            _kcpTransport = transport;

            if (_kcpTransport != null)
            {
                _kcpTransport.ServerRawPacket += OnServerRawPacket;
                _kcpTransport.ClientRawPacket += OnClientRawPacket;
            }
        }

        public void ConfigurePunchPort(ushort port)
        {
            if (port == 0)
            {
                return;
            }

            udpPunchPort = port;
        }

        private async UniTask<NatTraversalResult> ProbeWithTransportSocketAsync(string stunServer, CancellationToken token)
        {
            if (_kcpTransport == null)
            {
                throw new StunException("Transport unavailable for STUN probe.");
            }

            var (host, port) = ParseStunEndpoint(stunServer);

            IPAddress[] addresses;
            try
            {
                addresses = await Dns.GetHostAddressesAsync(host);
                if (addresses == null || addresses.Length == 0)
                {
                    throw new StunException($"Host '{host}' resolved to no addresses.");
                }
            }
            catch (Exception ex) when (ex is SocketException || ex is ArgumentException)
            {
                throw new StunException($"Failed to resolve STUN host '{host}'", ex);
            }

            var orderedAddresses = addresses
                .Where(ip => ip != null && (ip.AddressFamily == AddressFamily.InterNetwork || ip.AddressFamily == AddressFamily.InterNetworkV6))
                .OrderBy(ip => ip.AddressFamily == AddressFamily.InterNetwork ? 0 : 1)
                .ToArray();

            if (orderedAddresses.Length == 0)
            {
                throw new StunException($"STUN host '{host}' did not resolve to any IPv4/IPv6 addresses.");
            }

            foreach (var resolved in orderedAddresses)
            {
                token.ThrowIfCancellationRequested();

                IPAddress address = resolved;
                if (address.IsIPv4MappedToIPv6)
                {
                    address = address.MapToIPv4();
                }

                if (address.AddressFamily == AddressFamily.InterNetworkV6 && (_kcpTransport == null || !_kcpTransport.DualMode))
                {
                    continue;
                }

                var endPoint = new IPEndPoint(address, port);
                var request = StunClient.BuildBindingRequest(out var transactionId);
                var completion = new UniTaskCompletionSource<NatTraversalResult>();
                var key = ToTransactionKey(transactionId);

                lock (_stunSync)
                {
                    _pendingServerStun[key] = new PendingStunRequest(transactionId, stunServer, completion);
                }

                bool sent = _kcpTransport.TrySendServerRaw(endPoint, new ArraySegment<byte>(request));
                if (!sent)
                {
                    lock (_stunSync)
                    {
                        _pendingServerStun.Remove(key);
                    }
                    continue;
                }

                var timeoutTask = UniTask.Delay(stunTimeoutMilliseconds);
                var cancelTask = UniTask.WaitUntilCanceled(token);

                int completedIndex;
                try
                {
                    completedIndex = await UniTask.WhenAny(completion.Task, timeoutTask, cancelTask);
                }
                finally
                {
                    lock (_stunSync)
                    {
                        _pendingServerStun.Remove(key);
                    }
                }

                if (completedIndex == 0)
                {
                    return await completion.Task;
                }

                if (completedIndex == 2)
                {
                    token.ThrowIfCancellationRequested();
                }

                Debug.LogWarning($"[NatTraversalService] STUN probe timed out via {stunServer} ({address}).");
            }

            throw new StunException("STUN response timed out");
        }

        public void BeginHolePunch(IPEndPoint remoteEndPoint, string context = null, TimeSpan? duration = null)
        {
            if (remoteEndPoint == null)
            {
                Debug.LogWarning("[NatTraversalService] Hole punch failed: remote endpoint missing");
                return;
            }

            if (remoteEndPoint.Address.Equals(IPAddress.None) || remoteEndPoint.Port <= 0)
            {
                Debug.LogWarning($"[NatTraversalService] Hole punch skipped for invalid target {remoteEndPoint}");
                return;
            }

            if (_kcpTransport == null)
            {
                Debug.LogWarning("[NatTraversalService] Hole punch failed: transport unavailable");
                return;
            }

            StopHolePunch();

            _punchSendCts = new CancellationTokenSource();
            var punchDuration = duration ?? TimeSpan.FromSeconds(NetworkTransportDefaults.DefaultPunchRetryWindowSeconds);
            PunchSendLoop(remoteEndPoint, punchDuration, context ?? string.Empty, _punchSendCts.Token).Forget();
        }

        public void StopHolePunch()
        {
            if (_punchSendCts == null)
            {
                return;
            }

            _punchSendCts.Cancel();
            _punchSendCts.Dispose();
            _punchSendCts = null;
        }

        public void BeginHolePunchBurst(IPEndPoint remoteEndPoint, string context = null, int burstCount = RESPONSE_BURST_COUNT, TimeSpan? interval = null)
        {
            if (remoteEndPoint == null)
            {
                return;
            }

            RespondToPunch(remoteEndPoint, PunchPayload, burstCount, interval ?? RESPONSE_INTERVAL, this.GetCancellationTokenOnDestroy(), context ?? string.Empty).Forget();
        }

        private async UniTaskVoid PunchSendLoop(IPEndPoint remoteEndPoint, TimeSpan duration, string context, CancellationToken token)
        {
            var natType = _cachedResult?.NatType ?? NetworkNatType.Unknown;
            Debug.Log($"[NatTraversalService] Starting UDP punch to {remoteEndPoint} (context: {context}, local port: {udpPunchPort}, NAT: {natType})");

            using var lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
            var combinedToken = lifetimeCts.Token;
            int sentCount = 0;
            var deadline = DateTime.UtcNow + duration;

            try
            {
                while (!combinedToken.IsCancellationRequested && DateTime.UtcNow < deadline)
                {
                    await SendPunchPayloadAsync(remoteEndPoint, PunchPayload, combinedToken);
                    sentCount++;

                    try
                    {
                        await UniTask.Delay(TimeSpan.FromMilliseconds(250), cancellationToken: combinedToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // cancellation requested
            }
            finally
            {
                Debug.Log($"[NatTraversalService] Punch routine finished after {sentCount} sends (context: {context})");
            }
        }

        private async UniTaskVoid RespondToPunch(IPEndPoint remoteEndPoint, byte[] payload, int bursts, TimeSpan interval, CancellationToken token, string context)
        {
            if (remoteEndPoint == null || payload == null)
            {
                return;
            }

            using var lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
            var combinedToken = lifetimeCts.Token;

            for (int i = 0; i < bursts && !combinedToken.IsCancellationRequested; i++)
            {
                await SendPunchPayloadAsync(remoteEndPoint, payload, combinedToken);

                if (interval > TimeSpan.Zero && i < bursts - 1)
                {
                    try
                    {
                        await UniTask.Delay(interval, cancellationToken: combinedToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async UniTask SendPunchPayloadAsync(IPEndPoint remoteEndPoint, byte[] payload, CancellationToken token)
        {
            if (_kcpTransport == null || payload == null)
            {
                return;
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            var segment = new ArraySegment<byte>(payload);

            if (remoteEndPoint != null && NetworkServer.active)
            {
                _kcpTransport.TrySendServerRaw(remoteEndPoint, segment);
            }

            if (!NetworkServer.active)
            {
                _kcpTransport.TrySendClientRaw(segment);
            }

        }

        private bool TryHandleServerStunResponse(ArraySegment<byte> segment)
        {
            if (segment.Array == null || segment.Count < 20)
            {
                return false;
            }

            var span = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);

            // STUN messages always start with the first two bits cleared.
            if ((span[0] & 0b1100_0000) != 0)
            {
                return false;
            }

            Span<byte> transactionId = stackalloc byte[12];
            span.Slice(8, 12).CopyTo(transactionId);
            PendingStunRequest pending;
            lock (_stunSync)
            {
                if (!_pendingServerStun.TryGetValue(ToTransactionKey(transactionId), out pending))
                {
                    return false;
                }
            }

            if (!StunClient.TryParseBindingResponse(span, pending.TransactionId, out var mapped, out var natType))
            {
                return false;
            }

            var result = new NatTraversalResult
            {
                NatType = natType,
                PublicEndPoint = mapped ?? new IPEndPoint(IPAddress.None, 0),
                LocalEndPoint = new IPEndPoint(IPAddress.Any, _kcpTransport != null ? _kcpTransport.Port : 0),
                StunServer = pending.Server,
                Timestamp = DateTimeOffset.UtcNow,
                IsTransportSocketResult = true
            };

            pending.Completion.TrySetResult(result);
            return true;
        }

        private bool OnServerRawPacket(ArraySegment<byte> segment, IPEndPoint remoteEndPoint)
        {
            if (TryHandleServerStunResponse(segment))
            {
                return true;
            }

            if (!IsPunchPayload(segment))
            {
                return false;
            }

            PunchPacketReceived?.Invoke(remoteEndPoint, PunchPayload);
            RespondToPunch(remoteEndPoint, PunchPayload, RESPONSE_BURST_COUNT, RESPONSE_INTERVAL, this.GetCancellationTokenOnDestroy(), "auto-response").Forget();
            return true;
        }

        private bool OnClientRawPacket(ArraySegment<byte> segment)
        {
            if (!IsPunchPayload(segment))
            {
                return false;
            }

            return true;
        }

        private static bool IsPunchPayload(ArraySegment<byte> payload)
        {
            if (payload.Array == null || payload.Count < PunchPayload.Length)
            {
                return false;
            }

            for (int i = 0; i < PunchPayload.Length; i++)
            {
                if (payload.Array[payload.Offset + i] != PunchPayload[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsPunchPayload(byte[] payload)
        {
            if (payload == null)
            {
                return false;
            }

            return IsPunchPayload(new ArraySegment<byte>(payload));
        }

        private static string ToTransactionKey(ReadOnlySpan<byte> transactionId)
        {
            return Convert.ToBase64String(transactionId.ToArray());
        }

        private static string ToTransactionKey(byte[] transactionId)
        {
            return Convert.ToBase64String(transactionId);
        }

        private static (string host, int port) ParseStunEndpoint(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ArgumentException("STUN endpoint required", nameof(endpoint));
            }

            string trimmed = endpoint.Trim();

            if (trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                int closing = trimmed.IndexOf(']');
                if (closing > 0)
                {
                    string host = trimmed.Substring(1, closing - 1);
                    string remainder = trimmed.Substring(closing + 1);
                    if (remainder.StartsWith(":", StringComparison.Ordinal) && int.TryParse(remainder.Substring(1), out var ipv6Port))
                    {
                        return (host, ipv6Port);
                    }

                    return (host, 3478);
                }
            }

            string[] segments = trimmed.Split(':');
            if (segments.Length == 1)
            {
                return (segments[0], 3478);
            }

            if (segments.Length == 2 && int.TryParse(segments[1], out var parsedPort))
            {
                return (segments[0], parsedPort);
            }

            throw new ArgumentException($"Unable to parse STUN endpoint '{endpoint}'", nameof(endpoint));
        }
    }
}
