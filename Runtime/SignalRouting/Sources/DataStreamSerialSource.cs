using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

#if ENABLE_SERIAL_PORTS
using System.IO.Ports;
#endif

namespace GenericDataStreaming
{
    /// <summary>
    /// Ricevitore da Porta Seriale / USB COM per collegare microcontrollori
    /// (Arduino, ESP32, Raspberry Pi Pico) e sensori medici seriali.
    /// Per abilitare su .NET Standard 2.1, definire 'ENABLE_SERIAL_PORTS' nei Scripting Define Symbols.
    /// </summary>
    [AddComponentMenu("Generic Data Streaming/Sources/Serial Port Source")]
    [ExecuteAlways]
    public class DataStreamSerialSource : MonoBehaviour, IDataStreamSource
    {
        public string ProtocolName => "Serial / COM Port";
        public bool IsConnected => isConnected;

        [Header("Serial Configuration")]
        [Tooltip("Nome della porta seriale (es. COM3 su Windows, o /dev/ttyUSB0 su Linux/Mac).")]
        public string portName = "COM3";

        [Tooltip("Baud Rate della comunicazione (standard: 9600 o 115200).")]
        public int baudRate = 115200;

        [Tooltip("Connetti automaticamente all'attivazione del componente.")]
        public bool autoConnect = false;

        private bool isConnected = false;
        private Thread readThread;
        private bool isRunning = false;
        private ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

#if ENABLE_SERIAL_PORTS
        private SerialPort serialPort;
#endif

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
#if ENABLE_SERIAL_PORTS
            if (isConnected) return;

            try
            {
                serialPort = new SerialPort(portName, baudRate);
                serialPort.ReadTimeout = 500;
                serialPort.WriteTimeout = 500;
                serialPort.Open();
                isConnected = true;
                isRunning = true;

                readThread = new Thread(ReadLoop);
                readThread.IsBackground = true;
                readThread.Start();

                Debug.Log($"[DataStreamSerialSource] Connesso alla porta {portName} a {baudRate} baud.");
            }
            catch (Exception ex)
            {
                isConnected = false;
                isRunning = false;
                Debug.LogWarning($"[DataStreamSerialSource] Impossibile aprire {portName}: {ex.Message}");
            }
#else
            Debug.LogWarning("[DataStreamSerialSource] La Porta Seriale (.NET Standard) richiede 'ENABLE_SERIAL_PORTS' in Player Settings -> Scripting Define Symbols. Per streaming immediato senza configurazioni, usa OSC o WebSocket.");
#endif
        }

        public void StopStreaming()
        {
            isRunning = false;
            isConnected = false;

            if (readThread != null && readThread.IsAlive)
            {
                readThread.Join(200);
                readThread = null;
            }

#if ENABLE_SERIAL_PORTS
            if (serialPort != null)
            {
                try
                {
                    if (serialPort.IsOpen) serialPort.Close();
                }
                catch { }
                serialPort.Dispose();
                serialPort = null;
            }
#endif
            Debug.Log("[DataStreamSerialSource] Porta seriale chiusa.");
        }

        private void ReadLoop()
        {
#if ENABLE_SERIAL_PORTS
            while (isRunning && serialPort != null && serialPort.IsOpen)
            {
                try
                {
                    string line = serialPort.ReadLine();
                    if (!string.IsNullOrEmpty(line))
                    {
                        ParseAndEnqueue(line);
                    }
                }
                catch (TimeoutException) { }
                catch (Exception ex)
                {
                    if (isRunning)
                    {
                        Debug.LogWarning($"[DataStreamSerialSource] Errore lettura seriale: {ex.Message}");
                    }
                    break;
                }
            }
#endif
        }

        private void ParseAndEnqueue(string line)
        {
            mainThreadActions.Enqueue(() =>
            {
                if (DataStreamRegistry.Instance == null) return;

                string[] pairs = line.Trim().Split(new[] { ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var pair in pairs)
                {
                    string[] parts = pair.Split(new[] { ':', '=' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        string channel = parts[0].Trim().ToLowerInvariant();
                        if (channel == "hr") channel = "heart_rate";
                        if (channel == "pi") channel = "perfusion_index";

                        if (float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float val))
                        {
                            DataStreamRegistry.Instance.PushValue(channel, val);
                        }
                    }
                }
            });
        }

        void Update()
        {
            while (mainThreadActions.TryDequeue(out var action))
            {
                action?.Invoke();
            }
        }
    }
}
