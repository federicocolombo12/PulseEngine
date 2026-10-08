#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using GenericDataStreaming;
using GenericDataStreaming.Modifiers;
// using GenericDataStreaming.Demo; removed
using UnityEngine.VFX;

namespace MedicalXR.EditorScripts
{
    public class GenerateRoutingDemoScene : EditorWindow
    {
        [MenuItem("MedicalXR/Generate DataStream Routing Demo Scene")]
        public static void GenerateScene()
        {
            // 1. Create a new empty scene
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 2. Create the DataStreamRegistry
            GameObject registryGo = new GameObject("DataStreamRegistry");
            DataStreamRegistry registry = registryGo.AddComponent<DataStreamRegistry>();
            
            // Add Data Sources (Default to OSC)
            var osc = registryGo.AddComponent<MedicalXR.Checkme.CheckmeOscReceiver>();
            osc.port = 7000;
            var sim = registryGo.AddComponent<DataStreamSimulatorSource>();
            sim.enabled = false; // Disabled by default
            
            registry.channels = new System.Collections.Generic.List<DataStreamChannel>
            {
                new DataStreamChannel { id = "heart_rate", inputRange = new Vector2(40, 180), outputRange = new Vector2(0.5f, 2f) },
                new DataStreamChannel { id = "spo2", inputRange = new Vector2(85, 100), outputRange = new Vector2(0, 1) },
                new DataStreamChannel { id = "heart_pulse", inputRange = new Vector2(0, 1), outputRange = new Vector2(0, 1) },
                new DataStreamChannel { id = "is_hypoxic", inputRange = new Vector2(0, 1), outputRange = new Vector2(0, 1) }
            };
            registry.registeredTriggers = new System.Collections.Generic.List<string> { "heartbeat" };

            // 3. Create the Modifiers (Decay and Math)
            GameObject modifiersGo = new GameObject("DataStream_Modifiers");
            
            var decayTrigger = modifiersGo.AddComponent<DataStreamDecayTrigger>();
            decayTrigger.triggerId = "heartbeat";
            decayTrigger.outputChannelId = "heart_pulse";
            decayTrigger.decaySpeed = 3.0f;

            var mathModifier = modifiersGo.AddComponent<DataStreamMathModifier>();
            mathModifier.sourceChannelId = "spo2";
            mathModifier.targetChannelId = "is_hypoxic";
            mathModifier.operation = MathOperation.ThresholdToBinaryInverted;
            mathModifier.operand = 90f; // Se spo2 < 90, is_hypoxic = 1

            // 4. Create SDF Infrastructure
            GameObject sdfGo = new GameObject("SDF_Volume");
            sdfGo.AddComponent<MeshFilter>();
            var mr = sdfGo.AddComponent<MeshRenderer>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.federicocolombo.pulseengine/Runtime/VolumetricSDF/Materials/BioSignalSDF_Mat.mat");
            if (mat == null) mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Core/RayMarching/Material/BioSignalSDF_Mat.mat");
            if (mat != null) mr.sharedMaterial = mat;
            var sdfController = sdfGo.AddComponent<MedicalXR.RayMarching.SdfGroupController>();
            
            // Add component but disable it first to prevent premature OnEnable execution
            var voxelizer = sdfGo.AddComponent<MedicalXR.RayMarching.SdfVolumeVoxelizer>();
            voxelizer.enabled = false; 
            
            // Load Compute Shader
            ComputeShader computeAsset = AssetDatabase.LoadAssetAtPath<ComputeShader>("Packages/com.federicocolombo.pulseengine/Runtime/VolumetricSDF/Shaders/SdfVolumeVoxelizer.compute");
            if (computeAsset == null)
            {
                computeAsset = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/_Core/RayMarching/Shaders/SdfVolumeVoxelizer.compute");
            }
            voxelizer.voxelizerShader = computeAsset;

            // Add a base sphere to the SDF
            GameObject sphereGo = new GameObject("SdfNode_HeartSphere");
            sphereGo.transform.SetParent(sdfGo.transform);
            var sdfSphere = sphereGo.AddComponent<MedicalXR.RayMarching.SdfNode>();
            sdfSphere.shapeType = MedicalXR.RayMarching.SdfShapeType.Sphere;
            sdfSphere.size = new Vector3(0.3f, 0.3f, 0.3f);
            sdfSphere.combineOp = MedicalXR.RayMarching.SdfCombineOp.SmoothUnion;
            sdfSphere.shapeColor = Color.red;
            
            // 5. Create Target VFX
            GameObject vfxGo = new GameObject("VfxCheckme_Target");
            var vfxComponent = vfxGo.AddComponent<VisualEffect>();
            VisualEffectAsset vfxAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>("Packages/com.federicocolombo.pulseengine/Runtime/VFX/VfxCheckme.vfx");
            if (vfxAsset == null)
            {
                vfxAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>("Assets/_Core/CheckmeIntegration/Vfx/VfxCheckme.vfx");
            }
            vfxComponent.visualEffectAsset = vfxAsset;
            
            // Link Voxelizer to VFX
            voxelizer.targetVfxGraph = vfxComponent;
            voxelizer.enabled = true; // Re-enable it now that everything is assigned (triggers OnEnable properly)
            
            // 6. Create Binders
            // Binder for Turbulence (Heart Rate)
            var turbulenceBinder = vfxGo.AddComponent<DataStreamPropertyBinder>();
            turbulenceBinder.channelId = "heart_rate";
            turbulenceBinder.targetComponent = vfxComponent;
            turbulenceBinder.targetPropertyName = "TurbulenceIntensity";
            
            // Binder for SpO2 (modulates ParticleScale on VFX)
            var spo2Binder = vfxGo.AddComponent<DataStreamPropertyBinder>();
            spo2Binder.channelId = "spo2";
            spo2Binder.targetComponent = vfxComponent;
            spo2Binder.targetPropertyName = "ParticleScale";
            spo2Binder.useLocalRemap = true;
            spo2Binder.localRemap = new Vector2(0.4f, 1.5f);

            // 7. Create Canvas UI
            GameObject canvasGo = new GameObject("Welcome_Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            GameObject panelGo = new GameObject("Background_Panel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelImage = panelGo.AddComponent<UnityEngine.UI.Image>();
            panelImage.color = new Color(0, 0, 0, 0.7f);
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;

            GameObject textGo = new GameObject("Welcome_Text");
            textGo.transform.SetParent(panelGo.transform, false);
            var text = textGo.AddComponent<UnityEngine.UI.Text>();
            text.text = "MedicalXR Bio-Feedback Engine\n\n1. Open 'MedicalXR -> DAW Mixer' to manage data routing.\n2. Choose protocol: Unity Simulator, OSC / Checkme, WebSocket, Serial.\n3. Switch clinical scenarios (Calma, Stress, Ipossia) or apply Scene Templates.";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); 
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            canvasGo.SetActive(false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            // 8. Camera Setup
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                mainCam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = Color.black;
            mainCam.transform.position = new Vector3(0, 0, -3f);
            mainCam.transform.rotation = Quaternion.identity;

            // 9. Save Scene
            string path = "Assets/_Core/DataStreaming/Scenes";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder("Assets/_Core/DataStreaming", "Scenes");
            }
            string scenePath = path + "/DataStreamRoutingDemo.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);

            Debug.Log($"[MedicalXR] DataStream Routing Demo Scene generated at {scenePath}");
        }
    }
}
#endif
