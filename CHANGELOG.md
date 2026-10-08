# Changelog

All notable changes to **PulseEngine** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-10-08

### Added
- **Signal Routing Architecture**:
  - `DataStreamRegistry`: Thread-safe, priority-based central signal dispatcher with zero GC allocations in steady state.
  - `DataStreamChannel`: Automatic linear range mapping $[Min, Max] \to [0, 1]$, customizable exponential smoothing, and clamping.
  - Multi-Protocol Ingestion: Native async clients for **OSC (UDP)**, **WebSockets**, **Serial / COM USB**, and an **Internal Simulator**.
  - Smart Inbound Traffic Sniffer & Auto-Discovery engine to detect and create channels on the fly.
  - Modifiers: `DataStreamMetronome` (network jitter eliminator for rhythm/pulse), `DataStreamDecayTrigger`, and `DataStreamMathModifier`.
  - Generic Reflection Binders: `DataStreamPropertyBinder`, `DataStreamColorBinder`, and `DataStreamTriggerBinder` to modulate any Material, Shader, VFX Graph, Transform, or Unity Event.

- **DAW Mixer Console (`DAWMixerWindow`)**:
  - Hardware-style audio console interface with vertical channel strips, interactive volume faders, real-time OLED-style oscilloscopes, and VU meters.
  - Real Mute `[M]` and Solo `[S]` engine-level silencing with automatic restful value restoration.
  - Live virtual patching, channel creation wizard, and 1-click preset snapshot management.

- **Volumetric SDF Engine**:
  - `BioSignalSDF.shader`: High-performance URP Raymarching shader supporting Spheres, Boxes, Tori, Cylinders, and Capsules.
  - Order-independent CSG operations: *Smooth Union*, *Smooth Subtraction*, *Smooth Intersection*, and sharp Booleans.
  - Standalone `SdfModifierVolume`: Bounded domain warp volumes (*Twist*, *Bend*, *3D Noise*) with smooth distance falloff.
  - Implicit Surface Shading: Triplanar projection in object space, Morten Mikkelsen Surface Gradient normal perturbation, and View-Space MatCap support.

- **GPU VFX Graph Voxelizer**:
  - `SdfVolumeVoxelizer.compute`: Computes a 3D signed distance field and color volume into a `RGBAHalf` `RenderTexture3D` in real-time.
  - Direct integration into Unity VFX Graph for real-time particle adhesion, collision, and advection.
  - *Physics-Only Mode* (`renderSolidMesh = false`): Decouples solid raymarching from particle physics to eliminate pixel fill-rate bottlenecks on standalone mobile VR (tested on Meta Quest 3).

- **Benchmarking & Profiling**:
  - `MedicalXRBenchmarkRunner`: Automated 13-stage scientific profiling suite sampling FPS, 1% lows, frame times, and VRAM bandwidth.
  - `MedicalXRPerformanceHUD`: In-game and VR-compatible performance overlay.
