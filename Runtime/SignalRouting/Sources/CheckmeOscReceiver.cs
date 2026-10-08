using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System;
using System.Threading;
using UnityEngine.Events;
using GenericDataStreaming;

namespace MedicalXR.Checkme
{
    /// <summary>
    /// Ricevitore OSC / UDP leggero per telemetria bio-segnale e sensori indossabili (es. Checkme Pro BLE Bridge, ZIG SIM, TouchDesigner).
    /// Decodifica pacchetti Open Sound Control in un thread asincrono a bassa latenza e instrada verso il DataStreamRegistry.
    /// </summary>
    [ExecuteAlways]
    public class CheckmeOscReceiver : MonoBehaviour, IDataStreamSource
    {
        [Header("Network Settings")]
        public int port = 7000;

        [Header("Bio-Signal Data (Read Only)")]
        public int heartRate;
        public int spo2;
        public float hrvRmssd;
        public float hrvSdnn;
        public int perfusionIndex;
        public bool rWaveTriggered;
        public event Action OnRWaveTriggered;

        [Tooltip("Se disattivato, non invierà trigger grezzi al DataStreamRegistry (utile se usi il Metronomo interno per fluidità).")]
        public bool emitNetworkTriggers = false;

        [Header("Events")]
        [Tooltip("UnityEvent scatenato all'arrivo dell'onda R / battito cardiaco.")]
        public UnityEvent onRWaveReceived;

        // Implementazione IDataStreamSource
        public string ProtocolName => "Checkme OSC / UDP Receiver";
        public bool IsConnected => isRunning && client != null;

        private Thread receiveThread;
        private UdpClient client;
        private bool isRunning;
        private bool rWavePending;
        private readonly object lockObject = new object();

        void OnEnable()
        {
            StartStreaming();
        }

        public void StartStreaming()
        {
            if (isRunning) return;
            isRunning = true;
            receiveThread = new Thread(new ThreadStart(ReceiveData));
            receiveThread.IsBackground = true;
            receiveThread.Start();
            Debug.Log($"[Checkme] Receiver avviato sulla porta {port}");
        }

        public void StopStreaming()
        {
            isRunning = false;
            if (client != null)
            {
                try { client.Close(); } catch { }
                client = null;
            }
        }

        void OnDisable()
        {
            StopStreaming();
        }

        void Update()
        {
            bool trigger = false;
            lock (lockObject)
            {
                if (rWavePending)
                {
                    trigger = true;
                    rWavePending = false;
                }
            }

            if (trigger)
            {
                rWaveTriggered = true;
                OnRWaveTriggered?.Invoke();
                onRWaveReceived?.Invoke();

                // Invia l'evento trigger al registro generico (disattivabile se si usa il Metronomo interno)
                if (emitNetworkTriggers && DataStreamRegistry.Instance != null)
                {
                    DataStreamRegistry.Instance.PushTrigger("heartbeat");
                }
            }

            // Invia i valori continui al registro generico ad ogni frame
            if (DataStreamRegistry.Instance != null)
            {
                DataStreamRegistry.Instance.PushValue("heart_rate", heartRate);
                DataStreamRegistry.Instance.PushValue("spo2", spo2);
                DataStreamRegistry.Instance.PushValue("perfusion_index", perfusionIndex);
                DataStreamRegistry.Instance.PushValue("hrv_rmssd", hrvRmssd);
                DataStreamRegistry.Instance.PushValue("hrv_sdnn", hrvSdnn);
            }
        }

        void LateUpdate()
        {
            rWaveTriggered = false;
        }

        private void ReceiveData()
        {
            try
            {
                client = new UdpClient(port);
                IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);

                while (isRunning)
                {
                    try
                    {
                        byte[] data = client.Receive(ref anyIP);
                        ParseOscMessage(data);
                    }
                    catch (SocketException)
                    {
                        // Socket closed on disable
                        break;
                    }
                    catch (Exception e)
                    {
                        if (isRunning) Debug.LogError($"[Checkme] Errore ricezione: {e.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                if (isRunning) Debug.LogError($"[Checkme] Errore bind porta {port}: {ex.Message}");
            }
        }

        private void ParseOscMessage(byte[] data)
        {
            // Estraggo l'indirizzo esatto per evitare falsi positivi (es: /checkme/hrv/rmssd che inizia con /checkme/hr)
            string address = GetOscAddress(data);
            
            if (address == "/checkme/hr")
            {
                heartRate = ExtractInt(data);
            }
            else if (address == "/checkme/spo2")
            {
                spo2 = ExtractInt(data);
            }
            else if (address == "/checkme/pi")
            {
                perfusionIndex = ExtractInt(data);
            }
            else if (address == "/checkme/hrv/rmssd" || address == "/checkme/rmssd")
            {
                hrvRmssd = ExtractFloat(data);
            }
            else if (address == "/checkme/hrv/sdnn" || address == "/checkme/sdnn")
            {
                hrvSdnn = ExtractFloat(data);
            }
            else if (address == "/checkme/r_wave")
            {
                if (ExtractInt(data) == 1)
                {
                    lock (lockObject)
                    {
                        rWavePending = true;
                    }
                }
            }
        }

        private string GetOscAddress(byte[] data)
        {
            int i = 0;
            while (i < data.Length && data[i] != 0) i++;
            return Encoding.ASCII.GetString(data, 0, i);
        }

        private int ExtractInt(byte[] data)
        {
            int offset = FindPayloadOffset(data);
            if (offset + 4 <= data.Length)
            {
                byte[] val = new byte[4];
                Array.Copy(data, offset, val, 0, 4);
                if (BitConverter.IsLittleEndian) Array.Reverse(val);
                return BitConverter.ToInt32(val, 0);
            }
            return 0;
        }

        private float ExtractFloat(byte[] data)
        {
            int offset = FindPayloadOffset(data);
            if (offset + 4 <= data.Length)
            {
                byte[] val = new byte[4];
                Array.Copy(data, offset, val, 0, 4);
                if (BitConverter.IsLittleEndian) Array.Reverse(val);
                return BitConverter.ToSingle(val, 0);
            }
            return 0f;
        }

        private int FindPayloadOffset(byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == ',')
                {
                    int tagEnd = i;
                    while (tagEnd < data.Length && data[tagEnd] != 0) tagEnd++;
                    return (tagEnd + 4) & ~3;
                }
            }
            return 0;
        }
    }
}

namespace GenericDataStreaming
{
    /// <summary>
    /// Generic OSC Ingestion Source alias for PulseEngine signal architecture.
    /// </summary>
    public class DataStreamOscSource : MedicalXR.Checkme.CheckmeOscReceiver
    {
    }
}
