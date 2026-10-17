#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using GenericDataStreaming;
using GenericDataStreaming.Modifiers;
using UnityEngine.VFX;

namespace MedicalXR.EditorScripts
{
    public class GenerateRoutingDemoScene : EditorWindow
    {
        public static void GenerateScene() => GenerateSignalRoutingScene();

        [MenuItem("MedicalXR/Generate Signal Routing Demo Scene")]
        public static void GenerateSignalRoutingScene()
        {
            // 1. Create a new empty scene
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Camera Setup
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera mainCam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.05f, 0.07f, 0.1f);
            mainCam.transform.position = new Vector3(0, 0, -3.5f);
            mainCam.transform.rotation = Quaternion.identity;

            // 3. Directional Light
            GameObject lightGo = new GameObject("Directional Light");
            Light dirLight = lightGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(1f, 0.95f, 0.85f);
            dirLight.intensity = 1.0f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 4. DataStreamRegistry & Sources
            GameObject registryGo = new GameObject("DataStreamRegistry");
            DataStreamRegistry registry = registryGo.AddComponent<DataStreamRegistry>();
            
            var osc = registryGo.AddComponent<MedicalXR.Checkme.CheckmeOscReceiver>();
            osc.port = 7000;

            var sim = registryGo.AddComponent<DataStreamSimulatorSource>();
            sim.enabled = true; // Enabled by default for immediate plug-and-play testing
            
            registry.channels = new List<DataStreamChannel>
            {
                new DataStreamChannel { id = "heart_rate", inputRange = new Vector2(40, 180), outputRange = new Vector2(0.5f, 2f), smoothing = 0.3f },
                new DataStreamChannel { id = "spo2", inputRange = new Vector2(85, 100), outputRange = new Vector2(0, 1), smoothing = 0.5f },
                new DataStreamChannel { id = "heart_pulse", inputRange = new Vector2(0, 1), outputRange = new Vector2(0, 1), smoothing = 0f },
                new DataStreamChannel { id = "is_hypoxic", inputRange = new Vector2(0, 1), outputRange = new Vector2(0, 1), smoothing = 0f }
            };
            registry.registeredTriggers = new List<string> { "heartbeat" };

            // 5. Signal Modifiers
            GameObject modifiersGo = new GameObject("DataStream_Modifiers");
            
            var decayTrigger = modifiersGo.AddComponent<DataStreamDecayTrigger>();
            decayTrigger.triggerId = "heartbeat";
            decayTrigger.outputChannelId = "heart_pulse";
            decayTrigger.decaySpeed = 3.0f;
            decayTrigger.maxIntensity = 1.0f;

            var mathModifier = modifiersGo.AddComponent<DataStreamMathModifier>();
            mathModifier.sourceChannelId = "spo2";
            mathModifier.targetChannelId = "is_hypoxic";
            mathModifier.operation = MathOperation.ThresholdToBinaryInverted;
            mathModifier.operand = 90f; // Se SpO2 <= 90%, is_hypoxic = 1

            // 6. Visual Target: Standalone Pulsing Object
            GameObject visualCore = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visualCore.name = "PulsingObject";
            visualCore.transform.position = Vector3.zero;
            visualCore.transform.localScale = Vector3.one;

            // Create and assign an aesthetic URP Lit material
            Material litMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            litMat.name = "PulsingCore_Material";
            litMat.color = new Color(0.9f, 0.2f, 0.3f, 1.0f);
            litMat.SetFloat("_Smoothness", 0.85f);
            visualCore.GetComponent<MeshRenderer>().sharedMaterial = litMat;

            // 7. DataStream Binders
            // Binder 1: Heart pulse -> Scale
            var scaleBinder = visualCore.AddComponent<DataStreamPropertyBinder>();
            scaleBinder.channelId = "heart_pulse";
            scaleBinder.targetComponent = visualCore.transform;
            scaleBinder.targetPropertyName = "localScale";
            scaleBinder.weightMultiplier = 1.0f;
            scaleBinder.useLocalRemap = true;
            scaleBinder.localRemap = new Vector2(0.9f, 1.35f);

            // Binder 2: SpO2 -> Color Remap
            var colorBinder = visualCore.AddComponent<DataStreamColorBinder>();
            colorBinder.channelId = "spo2";
            colorBinder.targetComponent = visualCore.GetComponent<MeshRenderer>();
            colorBinder.targetPropertyName = "_BaseColor";
            
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.1f, 0.35f, 0.95f), 0.0f),  // Hypoxic deep blue
                    new GradientColorKey(new Color(1.0f, 0.25f, 0.3f), 1.0f)   // Normal oxygenated red
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 1.0f)
                }
            );
            colorBinder.colorGradient = grad;

            // Binder 3: Heart Pulse -> Light Intensity
            GameObject pointLightGo = new GameObject("PulsingLight");
            pointLightGo.transform.position = new Vector3(0, 1.5f, -1.0f);
            Light pointLight = pointLightGo.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.range = 8.0f;
            pointLight.color = new Color(1.0f, 0.4f, 0.5f);
            pointLight.intensity = 1.0f;

            var lightBinder = pointLightGo.AddComponent<DataStreamPropertyBinder>();
            lightBinder.channelId = "heart_pulse";
            lightBinder.targetComponent = pointLight;
            lightBinder.targetPropertyName = "intensity";
            lightBinder.useLocalRemap = true;
            lightBinder.localRemap = new Vector2(0.5f, 3.5f);

            // 8. Save Scene
            string path = "Assets/_Core/DataStreaming/Scenes";
            EnsureDirectory(path);
            string scenePath = path + "/SignalRoutingDemo.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);

            Debug.Log($"[PulseEngine] Signal Routing Demo Scene generata con successo a: {scenePath}");
        }

        [MenuItem("MedicalXR/Generate Clinical Biofeedback Demo Scene")]
        public static void GenerateClinicalBiofeedbackScene()
        {
            Type controllerType = Type.GetType("MedicalXR.RayMarching.SdfGroupController, MedicalXR.VolumetricSDF");
            if (controllerType == null)
            {
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = a.GetType("MedicalXR.RayMarching.SdfGroupController");
                    if (t != null) { controllerType = t; break; }
                }
            }

            if (controllerType == null)
            {
                bool copy = EditorUtility.DisplayDialog(
                    "Modulo Companion Richiesto: VolumetricSDF",
                    "La scena di biofeedback clinico avanzato combina la telemetria di PulseEngine con il raymarching implicito GPU e il voxelizer di VolumetricSDF.\n\n" +
                    "Per generare questa scena è necessario aver installato il pacchetto companion 'com.federicocolombo.volumetricsdf'.\n\n" +
                    "Vuoi copiare l'URL Git del pacchetto negli appunti per installarlo tramite Unity Package Manager?",
                    "📋 Copia Git URL",
                    "Annulla"
                );

                if (copy)
                {
                    EditorGUIUtility.systemCopyBuffer = "https://github.com/federicocolombo12/VolumetricSDF.git";
                    Debug.Log("[PulseEngine] URL Git di VolumetricSDF copiato negli appunti: https://github.com/federicocolombo12/VolumetricSDF.git");
                }
                return;
            }

            // 1. Create a new empty scene
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 2. DataStreamRegistry & Sources
            GameObject registryGo = new GameObject("DataStreamRegistry");
            DataStreamRegistry registry = registryGo.AddComponent<DataStreamRegistry>();
            
            var osc = registryGo.AddComponent<MedicalXR.Checkme.CheckmeOscReceiver>();
            osc.port = 7000;
            var sim = registryGo.AddComponent<DataStreamSimulatorSource>();
            sim.enabled = true;
            
            registry.channels = new List<DataStreamChannel>
            {
                new DataStreamChannel { id = "heart_rate", inputRange = new Vector2(40, 180), outputRange = new Vector2(0.5f, 2f), smoothing = 0.3f },
                new DataStreamChannel { id = "spo2", inputRange = new Vector2(85, 100), outputRange = new Vector2(0, 1), smoothing = 0.5f },
                new DataStreamChannel { id = "heart_pulse", inputRange = new Vector2(0, 1), outputRange = new Vector2(0, 1), smoothing = 0f },
                new DataStreamChannel { id = "is_hypoxic", inputRange = new Vector2(0, 1), outputRange = new Vector2(0, 1), smoothing = 0f }
            };
            registry.registeredTriggers = new List<string> { "heartbeat" };

            // 3. Modifiers
            GameObject modifiersGo = new GameObject("DataStream_Modifiers");
            var decayTrigger = modifiersGo.AddComponent<DataStreamDecayTrigger>();
            decayTrigger.triggerId = "heartbeat";
            decayTrigger.outputChannelId = "heart_pulse";
            decayTrigger.decaySpeed = 3.0f;

            var mathModifier = modifiersGo.AddComponent<DataStreamMathModifier>();
            mathModifier.sourceChannelId = "spo2";
            mathModifier.targetChannelId = "is_hypoxic";
            mathModifier.operation = MathOperation.ThresholdToBinaryInverted;
            mathModifier.operand = 90f;

            // 4. Volumetric SDF Infrastructure via Reflection
            GameObject sdfGo = new GameObject("SDF_Volume");
            sdfGo.AddComponent<MeshFilter>();
            var mr = sdfGo.AddComponent<MeshRenderer>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.federicocolombo.volumetricsdf/Runtime/Materials/BioSignalSDF_Mat.mat")
                   ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/_Core/RayMarching/Material/BioSignalSDF_Mat.mat");
            if (mat != null) mr.sharedMaterial = mat;

            sdfGo.AddComponent(controllerType);
            
            Type voxelizerType = Type.GetType("MedicalXR.RayMarching.SdfVolumeVoxelizer, MedicalXR.VolumetricSDF");
            if (voxelizerType != null)
            {
                var voxelizer = sdfGo.AddComponent(voxelizerType) as MonoBehaviour;
                if (voxelizer != null)
                {
                    voxelizer.enabled = false;
                    ComputeShader computeAsset = AssetDatabase.LoadAssetAtPath<ComputeShader>("Packages/com.federicocolombo.volumetricsdf/Runtime/Shaders/SdfVolumeVoxelizer.compute")
                                              ?? AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/_Core/RayMarching/Shaders/SdfVolumeVoxelizer.compute");
                    var shaderField = voxelizerType.GetField("voxelizerShader");
                    shaderField?.SetValue(voxelizer, computeAsset);

                    // Add base sphere node
                    Type nodeType = Type.GetType("MedicalXR.RayMarching.SdfNode, MedicalXR.VolumetricSDF");
                    if (nodeType != null)
                    {
                        GameObject sphereGo = new GameObject("SdfNode_HeartSphere");
                        sphereGo.transform.SetParent(sdfGo.transform);
                        var node = sphereGo.AddComponent(nodeType);
                        var sizeField = nodeType.GetField("size");
                        sizeField?.SetValue(node, new Vector3(0.3f, 0.3f, 0.3f));
                    }

                    // 5. Create Target VFX
                    GameObject vfxGo = new GameObject("VfxCheckme_Target");
                    var vfxComponent = vfxGo.AddComponent<VisualEffect>();
                    VisualEffectAsset vfxAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>("Packages/com.federicocolombo.pulseengine/Runtime/VFX/VfxCheckme.vfx")
                                              ?? AssetDatabase.LoadAssetAtPath<VisualEffectAsset>("Assets/_Core/CheckmeIntegration/Vfx/VfxCheckme.vfx");
                    vfxComponent.visualEffectAsset = vfxAsset;
                    
                    var targetVfxField = voxelizerType.GetField("targetVfxGraph");
                    targetVfxField?.SetValue(voxelizer, vfxComponent);
                    voxelizer.enabled = true;

                    // 6. Binders
                    var turbulenceBinder = vfxGo.AddComponent<DataStreamPropertyBinder>();
                    turbulenceBinder.channelId = "heart_rate";
                    turbulenceBinder.targetComponent = vfxComponent;
                    turbulenceBinder.targetPropertyName = "TurbulenceIntensity";
                    
                    var spo2Binder = vfxGo.AddComponent<DataStreamPropertyBinder>();
                    spo2Binder.channelId = "spo2";
                    spo2Binder.targetComponent = vfxComponent;
                    spo2Binder.targetPropertyName = "ParticleScale";
                    spo2Binder.useLocalRemap = true;
                    spo2Binder.localRemap = new Vector2(0.4f, 1.5f);
                }
            }

            // 7. Save Scene
            string path = "Assets/_Core/DataStreaming/Scenes";
            EnsureDirectory(path);
            string scenePath = path + "/ClinicalBiofeedbackDemo.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);

            Debug.Log($"[PulseEngine] Clinical Biofeedback Demo Scene generata con successo a: {scenePath}");
        }

        private static void EnsureDirectory(string relativeAssetPath)
        {
            if (string.IsNullOrEmpty(relativeAssetPath)) return;

            string fullPath = System.IO.Path.Combine(Application.dataPath, relativeAssetPath.StartsWith("Assets/") ? relativeAssetPath.Substring(7) : (relativeAssetPath.StartsWith("Assets") ? relativeAssetPath.Substring(6) : relativeAssetPath));
            if (!System.IO.Directory.Exists(fullPath))
            {
                System.IO.Directory.CreateDirectory(fullPath);
                AssetDatabase.Refresh();
            }

            if (!AssetDatabase.IsValidFolder(relativeAssetPath))
            {
                string[] parts = relativeAssetPath.Split(new char[] { '/', '\\' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    string current = parts[0];
                    for (int i = 1; i < parts.Length; i++)
                    {
                        string next = current + "/" + parts[i];
                        if (!AssetDatabase.IsValidFolder(next))
                        {
                            AssetDatabase.CreateFolder(current, parts[i]);
                        }
                        current = next;
                    }
                }
            }
        }
    }
}
#endif
