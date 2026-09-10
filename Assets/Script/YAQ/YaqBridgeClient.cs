using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using YARG.Core.Logging;

namespace YARG.YAQ
{
    /// <summary>
    /// WebSocket client that talks to the YAQ server bridge (<c>/ws?role=yarg</c>).
    /// </summary>
    public sealed class YaqBridgeClient : IDisposable
    {
        public event Action Connected;
        public event Action Disconnected;
        public event Action<JObject> MessageReceived;

        private ClientWebSocket _socket;
        private CancellationTokenSource _cts;
        private readonly ConcurrentQueue<string> _outbound = new();
        private Task _loop;
        private string _url;

        public bool IsConnected =>
            _socket != null && _socket.State == WebSocketState.Open;

        public void Start(string url)
        {
            _url = url;
            Stop();
            _cts = new CancellationTokenSource();
            _loop = Task.Run(() => RunAsync(_cts.Token));
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
            }
            catch
            {
                // ignored
            }

            try
            {
                _socket?.Abort();
                _socket?.Dispose();
            }
            catch
            {
                // ignored
            }

            _socket = null;
            _cts = null;
        }

        public void Send(object payload)
        {
            var json = JsonConvert.SerializeObject(payload);
            _outbound.Enqueue(json);
        }

        public void Dispose() => Stop();

        private async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    _socket = new ClientWebSocket();
                    await _socket.ConnectAsync(new Uri(_url), token);
                    YargLogger.LogFormatInfo("YAQ bridge connected to {0}", _url);
                    Connected?.Invoke();
                    Send(new { type = "hello", version = "yarg-event-1" });

                    var buffer = new byte[1024 * 256];
                    while (_socket.State == WebSocketState.Open && !token.IsCancellationRequested)
                    {
                        while (_outbound.TryDequeue(out var outbound))
                        {
                            var bytes = Encoding.UTF8.GetBytes(outbound);
                            await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, token);
                        }

                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(250));
                        try
                        {
                            var result = await _socket.ReceiveAsync(buffer, timeoutCts.Token);
                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                break;
                            }

                            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                            var obj = JObject.Parse(json);
                            MessageReceived?.Invoke(obj);
                        }
                        catch (OperationCanceledException) when (!token.IsCancellationRequested)
                        {
                            // receive timeout — loop to flush outbound
                        }
                    }
                }
                catch (Exception ex) when (!token.IsCancellationRequested)
                {
                    YargLogger.LogFormatWarning("YAQ bridge disconnected: {0}", ex.Message);
                    Disconnected?.Invoke();
                    await Task.Delay(2000, token);
                }
            }
        }
    }

    [Serializable]
    public class YaqQueuePreview
    {
        public string setId;
        public string songHash;
        public string songName;
        public string songArtist;
        public List<YaqPreviewPlayer> players = new();
    }

    [Serializable]
    public class YaqPreviewPlayer
    {
        public string name;
        public string instrument;
        public string difficulty;
    }

    [Serializable]
    public class YaqSetPlayer
    {
        public string id;
        public string name;
        public string songHash;
        public string instrument;
        public string difficulty;
    }

    [Serializable]
    public class YaqPlaySet
    {
        public string id;
        public string songHash;
        public string songName;
        public string songArtist;
        public List<string> playerIds;
    }
}
