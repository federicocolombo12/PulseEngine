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

## Step 3: Spawn a Volumetric SDF Hierarchy

1. In the Unity Hierarchy window, right-click and select **GameObject > 3D Object > PulseEngine > SDF Group Controller**.
2. A new GameObject with the `SdfGroupController` component and a solid proxy mesh is instantiated.
3. In the Inspector of `SDF Group Controller`, click **[+ Aggiungi Nodo Sfera]**.
4. A child GameObject with an `SdfNode` component is created. Select it and move it in the Scene view using standard transform gizmos.
5. Click **[+ Aggiungi Nodo Scatola]** and move the box close to the sphere:
   * In the `SdfNode` Inspector of the second shape, set **Combine Operation** to `SmoothUnion`.
   * Adjust **Blend Softness** to `0.25`.
   * Watch the two shapes melt seamlessly together in real time directly inside the **Scene View**!

---

## Step 4: Wire a Signal to Volumetric Geometry (Live Patching)

Now let's bind the simulated heart rate to deform the geometry:

1. Return to the **DAW Mixer** window.
2. Locate the channel strip for `heart_rate`.
3. In the **Output Binders** section at the bottom of the strip, click **[+ Attach Property Binder]**.
4. Select your **SDF Group Controller** GameObject as the Target.
5. In the target dropdown, choose `globalBlendSoftness`.
6. Enable **Use Local Remap**:
   * Set Min Output to `0.05` (tense, sharp geometry).
   * Set Max Output to `0.40` (relaxed, flowing organic geometry).
7. Enter **Play Mode** (or observe live in **Edit Mode** via `[ExecuteAlways]`):
   * As the simulated heart pumps, the volumetric shape pulses smoothly, dynamically breathing between sharp cuts and organic fusions!

---

## Step 5: Next Steps

Congratulations! You have established a complete real-time signal-to-geometry pipeline.

Explore the in-depth manuals:
* [DAW Mixer Console Manual](manual/signal-routing/daw-mixer.md): Learn advanced features including Insert FX, virtual channels, and Mute/Solo logic.
* [GPU Voxelizer & VFX Graph Pipeline](manual/volumetric-sdf/gpu-voxelizer-vfx.md): Connect your SDF volume directly into particle collisions.
* [Quest 3 Benchmarking](manual/benchmarks/quest3-profiling.md): Measure and profile your scene for mobile virtual reality.
