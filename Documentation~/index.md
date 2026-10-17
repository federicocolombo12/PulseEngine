# About PulseEngine

Welcome to the **PulseEngine** documentation.

**PulseEngine** is a high-performance Unity package designed for real-time signal routing, virtual DAW console mixing, and procedural volumetric rendering. It couples multi-protocol continuous telemetry (biometric sensors, audio streams, IoT, motion tracking, network sockets) directly to procedural geometry (**Signed Distance Fields - SDFs**) and massive GPU particle swarms (**Unity VFX Graph**).

Originally engineered for clinical trauma biofeedback in Extended Reality (XR) at the **Politecnico di Torino**, PulseEngine is built from the ground up to be **domain-agnostic**, making it equally powerful for medical simulation, audio-reactive VJ stages, interactive installations, and game development.

<p align="center">
  <a href="images/demo_raw.mp4">
    <img src="images/demo_overview_preview.gif" width="100%" alt="PulseEngine Live In-Editor Demo Walkthrough" />
  </a>
  <br>
  <em>🎬 <b>PulseEngine In-Engine Walkthrough</b>: Virtual DAW Mixer console, telemetry routing, and real-time GPU particle swarm modulation.</em>
</p>

---

## Core Architectural Pillars

<p align="center">
  <img src="images/diagrams/pulseengine_architecture.svg" width="100%" alt="PulseEngine System Pipeline and Signal Matrix Architecture" />
</p>

### 1. High-Performance Virtual DAW Mixer
* **Hardware-Style Console (`DAWMixerWindow`)**: Direct visual mixing with vertical channel strips, interactive faders, hardware OLED oscilloscopes, VU meters, and engine-level Mute/Solo.
* **Zero-GC Runtime Dispatcher**: Thread-safe, non-allocating signal bus (`DataStreamRegistry`) running at full target framerate (72/90/120 Hz).
* **Automatic Signal Discovery**: Inbound signal sniffer that inspects incoming telemetry, detects unknown packets, and provisions calibrated channels automatically.
* **Network Anti-Jitter Metronome**: Converts clustered, irregular network data packets into smooth rhythmic triggers with continuous mathematical phase integration.

### 2. Universal Parameter & Visual Binders
* **Non-Intrusive Component Modulation**: `DataStreamPropertyBinder` and `DataStreamColorBinder` attach to any GameObject and modulate target properties with custom local remap ranges and weight multipliers.
* **Multi-Domain Targeting**: Directly controls Materials & Custom Shaders (Emission, Albedo), Transforms (Position, Rotation, Scale), Lights, Audio cutoffs, and VFX Graph blackboard properties with zero boilerplate code.
* **Modular Companion Integration**: Pairs seamlessly with the **[VolumetricSDF](https://github.com/federicocolombo12/VolumetricSDF)** companion package to drive real-time procedural CSG shapes, bounded modifier volumes, and GPU particle swarms.

---

## Technical Specifications & Requirements

| Specification | Supported / Verified Requirement |
| :--- | :--- |
| **Unity Version** | Unity 6 (6000.0+) LTS recommended |
| **Render Pipeline** | Universal Render Pipeline (URP) 17.0+ |
| **VFX Engine** | Unity Visual Effect Graph (VFX Graph) 17.0+ |
| **Input System** | Unity New Input System (`UnityEngine.InputSystem`) |
| **Tested Platforms** | Meta Quest 3 (Standalone Android OpenXR), PCVR, Windows Standalone |
| **Target Framerate** | 72 Hz / 90 Hz / 120 Hz with 0 Bytes runtime GC allocation |

---

## Document Navigation Guide

* [Installation & Setup](installation-and-setup.md): Package installation steps, project validation, and URP configuration.
* [Getting Started](getting-started.md): 5-minute quickstart from a raw wave generator to dynamic procedural geometry.
* **Signal Routing Manual**:
  * [Architecture & Data Pipeline](manual/signal-routing/architecture.md)
  * [DAW Mixer Console Manual](manual/signal-routing/daw-mixer.md)
  * [Multi-Protocol Ingestion](manual/signal-routing/multi-protocol-sources.md)
  * [Modifiers & Metronome](manual/signal-routing/modifiers-and-metronome.md)
  * [Property & Color Binders](manual/signal-routing/binders.md)
* **Volumetric SDF Engine Manual**:
  * [Primitives & CSG Operations](manual/volumetric-sdf/primitives-and-csg.md)
  * [Modifier Volumes](manual/volumetric-sdf/modifier-volumes.md)
  * [GPU Voxelizer & VFX Graph Pipeline](manual/volumetric-sdf/gpu-voxelizer-vfx.md)
  * [Triplanar Texturing & MatCaps](manual/volumetric-sdf/surface-texturing.md)
* **Clinical Biofeedback Suite**:
  * [Medical XR Bio-Feedback Suite](manual/clinical-biofeedback/biofeedback-suite.md)
* **Performance & Optimization**:
  * [Quest 3 Benchmarking & Profiling](manual/benchmarks/quest3-profiling.md)
