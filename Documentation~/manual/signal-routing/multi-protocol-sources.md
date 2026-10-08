# Multi-Protocol Ingestion (OSC, WebSockets, Serial, Simulator)

PulseEngine supports multi-protocol ingestion natively. Through the abstract `IDataStreamSource` interface, any external hardware, network protocol, or local simulation can pump telemetry into the central `DataStreamRegistry`.

---

## 1. The `IDataStreamSource` Interface

All signal sources implement a clean, unified contract:

```csharp
namespace PulseEngine
{
    public interface IDataStreamSource
    {
        string SourceName { get; }
        bool IsConnected { get; }
        void Connect();
        void Disconnect();
        void UpdateSource(float deltaTime);
    }
}
```

---

## 2. Ingestion Protocols Overview

### 1. OSC / UDP Receiver (`CheckmeOscReceiver` / `DataStreamOscSource`)
* **Transport**: UDP broadcast / unicast (default port `7000`).
* **Format**: Standard Open Sound Control (OSC) packets.
* **Architecture**: Runs an asynchronous background listening thread. Inbound packets are decoded and parsed using exact string comparison (avoiding partial prefix collisions) and pushed into a thread-safe concurrent queue.
* **Best For**: TouchDesigner, Max/MSP, PureData, medical sensor bridges (Python BLE clients), iOS/Android sensor apps (ZIG SIM).

### 2. WebSocket Client (`DataStreamWebSocketSource`)
* **Transport**: TCP WebSocket (`ws://` or `wss://`).
* **Format**: JSON payloads or binary buffers:
  ```json
  {
    "channel": "accelerometer_y",
    "value": 9.81
  }
  ```
* **Architecture**: Asynchronous `ClientWebSocket` with automatic exponential reconnection backoff.
* **Best For**: Node.js backends, cloud telemetry, WebXR companions, browser-based control panels.

### 3. Serial / USB COM (`DataStreamSerialSource`)
* **Transport**: USB Virtual COM Port (`System.IO.Ports.SerialPort`).
* **Format**: ASCII newline-delimited key-value pairs (e.g., `EMG:412\n` or `PULSE:1.24\n`) or CSV strings.
* **Architecture**: Configurable baud rate ($9600$, $115200$, etc.), read timeouts, and thread buffering.
* **Best For**: Arduino, ESP32, Teensy, Raspberry Pi Pico, direct EMG/EEG/ECG microcontrollers.

### 4. Internal Simulation Engine (`DataStreamSimulatorSource`)
* **Transport**: None (in-engine procedural generation).
* **Features**:
  * **Physiological Modeling**: Generates synthetic Heart Rate ($60\text{--}140\text{ BPM}$), continuous ECG waveforms ($125\text{ Hz}$), blood oxygenation ($80\text{--}100\%$), peripheral perfusion ($0.5\text{--}8.0\%$), and autonomic HRV stress (RMSSD / SDNN).
  * **Clinical Scenarios**: Pre-configured clinical states (*Calma*, *Stress*, *Ipossia*) with smooth transitional interpolation ($\sim 2.5\text{ s}$).
  * **Generic Wave Generators**: Sine, Triangle, Sawtooth, Square, and Perlin Noise waves with adjustable frequency, amplitude, and phase offsets.
* **Best For**: Offline testing, rapid prototyping, unit testing, and standalone exhibition demos without connected sensor hardware.

---

## 3. Protocol Comparison & Property Reference

| Protocol Source | Threading Model | Typical Latency | Bandwidth Overhead | Reconnection |
| :--- | :--- | :---: | :---: | :---: |
| **OSC (UDP)** | Async Background Thread | $< 1\text{ ms}$ | Very Low (binary OSC) | Connectionless |
| **WebSockets (TCP)**| Async Tasks (`async/await`) | $2\text{--}10\text{ ms}$ | Low (JSON/Binary) | Auto-Retry Loop |
| **Serial / COM** | Polled Thread / Worker | $< 2\text{ ms}$ | Ultra Low (Raw ASCII) | Auto-Detect Port |
| **Internal Simulator**| Player Loop / `[ExecuteAlways]` | $0\text{ ms}$ | None (0 GC) | N/A |

---

## 4. Writing a Custom Source

Creating a custom hardware source (e.g., MQTT, Bluetooth LE, or Audio FFT) requires only a few lines of C#:

```csharp
using UnityEngine;
using PulseEngine;

public class CustomMicrophoneSource : MonoBehaviour, IDataStreamSource
{
    public string SourceName => "Microphone Audio FFT";
    public bool IsConnected { get; private set; }

    [SerializeField] private AudioSource audioSource;
    private float[] spectrumData = new float[64];

    public void Connect()
    {
        IsConnected = true;
    }

    public void Disconnect()
    {
        IsConnected = false;
    }

    public void UpdateSource(float deltaTime)
    {
        if (!IsConnected || audioSource == null) return;

        // Sample audio spectrum
        audioSource.GetSpectrumData(spectrumData, 0, FFTWindow.BlackmanHarris);

        float bassEnergy = spectrumData[0] + spectrumData[1];
        float midEnergy = spectrumData[8] + spectrumData[9];

        // Push into registry
        var registry = DataStreamRegistry.Instance;
        registry.PushRawValue("audio_bass", bassEnergy);
        registry.PushRawValue("audio_mid", midEnergy);
    }
}
```

---

## 5. Network Jitter & Packet Clustering

> [!WARNING]
> **Bluetooth & UDP Bursting**: Physical wireless sensors (such as BLE pulse oximeters or smartwatches) transmit telemetry in bursts or "packet clusters" rather than perfectly timed intervals. Feeding clustered pulses directly into graphics creates visual micro-stutters and artificial arrhythmias.  
> Always route bursty rhythmic triggers through the **`DataStreamMetronome`** modifier (see [Modifiers & Metronome](modifiers-and-metronome.md)).
