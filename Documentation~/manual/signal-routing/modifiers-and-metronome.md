# Modifiers & Metronome (Signal Conditioning)

Signal Modifiers in PulseEngine act like **Insert FX** on an audio mixing desk. They sit between raw channel ingestion and visual output binders, performing mathematical conditioning, temporal decay, and anti-jitter phase stabilization.

---

## 1. Network Jitter Eliminator: `DataStreamMetronome`

### The Problem: Burst Jitter in Wireless Sensors
Bluetooth Low Energy (BLE) and Wi-Fi UDP streams frequently transmit telemetry packets in bursts. If a medical sensor transmits an average of $60\text{ BPM}$ ($1.0\text{ Hz}$), packets often arrive in pairs with variable millisecond delays ($[0.05\text{ s}, 1.95\text{ s}]$). Mapping visual heartbeats directly to raw packet arrival timestamps induces noticeable graphic shudder and jarring visual arrhythmias.

### The Solution: Continuous Phase Integration
`DataStreamMetronome` reads continuous physical rates (e.g., `heart_rate` in BPM) from `DataStreamRegistry` and integrates mathematical phase continuously:

$$\Delta \phi = \frac{\text{BPM}}{60} \cdot \Delta t$$
$$\phi_t = (\phi_{t-1} + \Delta \phi) \pmod{1.0}$$

When the phase $\phi_t$ completes a cycle ($0.0 \to 1.0$), the metronome fires a clean, mathematically perfect pulse trigger (`pulseTriggered`), completely decoupling visual pacing from network delivery lag.

```mermaid
graph LR
    A[Bluetooth / OSC Stream] -- "Jittery Bursts" --> B[DataStreamRegistry: BPM Value]
    B --> C[DataStreamMetronome: Phase Integration]
    C -- "Flawless Rhythmic Trigger" --> D[VisualEffect / Shader / Audio]
```

### Double-Precision Time Integration
> [!NOTE]
> In long-running Unity Editor sessions, standard single-precision floats (`float`) experience **Catastrophic Cancellation** when calculating $\Delta t$ from large system uptime values (`EditorApplication.timeSinceStartup`). `DataStreamMetronome` uses double-precision accumulation (`double`) internally to ensure zero phase drift over days of continuous operation.

---

## 2. Exponential Decay Generator: `DataStreamDecayTrigger`

Discrete triggers (such as a heartbeat, footstep, or MIDI note-on) often need to drive continuous physical motions (such as scale bounce, emission flashes, or particle velocities).

`DataStreamDecayTrigger` listens for a trigger and instantly ramps its output to $1.0$, then decays exponentially toward zero:

$$V_t = V_{t-1} \cdot e^{-\lambda \Delta t}$$
where $\lambda$ is the decay speed constant.

### Inspector Properties

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| **`listenTriggerId`** | `string` | `"heartbeat"` | Identifier of the discrete trigger event in the registry. |
| **`outputChannelId`** | `string` | `"heart_pulse"`| Output continuous channel provisioned in the registry. |
| **`decaySpeed`** | `float` | `4.0` | Rate of decay. Higher values produce sharp, snappier bounces. |
| **`attackTime`** | `float` | `0.0` | Optional rise time before decay begins. |

---

## 3. Mathematical Conditioning: `DataStreamMathModifier`

`DataStreamMathModifier` transforms continuous channels into derived secondary signals without writing custom C# scripts.

### Operation Modes
* **Threshold**: Emits $1.0$ when input exceeds a cutoff, $0.0$ otherwise (e.g., `is_tachycardic` when $\text{BPM} > 120$).
* **Invert**: Flips normalized signals ($1.0 - x$), ideal for mapping low parasympathetic HRV into high visual turbulence.
* **Multiply / Gain**: Amplifies or scales the range.
* **Remap & Clamp**: Remaps arbitrary input intervals $[A, B]$ to $[C, D]$.

```mermaid
graph LR
    Raw[Channel: spo2] --> Mod[MathModifier: Threshold < 90%]
    Mod --> Derived[New Channel: is_hypoxic (0 or 1)]
    Derived --> Binders[Tint Particles Cyanotic Blue]
```
