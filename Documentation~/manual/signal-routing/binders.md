# Property & Color Binders (Output Mapping)

Binders in PulseEngine connect normalized signals ($[0, 1]$) from `DataStreamRegistry` to any visual, acoustic, or physical property in your Unity scene. They utilize C# **Reflection** to detect target fields automatically and support local remap ranges and resting value restoration.

---

## 1. Generic Property Binder: `DataStreamPropertyBinder`

`DataStreamPropertyBinder` is the primary component for driving numerical properties.

### Supported Binding Targets

| Target Mode | Description | Example Use Case |
| :--- | :--- | :--- |
| **`VisualEffectProperty`** | Exposes exposed Blackboard float properties in Unity VFX Graph. | Driving turbulence intensity, particle spawn rate, attraction speed. |
| **`MaterialFloat`** | Sets float uniforms on a `Renderer.material` or `sharedMaterial`. | Modulating shader blend softness, distortion frequency, rim power. |
| **`TransformScale`** | Dynamically scales a `Transform` on X, Y, Z, or uniformly. | Making a 3D anatomical heart or organism pulse with physical volume. |
| **`LightIntensity`** | Controls the `Light.intensity` of a scene light source. | Creating rhythmic light strobes or alarm illumination. |
| **`ComponentField`** | Dynamically modifies any public `float` field or property via Reflection. | Driving custom parameters in third-party scripts. |

---

## 2. Inspector Reference Table

### `DataStreamPropertyBinder` Properties

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| **`channelId`** | `string` | `""` | The registry channel this binder listens to (selectable via dropdown). |
| **`targetMode`** | `Enum` | `VisualEffect`| Destination component category. |
| **`targetObject`** | `GameObject`| `null` | The scene GameObject hosting the destination component. |
| **`propertyName`** | `string` | `""` | The specific property name on the target component (auto-populated dropdown). |
| **`useLocalRemap`** | `bool` | `true` | If true, converts normalized $[0, 1]$ into a custom $[Min, Max]$ target interval. |
| **`minOutputValue`**| `float` | `0.0` | Output value when incoming normalized signal is $0.0$. |
| **`maxOutputValue`**| `float` | `1.0` | Output value when incoming normalized signal is $1.0$. |
| **`restValue`** | `float` | `0.0` | Fallback value restored when the channel is muted (`[M]`) in the DAW Mixer. |

---

## 3. Dynamic Color Evaluator: `DataStreamColorBinder`

`DataStreamColorBinder` evaluates a dynamic **Unity Gradient** in real time driven by a continuous float signal.

### Workflow Example: Hypoxic Visual Feedback
1. Attach `DataStreamColorBinder` to a GameObject with a `VisualEffect` or `Renderer`.
2. Set **Channel ID** to `"spo2"`.
3. In the Inspector Gradient editor:
   * Left ($0.0$, critically low oxygen): Cyanotic dark blue (`#1A2A6C`).
   * Middle ($0.5$, moderate hypoxemia): Desaturated purple (`#8E2DE2`).
   * Right ($1.0$, healthy oxygenation): Bright arterial crimson red (`#E52D27`).
4. Set Target Property to `ParticleColor`.
5. As blood oxygenation falls, the particle swarm automatically shifts hue across the gradient.

---

## 4. Discrete Event Binder: `DataStreamTriggerBinder`

`DataStreamTriggerBinder` responds to instantaneous triggers (e.g., `pulseTriggered`, `threshold_exceeded`) by invoking standard `UnityEvent` listeners.

### Capabilities
* Triggering a particle burst via `VisualEffect.SendEvent("OnPlay")`.
* Playing an audio one-shot on an `AudioSource`.
* Activating haptic feedback patterns on VR controllers (Meta Quest Touch).

---

## 5. Rest Value Restoration

> [!TIP]
> **Why Rest Values Matter**: When an audio engineer mutes a channel on a physical mixing console, instruments do not freeze indefinitely at arbitrary amplitudes—they go silent.  
> Similarly, when a channel in PulseEngine is muted (`isMuted = true`), all connected binders execute `RestoreOriginalValue()`, smoothly interpolating parameters back to neutral resting states so your scene never remains stuck in a contorted or extreme state.
