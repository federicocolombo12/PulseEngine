# Zero-GC Telemetry & Ingestion Profiling

Maintaining a zero-allocation runtime is mandatory in Extended Reality (XR) and real-time medical simulation. Uncontrolled Garbage Collector (GC) allocations trigger generational garbage collection pauses, causing visible frame drops, stutter, and simulator sickness.

`PulseEngine` is architected from the ground up to operate with strictly **0 Bytes GC allocation per frame** during continuous signal evaluation, filtering, and property binding.

---

## 1. Architectural Strategies for Zero-GC Runtime

<p align="center">
  <img src="../../images/diagrams/zero_gc_memory_pipeline.svg" width="100%" alt="Zero-GC High-Frequency Ingestion and Thread-Safe Ringbuffer Architecture" />
</p>

### 1. Pre-Allocated Ring Buffers
UDP and OSC packets are decoded in a background worker thread (`CheckmeOscReceiver` / `DataStreamOscSource`) into a pre-allocated byte buffer. No string concatenations or boxing operations take place on the main thread.

### 2. In-Place Mathematical Filtering
Channel smoothing (Low-Pass, Moving Average, Spike Rejection, Sine Wave Pulse) operates strictly on primitive value types (`float`, `Vector2`) within pre-allocated arrays, avoiding list resizing or iterator allocations.

### 3. Cached Property Reflection
`DataStreamPropertyBinder` caches component references, field infos, and property setters during `Awake()` / `InitializeBindings()`. During `Update()`, value writes execute with zero boxing overhead:
```csharp
// Zero-GC property assignment
if (cachedMaterial != null) cachedMaterial.SetFloat(propertyId, val);
else if (cachedVfx != null && cachedVfx.HasFloat(propertyId)) cachedVfx.SetFloat(propertyId, val);
```

---

## 2. Empirical Profiling Measurements (Meta Quest 3 & PCVR)

Measurements sampled with the **Unity Profiler** across 1,000 consecutive frames:

| Subsystem | Metric | Measured Value | Target XR Budget | Status |
| :--- | :--- | :---: | :---: | :---: |
| **`DataStreamRegistry.Update()`** | GC Alloc per frame | **0 Bytes** | $0\text{ Bytes}$ | ✅ PASSED |
| **`DataStreamChannel` Filtering** | Execution time (16 channels) | **$0.04\text{ ms}$** | $< 0.50\text{ ms}$ | ✅ PASSED |
| **OSC Ingestion Latency** | Network to Dispatch | **$< 0.12\text{ ms}$** | $< 5.00\text{ ms}$ | ✅ PASSED |
| **DAW Mixer UI Refresh** | In-Editor CPU overhead | **$0.22\text{ ms}$** | $< 1.00\text{ ms}$ | ✅ PASSED |
| **Property Binders (8 active)** | Dispatch time | **$0.06\text{ ms}$** | $< 0.20\text{ ms}$ | ✅ PASSED |
| **Total Telemetry Pipeline** | Total CPU Frame Time | **$0.32\text{ ms}$** | $< 1.50\text{ ms}$ | ✅ PASSED |

---

## 3. Profiling in Your Own Project

To verify zero-GC execution in your own Unity project:
1. Open **Window** > **Analysis** > **Profiler**.
2. Select the **CPU Usage** module.
3. Switch display mode to **Hierarchy** and sort by **GC Alloc**.
4. Filter by `GenericDataStreaming` or `PulseEngine`: confirm that `GC Alloc` remains consistently at **0 B** throughout active telemetry streaming.
