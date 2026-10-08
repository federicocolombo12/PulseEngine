# Installation and Setup

This page guides you through installing **PulseEngine** into your Unity project and validating the project settings.

---

## 1. Prerequisites

Before installing PulseEngine, verify that your project satisfies the following requirements:

1. **Unity Version**: Unity 6 (6000.0.0+) or higher.
2. **Universal Render Pipeline (URP)**: Active URP asset configured in **Project Settings > Graphics**.
3. **Visual Effect Graph**: The `com.unity.visualeffectgraph` package must be installed in your project.
4. **Input System**: Both the New Input System and Mathematics packages must be enabled.

---

## 2. Installing the Package

### Method A: Via Git URL (Recommended for Private / Team Repositories)
1. Open your Unity Project.
2. Navigate to **Window > Package Manager**.
3. Click the **[+]** icon in the upper-left corner and select **Add package from git URL...**.
4. Enter the repository URL:
   ```text
   https://github.com/federicocolombo12/PulseEngine.git
   ```
5. Click **Add**. Unity will automatically fetch and compile the package.

### Method B: From Local Disk (Development Workflow)
1. In the **Package Manager**, click the **[+]** icon and select **Add package from disk...**.
2. Browse to the directory where you cloned or extracted PulseEngine:
   ```text
   C:/Users/.../PulseEngine/package.json
   ```
3. Select `package.json` and click **Open**.

---

## 3. Project Settings Configuration

### Graphics & Shader Stripping
To ensure custom compute shaders and raymarching variants execute without runtime stripping:
1. Go to **Project Settings > Graphics**.
2. Under **Shader Loading**, verify that compute shaders are supported on your target platform.
3. For standalone **Meta Quest 3** deployment, ensure your URP pipeline asset has **Intermediate Texture** set to *Auto* or *Always* if using screen-space effects.

### Enabling Samples
PulseEngine ships with ready-to-run demo environments:
1. Open **Window > Package Manager**.
2. Select **In Project** and click **PulseEngine: Signal Routing & Volumetric VFX**.
3. Expand the **Samples** section on the right-hand panel.
4. Click **Import** next to:
   * **01 Signal Routing & DAW Demo**: Demonstrates signal routing, DAW Mixer controls, and dynamic binders.
   * **02 Clinical Biofeedback Suite**: Full medical XR suite featuring simulated vitals, dynamic heart/lung metaphors, and Quest 3 profiling tools.
