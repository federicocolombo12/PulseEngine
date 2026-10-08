using UnityEngine;
using GenericDataStreaming;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MedicalXR.RayMarching
{
    /// <summary>
    /// HUD compatto per il monitoraggio in tempo reale delle prestazioni in XR / Editor.
    /// Mostra FPS, Frame Time, Voxelizer Resolution, VRAM occupata e stato del Pixel-Fill Decoupling.
    /// Utilizzabile direttamente sia in Play Mode che su Standalone (Meta Quest 3).
    /// </summary>
    [ExecuteAlways]
    public class MedicalXRPerformanceHUD : MonoBehaviour
    {
        [Header("Display Settings")]
        public bool showHUD = true;
        public KeyCode toggleKey = KeyCode.F1;
        public KeyCode toggleSolidMeshKey = KeyCode.F2;
        public KeyCode cycleResolutionKey = KeyCode.F3;
        public KeyCode benchmarkKey = KeyCode.F4;

        [Header("Smoothing")]
        [Range(0.01f, 0.5f)]
        public float fpsUpdateInterval = 0.2f;

        private float accumulatedDeltaTime = 0f;
        private int accumulatedFrames = 0;
        private float currentFps = 0f;
        private float currentFrameTimeMs = 0f;
        private float lastFpsUpdateTime = 0f;

        private SdfVolumeVoxelizer voxelizer;
        private SdfGroupController controller;
        private DataStreamSimulatorSource simulator;
        private MedicalXRBenchmarkRunner runner;

        private static readonly int[] AvailableResolutions = { 16, 32, 48, 64, 96, 128 };

        void OnEnable()
        {
            FindReferences();
        }

        private void FindReferences()
        {
            if (voxelizer == null) voxelizer = FindAnyObjectByType<SdfVolumeVoxelizer>();
            if (controller == null) controller = FindAnyObjectByType<SdfGroupController>();
            if (simulator == null) simulator = FindAnyObjectByType<DataStreamSimulatorSource>();
            if (runner == null) runner = FindAnyObjectByType<MedicalXRBenchmarkRunner>();
        }

        void Update()
        {
            HandleInputs();

            // Calcolo FPS smussato
            float dt = Time.unscaledDeltaTime;
            accumulatedDeltaTime += dt;
            accumulatedFrames++;

            if (accumulatedDeltaTime >= fpsUpdateInterval)
            {
                currentFps = accumulatedFrames / accumulatedDeltaTime;
                currentFrameTimeMs = (accumulatedDeltaTime / accumulatedFrames) * 1000f;
                accumulatedDeltaTime = 0f;
                accumulatedFrames = 0;
            }
        }

        private void HandleInputs()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f1Key.wasPressedThisFrame) showHUD = !showHUD;
                if (controller != null && kb.f2Key.wasPressedThisFrame) controller.renderSolidMesh = !controller.renderSolidMesh;
                if (voxelizer != null && kb.f3Key.wasPressedThisFrame) CycleResolution();
                if (kb.f4Key.wasPressedThisFrame) TriggerBenchmark();
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(toggleKey)) showHUD = !showHUD;
            if (controller != null && Input.GetKeyDown(toggleSolidMeshKey)) controller.renderSolidMesh = !controller.renderSolidMesh;
            if (voxelizer != null && Input.GetKeyDown(cycleResolutionKey)) CycleResolution();
            if (Input.GetKeyDown(benchmarkKey)) TriggerBenchmark();
#endif
        }

        public void TriggerBenchmark()
        {
            FindReferences();
            if (runner == null) runner = gameObject.AddComponent<MedicalXRBenchmarkRunner>();
            if (runner != null && !runner.isRunning)
            {
                runner.StartBenchmark();
            }
        }

        public void CycleResolution()
        {
            FindReferences();
            if (voxelizer == null) return;
            int current = voxelizer.textureResolution;
            int nextIdx = 0;
            for (int i = 0; i < AvailableResolutions.Length; i++)
            {
                if (AvailableResolutions[i] > current)
                {
                    nextIdx = i;
                    break;
                }
            }
            voxelizer.textureResolution = AvailableResolutions[nextIdx];
            Debug.Log($"[Performance HUD] Risoluzione Voxelizer impostata a: {voxelizer.textureResolution}^3");
        }

        void OnGUI()
        {
            if (!showHUD) return;

            FindReferences();

            // Stile compatto
            GUI.skin.box.fontSize = 12;
            GUI.skin.label.fontSize = 12;

            float x = 15f;
            float y = 15f;
            float width = 340f;
            float height = (runner != null && runner.isRunning) ? 220f : 190f;

            GUI.color = new Color(0.05f, 0.05f, 0.1f, 0.90f);
            GUI.Box(new Rect(x, y, width, height), GUIContent.none);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(x + 10, y + 8, width - 20, height - 16));

            GUILayout.Label("<b>📊 MEDICAL XR PERFORMANCE HUD</b>", GUILayout.Height(18));

            // FPS colorati per target XR (72/90 fps)
            Color fpsColor = currentFps >= 71f ? Color.green : (currentFps >= 45f ? Color.yellow : Color.red);
            GUI.color = fpsColor;
            GUILayout.Label($"<b>FPS: {currentFps:F1}</b>  ({currentFrameTimeMs:F2} ms)", GUILayout.Height(18));
            GUI.color = Color.white;

            // Risoluzione Voxelizer e VRAM
            if (voxelizer != null)
            {
                int res = voxelizer.textureResolution;
                long totalVoxels = (long)res * res * res;
                float vramMb = (totalVoxels * 8f) / (1024f * 1024f); // 8 bytes per voxel (RGBAHalf)
                GUILayout.Label($"Voxelizer 3D: <b>{res}x{res}x{res}</b> ({totalVoxels:N0} voxels)");
                GUILayout.Label($"VRAM Texture3D (R16G16B16A16): <b>{vramMb:F2} MB</b>");
            }
            else
            {
                GUILayout.Label("Voxelizer: Non trovato");
            }

            // Stato Solid Mesh (Decoupling)
            if (controller != null)
            {
                bool solid = controller.renderSolidMesh;
                GUI.color = solid ? new Color(1f, 0.7f, 0.2f) : Color.cyan;
                GUILayout.Label(solid ? "Mesh Mode: <b>Raymarch Visivo ON</b>" : "Mesh Mode: <b>Decoupled Physics ONLY</b>");
                GUI.color = Color.white;
            }

            // Scenario Attivo
            if (simulator != null && simulator.isActiveAndEnabled)
            {
                GUILayout.Label($"Scenario Clinico: <b>{simulator.currentScenario}</b>");
            }

            // Benchmark Running Banner
            if (runner != null && runner.isRunning)
            {
                GUILayout.Space(2);
                GUI.color = Color.yellow;
                GUILayout.Label($"<b>⚡ BENCHMARK: {runner.currentTestStatus} ({runner.progressPercent * 100f:F0}%)</b>");
                GUI.color = Color.white;
            }

            // Shortcuts
            GUILayout.Space(4);
            GUI.color = new Color(0.75f, 0.75f, 0.75f);
            GUILayout.Label("<size=10>[F1] HUD | [F2] Raymarch | [F3] Res | [F4] Benchmark</size>");
            GUI.color = Color.white;

            GUILayout.EndArea();
        }
    }
}
