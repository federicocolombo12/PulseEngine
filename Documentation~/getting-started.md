# Getting Started with PulseEngine

This tutorial walks you through setting up your first real-time signal routing pipeline and binding it to procedural volumetric geometry in under 5 minutes.

---

## Step 1: Open the DAW Mixer Console

1. In the Unity Editor menu, select **Window > PulseEngine > DAW Mixer**.
2. Dock the console alongside your **Scene** or **Game** view.
3. The DAW Mixer will display the top toolbar with:
   * **Source Selector**: Choose between `Simulator`, `OSC / UDP`, `WebSocket`, or `Serial COM`.
   * **Inbound Signal Sniffer**: Real-time traffic monitor with activity LEDs.
   * **Master Preset Bar**: Preset switcher, 1-click snapshot save, and overwrite options.

```text
+---------------------------------------------------------------------------------------+
|  [PULSE ENGINE]  ● IN SCENA: PRESET [Default]  [◀] [▶]  [+ Nuovo]  [💾 Salva]         |
+---------------------------------------------------------------------------------------+
|  [⚡ SIMULATORE] [📡 OSC] [🌐 WEBSOCKET] [🔌 SERIAL]   |   [⚡ BENCHMARK] [❓ GUIDA]    |
+---------------------------------------------------------------------------------------+
```

---

## Step 2: Provision a Signal Channel

1. In the DAW Mixer top bar, click **⚡ SIMULATORE** to activate the internal signal generator.
2. Under the **Inbound Signal Sniffer** foldout, notice the detected channels:
   * `heart_rate` (BPM continuous wave)
   * `spo2` (blood oxygenation percentage)
   * `hrv_rmssd` (autonomic stress indicator)
3. Click the green **[+ Auto-Crea Canali Rilevati]** button.
4. Vertical channel strips will appear in the mixer console, complete with:
   * Real-time VU meter and oscilloscope.
   * Input range remap sliders $[Min, Max] \to [0, 1]$.
   * Interactive volume/amplitude fader.
   * Hardware **[M]** (Mute) and **[S]** (Solo) buttons.

---

## Step 3: Wire a Signal to Scene Objects (Live Patching)

Now let's bind the simulated heart rate to modulate a scene property in real time:

1. Create a 3D Sphere, Light, or Particle System in your scene (or select any existing GameObject).
2. Return to the **DAW Mixer** window.
3. Locate the channel strip for `heart_rate`.
4. In the **Output Binders** section at the bottom of the strip, click **[+ Attach Property Binder]**.
5. Select your target GameObject in the slot:
   * **Target Mode**: Choose `TransformScale`, `MaterialFloat`, or `LightIntensity`.
   * **Property / Axis**: Choose the target property to drive (e.g. `Uniform Scale` or `Intensity`).
6. Enable **Use Local Remap**:
   * Set Min Output to `1.0` (resting scale).
   * Set Max Output to `1.25` (peak systolic contraction).
7. Observe live in **Edit Mode** (via `[ExecuteAlways]`) or enter **Play Mode**:
   * The object will pulse in perfect rhythm with the simulated cardiac wave!

---

## Step 4: Companion Integration with VolumetricSDF (Optional)

If you have also imported our companion package **[VolumetricSDF](https://github.com/federicocolombo12/VolumetricSDF)** (`com.federicocolombo.volumetricsdf`):

1. Right-click in the Hierarchy and select **GameObject > 3D Object > VolumetricSDF > SDF Volume**.
2. In the `heart_rate` channel strip on the DAW Mixer, add a **Property Binder** targeting the `SdfGroupController` component.
3. Select `globalBlendSoftness`:
   * Set Min Output to `0.05` (sharp, spiky Kiki state).
   * Set Max Output to `0.45` (soft, melting Bouba state).
4. As the simulated heart pumps, the volumetric shape pulses smoothly, dynamically breathing between sharp cuts and organic fusions!

---

## Step 5: Next Steps

Congratulations! You have established a complete real-time telemetry routing pipeline.

Explore the in-depth manuals:
* [Architecture & Data Pipeline](manual/signal-routing/architecture.md): Deep dive into the zero-GC ingestion bus and thread-safe ringbuffer.
* [DAW Mixer Console Manual](manual/signal-routing/daw-mixer.md): Learn advanced features including Insert FX, virtual channels, and Mute/Solo logic.
* [Zero-GC Telemetry & Ingestion Profiling](manual/benchmarks/zero-gc-telemetry-profiling.md): Profiling measurements verifying zero heap allocations at 120 Hz.
* [Driving VolumetricSDF with PulseEngine](manual/integrations/volumetric-sdf-binding.md): End-to-end guide to driving procedural CSG and VFX Graph particles.
* [VolumetricSDF Technical Documentation ↗](https://federicocolombo12.github.io/VolumetricSDF/): Dedicated manual for the companion implicit CSG & GPU voxelizer engine.
