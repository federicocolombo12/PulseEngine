# Standalone Modifier Volumes (Spatial Domain Warping)

Rather than attaching mathematical distortions (twists, bends, noise) to individual nodes, PulseEngine adopts a **Standalone Modifier Volume** architecture (inspired by volume modifiers in MudBun and Houdini). 

Modifier volumes exist as independent GameObjects in 3D space with dedicated position, rotation, and bounding bounds. They act like **localized spatial force fields**, warping any geometric primitives that intersect their volume.

---

## 1. Why Standalone Volumes?

* **Spatial Locality**: You can place a twist modifier strictly across the aortic arch of a heart or the waist of a character without distorting the rest of the body.
* **Decoupled Animation**: Modifiers can be translated, rotated, and modulated dynamically (e.g., connected to telemetry via `DataStreamPropertyBinder`) without modifying the underlying geometric node hierarchy.
* **Falloff Margins**: Every volume features a smooth outer falloff margin to prevent abrupt geometric clipping or discontinuous topological tearing.

---

## 2. Supported Deformation Operations

```mermaid
graph TD
    P[Sampling Point p] --> ModCheck{Inside Modifier Volume?}
    ModCheck -- Yes --> Falloff[Compute Falloff Weight w based on distance to core box]
    Falloff --> Deform[Apply Inverse Space Warp p' = InverseTransform(p)]
    Deform --> Eval[Evaluate CSG Primitives at p']
    ModCheck -- No --> EvalDirect[Evaluate CSG Primitives at p]
```

### 1. Twist (Torsional Contortion)
Twists 3D space along the local Y-axis proportional to height and angle:
$$\theta = y \cdot \text{twistAngle} \cdot w$$
$$p_x' = p_x \cos(\theta) - p_z \sin(\theta)$$
$$p_z' = p_x \sin(\theta) + p_z \cos(\theta)$$
where $w \in [0, 1]$ is the localized falloff weight.

### 2. Bend (Curvature Deformation)
Bends space along a cylindrical curvature radius $R$:
$$\theta = p_x \cdot \text{bendCurvature} \cdot w$$
$$p_x' = R \sin(\theta)$$
$$p_y' = p_y - R (1 - \cos(\theta))$$

### 3. Bounded 3D Noise (Cellular & Organic Turbulence)
Displaces surface boundaries using high-frequency 3D procedural noise confined strictly to the volume:
$$\mathbf{p}' = \mathbf{p} + \nabla \text{Noise}(\mathbf{p} \cdot \text{frequency}) \cdot \text{amplitude} \cdot w$$

---

## 3. Double Wireframe Scene Gizmo

In the Unity Scene View, `SdfModifierVolume` renders an interactive dual gizmo:
* **Solid Yellow Box**: The **Core Zone** ($w = 1.0$), where $100\%$ of the deformation force applies.
* **Dotted Outer Cyan Margin**: The **Falloff Zone** ($0.0 < w < 1.0$), where deformation diminishes smoothly toward zero, ensuring $C^1$ continuity with unwarped space.

---

## 4. Inspector Reference: `SdfModifierVolume`

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| **`operation`** | `Enum` | `Twist` | Deformation algorithm: `Twist`, `Bend`, `Noise3D`. |
| **`volumeBounds`**| `Vector3`| `(1.0, 1.0, 1.0)`| Dimensions of the core distortion region in local units. |
| **`falloffRadius`**| `float` | `0.2` | Soft transition boundary outside the core box in meters. |
| **`strength`** | `float` | `1.0` | Primary deformation amplitude (degrees for twist, displacement for noise). |
| **`frequency`** | `float` | `2.5` | Spatial frequency for `Noise3D` turbulence. |
| **`targetNodes`** | `Enum` | `AllAbove` | Target scope: Affects all nodes higher in the hierarchy or specific tags. |

---

## 5. Performance Note

> [!TIP]
> **GPU Backward-Warp Execution**: In the raymarching shader and compute voxelizer, modifiers execute as a fast inverse-coordinate warp before distance evaluations occur. Because the ray step distance remains conservative within the falloff zone, sphere tracing does not overstep or cause surface artifacts.
