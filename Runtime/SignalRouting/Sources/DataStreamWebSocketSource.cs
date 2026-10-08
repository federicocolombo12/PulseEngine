using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace GenericDataStreaming
{
    /// <summary>
    /// Sorgente dati WebSocket per ricevere flussi biometrici da server remoti,
    /// bridge Python, gateway IoT o applicazioni web.
    /// Compatibile con .NET nativo e Standalone VR (Quest).
    /// </summary>
    [AddComponentMenu("Generic Data Streaming/Sources/WebSocket Source")]
    [ExecuteAlways]
    public class DataStreamWebSocketSource : MonoBehaviour, IDataStreamSource
    {
        public string ProtocolName => "WebSocket Client";
        public bool IsConnected => webSocket != null && webSocket.State == WebSocketState.Open;

        [Header("Connection Settings")]
        [Tooltip("Indirizzo del server WebSocket (es: ws://127.0.0.1:8080 o wss://server.com/stream).")]
        public string serverUri = "ws://127.0.0.1:8080";

        [Tooltip("Connetti automaticamente all'avvio o all'attivazione del componente.")]
        public bool autoConnect = false;

        private ClientWebSocket webSocket;
        private CancellationTokenSource cts;
        private ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();
        private bool isConnecting = false;

        void OnEnable()
        {
            if (autoConnect)
            {
                StartStreaming();
            }
        }

        void OnDisable()
        {
            StopStreaming();
        }

        public void StartStreaming()
        {
            if (IsConnected || isConnecting) return;
            ConnectAsync();
        }

        public void StopStreaming()
        {
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
                cts = null;
            }

            if (webSocket != null)
            {
                try
                {
                    if (webSocket.State == WebSocketState.Open)
                    {
                        webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).Wait(500);
                    }
                }
                catch { }
                webSocket.Dispose();
                webSocket = null;
            }

            isConnecting = false;
            Debug.Log("[DataStreamWebSocketSource] Disconnesso.");
        }

        private async void ConnectAsync()
        {
            isConnecting = true;
            cts = new CancellationTokenSource();
            webSocket = new ClientWebSocket();

            try
            {
                Uri uri = new Uri(serverUri);
                Debug.Log($"[DataStreamWebSocketSource] Connessione a {uri}...");
                await webSocket.ConnectAsync(uri, cts.Token);
                Debug.Log($"[DataStreamWebSocketSource] Connesso con successo a {uri}!");
                isConnecting = false;
                _ = ReceiveLoopAsync(cts.Token);
            }
            catch (Exception ex)
            {
                isConnecting = false;
                Debug.LogWarning($"[DataStreamWebSocketSource] Errore connessione WebSocket: {ex.Message}");
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            var buffer = new byte[4096];

            while (!token.IsCancellationRequested && webSocket != null && webSocket.State == WebSocketState.Open)
            {
                try
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by server", CancellationToken.None);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        ParseAndEnqueue(message);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DataStreamWebSocketSource] Errore ricezione: {ex.Message}");
                    break;
                }
            }
        }

        private void ParseAndEnqueue(string message)
        {
            // Supporta messaggi in formato:
            // 1. Linea singola "channel:value" (es: "heart_rate:75.5")
            // 2. Più linee o virgole "heart_rate:75,spo2:98"
            // 3. JSON standard (estrazione semplice per non richiedere librerie pesanti)

            mainThreadActions.Enqueue(() =>
            {
                if (DataStreamRegistry.Instance == null) return;

                message = message.Trim();
                if (message.StartsWith("{") && message.EndsWith("}"))
                {
                    // JSON parsing semplice: "key": value
                    string clean = message.Substring(1, message.Length - 2);
                    string[] pairs = clean.Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var pair in pairs)
                    {
                        string[] kv = pair.Split(':');
                        if (kv.Length >= 2)
                        {
                            string key = kv[0].Replace("\"", "").Trim();
                            string valStr = kv[1].Replace("\"", "").Trim();
                            if (float.TryParse(valStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float val))
                            {
                                DataStreamRegistry.Instance.PushValue(key, val);
                            }
                        }
                    }
                }
                else
                {
                    // Formato delimitato
                    string[] items = message.Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var item in items)
                    {
                        string[] parts = item.Split(':');
                        if (parts.Length == 2)
                        {
                            string channelId = parts[0].Trim();
                            if (float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float val))
                            {
                                DataStreamRegistry.Instance.PushValue(channelId, val);
                            }
                        }
                    }
                }
            });
        }

        void Update()
        {
            // Esegue i dati accumulati sul Main Thread di Unity
            while (mainThreadActions.TryDequeue(out var action))
            {
                action?.Invoke();
            }
        }
    }
}
