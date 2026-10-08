#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using GenericDataStreaming;
using MedicalXR.RayMarching;

namespace MedicalXR.EditorScripts
{
    [InitializeOnLoad]
    public static class GenerateClinicalTemplates
    {
        static GenerateClinicalTemplates()
        {
            EditorApplication.delayCall += () =>
            {
                if (!System.IO.File.Exists("Assets/_Core/SDFTemplate/Calma.asset"))
                {
                    GenerateAll();
                }
            };
        }

        [MenuItem("MedicalXR/Generate Clinical Scene Templates")]
        public static void GenerateAll()
        {
            EnsureDirectory("Assets/_Core/SDFTemplate");

            CreateCalmaTemplate();
            CreateStressTemplate();
            CreateIpossiaTemplate();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GenerateClinicalTemplates] I 3 Template Clinici ('Calma', 'Stress', 'Ipossia') sono stati generati con successo!");
        }

        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string[] parts = path.Split('/');
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

        private static void CreateCalmaTemplate()
        {
            var template = ScriptableObject.CreateInstance<SdfSceneTemplate>();
            template.name = "Calma";
            template.textureResolution = 96;
            template.volumeSize = 2.0f;
            template.renderSolidMesh = false;

            template.globalBlendSoftness = 0.35f;
            template.pulseMultiplier = 0.8f;
            template.pulseDecaySpeed = 3.0f;
            template.shaderPulseParam = "_RWavePulse";

            // Nodi SDF (Sfera + Fiore organico da Assets/_Core/VFX/3fiore2.asset)
            var texFiore = AssetDatabase.LoadAssetAtPath<Texture3D>("Assets/_Core/VFX/3fiore2.asset");
            
            template.nodes = new List<SdfNodeSnapshot>
            {
                new SdfNodeSnapshot
                {
                    nodeName = "SdfNode_CoreSphere",
                    shapeType = SdfShapeType.Sphere,
                    size = new Vector3(0.18f, 0.18f, 0.18f),
                    combineOp = SdfCombineOp.SmoothUnion,
                    shapeColor = new Color(0.0f, 0.85f, 0.95f, 1.0f), // Ciano bioluminescente
                    pulseSensitivity = 0.5f,
                    localPosition = Vector3.zero,
                    localScale = Vector3.one,
                    cyclicAmplitude = new Vector3(0.06f, 0f, 0.06f),
                    cyclicFrequency = new Vector3(1f, 0f, 2f),
                    cyclicAmplitudeMultiplier = 1f
                },
                new SdfNodeSnapshot
                {
                    nodeName = "SdfNode_FlowerPetals",
                    shapeType = texFiore != null ? SdfShapeType.BakedTexture3D : SdfShapeType.Torus,
                    bakedSdfTexture = texFiore,
                    size = new Vector3(0.22f, 0.22f, 0.22f),
                    combineOp = SdfCombineOp.SmoothUnion,
                    blendSoftness = 0.35f, // Blend softness per-nodo
                    shapeColor = new Color(0.05f, 0.45f, 0.85f, 1.0f), // Blu mare rilassante
                    pulseSensitivity = 1.0f,
                    localPosition = new Vector3(0f, 0.02f, 0f),
                    localScale = Vector3.one
                }
            };

            // Canali
            template.channels = new List<ChannelSnapshot>
            {
                new ChannelSnapshot { id = "heart_rate", sourceType = ChannelSourceType.External, inputRange = new Vector2(40, 180), outputRange = new Vector2(0.5f, 2f), smoothing = 0.5f },
                new ChannelSnapshot { id = "spo2", sourceType = ChannelSourceType.External, inputRange = new Vector2(85, 100), outputRange = new Vector2(0, 1), smoothing = 0.5f },
                new ChannelSnapshot { id = "hrv_rmssd", sourceType = ChannelSourceType.External, inputRange = new Vector2(10, 100), outputRange = new Vector2(0, 1), smoothing = 0.5f },
                new ChannelSnapshot { id = "hrv_sdnn", sourceType = ChannelSourceType.External, inputRange = new Vector2(10, 100), outputRange = new Vector2(0, 1), smoothing = 0.5f },
                new ChannelSnapshot { id = "perfusion_index", sourceType = ChannelSourceType.External, inputRange = new Vector2(0.5f, 10f), outputRange = new Vector2(0, 1), smoothing = 0.5f },
                new ChannelSnapshot { id = "heart_pulse", sourceType = ChannelSourceType.Virtual, filterType = ChannelFilterType.SineWavePulse, sourceChannelId = "heart_rate", inputRange = new Vector2(0, 1), outputRange = new Vector2(0, 1), smoothing = 0f }
            };

            // Binders
            var gradCalma = new Gradient();
            gradCalma.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.05f, 0.35f, 0.65f), 0.0f),
                    new GradientColorKey(new Color(0.0f, 0.85f, 0.95f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 1.0f)
                }
            );

            template.propertyBinders = new List<PropertyBinderSnapshot>
            {
                new PropertyBinderSnapshot
                {
                    channelId = "hrv_rmssd",
                    targetGameObjectName = "SdfNode_CoreSphere",
                    targetComponentType = typeof(SdfNode).FullName,
                    targetPropertyName = "cyclicAmplitudeMultiplier",
                    useLocalRemap = true,
                    localRemap = new Vector2(-10f, 10f) // Remap estremo come richiesto (-10 a 10)
                },
                new PropertyBinderSnapshot
                {
                    channelId = "hrv_rmssd",
                    targetGameObjectName = "SdfNode_FlowerPetals",
                    targetComponentType = typeof(SdfNode).FullName,
                    targetPropertyName = "blendSoftness",
                    useLocalRemap = true,
                    localRemap = new Vector2(0.25f, 0.65f) // HRV alto espande la fusione morbida organica del nodo
                },
                new PropertyBinderSnapshot
                {
                    channelId = "heart_pulse",
                    targetGameObjectName = "SDF_Volume",
                    targetComponentType = typeof(SdfGroupController).FullName,
                    targetPropertyName = "continuousPulse",
                    useLocalRemap = true,
                    localRemap = new Vector2(0f, 0.25f)
                },
                new PropertyBinderSnapshot
                {
                    channelId = "heart_rate",
                    targetGameObjectName = "VfxCheckme_Target",
                    targetComponentType = typeof(UnityEngine.VFX.VisualEffect).FullName,
                    targetPropertyName = "TurbulenceIntensity",
                    useLocalRemap = true,
                    localRemap = new Vector2(0.15f, 0.45f)
                }
            };

            template.colorBinders = new List<ColorBinderSnapshot>
            {
                new ColorBinderSnapshot
                {
                    channelId = "spo2",
                    targetGameObjectName = "VfxCheckme_Target",
                    targetComponentType = typeof(UnityEngine.VFX.VisualEffect).FullName,
                    targetPropertyName = "Color",
                    colorGradient = gradCalma
                }
            };

            string path = "Assets/_Core/SDFTemplate/Calma.asset";
            if (AssetDatabase.LoadAssetAtPath<SdfSceneTemplate>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(template, path);
        }

        private static void CreateStressTemplate()
        {
            var template = ScriptableObject.CreateInstance<SdfSceneTemplate>();
            template.name = "Stress";
            template.textureResolution = 96;
            template.volumeSize = 2.0f;
            template.renderSolidMesh = false;

            template.globalBlendSoftness = 0.08f; // Molto più teso/spigoloso
            template.pulseMultiplier = 1.6f;
            template.pulseDecaySpeed = 5.0f;
            template.shaderPulseParam = "_RWavePulse";

            // Nodi SDF (Sfera + Spuntoni da Assets/_Core/VFX/1spuntone.asset)
            var texSpuntone = AssetDatabase.LoadAssetAtPath<Texture3D>("Assets/_Core/VFX/1spuntone.asset");

            template.nodes = new List<SdfNodeSnapshot>
            {
                new SdfNodeSnapshot
                {
                    nodeName = "SdfNode_StressCore",
                    shapeType = SdfShapeType.Sphere,
                    size = new Vector3(0.15f, 0.15f, 0.15f),
                    combineOp = SdfCombineOp.SmoothUnion,
                    shapeColor = new Color(0.95f, 0.15f, 0.1f, 1.0f), // Rosso fiamma
                    pulseSensitivity = 0.8f,
                    localPosition = Vector3.zero,
                    localScale = Vector3.one
                },
                new SdfNodeSnapshot
                {
                    nodeName = "SdfNode_Spikes",
                    shapeType = texSpuntone != null ? SdfShapeType.BakedTexture3D : SdfShapeType.Box,
                    bakedSdfTexture = texSpuntone,
                    size = new Vector3(0.24f, 0.24f, 0.24f),
                    combineOp = SdfCombineOp.SmoothUnion,
                    blendSoftness = 0.08f, // Blend softness per-nodo
                    shapeColor = new Color(1.0f, 0.55f, 0.0f, 1.0f), // Ambra intenso
                    pulseSensitivity = 1.4f,
                    localPosition = Vector3.zero,
                    localScale = Vector3.one
                }
            };

            template.channels = new List<ChannelSnapshot>
            {
                new ChannelSnapshot { id = "heart_rate", sourceType = ChannelSourceType.External, inputRange = new Vector2(40, 180), outputRange = new Vector2(0.5f, 2f), smoothing = 0.5f },
                new ChannelSnapshot { id = "spo2", sourceType = ChannelSourceType.External, inputRange = new Vector2(85, 100), outputRange = new Vector2(0, 1), smoothing = 0.5f },
                new ChannelSnapshot { id = "hrv_rmssd", sourceType = ChannelSourceType.External, inputRange = new Vector2(10, 100), outputRange = new Vector2(0, 1), smoothing = 0.5f },
                new ChannelSnapshot { id = "hrv_sdnn", sourceType = ChannelSourceType.External, inputRange = new Vector2(10, 100), outputRange = new Vector2(0, 1), smoothing = 0.5f },
                new ChannelSnapshot { id = "perfusion_index", sourceType = ChannelSourceType.External, inputRange = new Vector2(0.5f, 10f), outputRange = new Vector2(0, 1), smoothing = 0.5f },
                new ChannelSnapshot { id = "heart_pulse", sourceType = ChannelSourceType.Virtual, filterType = ChannelFilterType.SineWavePulse, sourceChannelId = "heart_rate", inputRange = new Vector2(0, 1), outputRange = new Vector2(0, 1), smoothing = 0f }
            };

            var gradStress = new Gradient();
            gradStress.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1.0f, 0.55f, 0.0f), 0.0f),
                    new GradientColorKey(new Color(1.0f, 0.05f, 0.02f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 1.0f)
                }
            );

            template.propertyBinders = new List<PropertyBinderSnapshot>
            {
                new PropertyBinderSnapshot
                {
                    channelId = "hrv_rmssd",
                    targetGameObjectName = "VfxCheckme_Target",
                    targetComponentType = typeof(UnityEngine.VFX.VisualEffect).FullName,
                    targetPropertyName = "TurbulenceIntensity",
                    useLocalRemap = true,
                    localRemap = new Vector2(3.0f, 0.5f) // Invertito: HRV basso (<20ms in stress) scatena massima turbolenza
                },
                new PropertyBinderSnapshot
                {
                    channelId = "hrv_rmssd",
                    targetGameObjectName = "SdfNode_Spikes",
                    targetComponentType = typeof(SdfNode).FullName,
                    targetPropertyName = "blendSoftness",
                    useLocalRemap = true,
                    localRemap = new Vector2(0.04f, 0.22f) // HRV basso rende l'SDF teso e spigoloso sul nodo
                },
                new PropertyBinderSnapshot
                {
                    channelId = "heart_pulse",
                    targetGameObjectName = "SDF_Volume",
                    targetComponentType = typeof(SdfGroupController).FullName,
                    targetPropertyName = "continuousPulse",
                    useLocalRemap = true,
                    localRemap = new Vector2(0f, 0.55f)
                }
            };

            template.colorBinders = new List<ColorBinderSnapshot>
            {
                new ColorBinderSnapshot
                {
                    channelId = "heart_rate",
                    targetGameObjectName = "VfxCheckme_Target",
                    targetComponentType = typeof(UnityEngine.VFX.VisualEffect).FullName,
                    targetPropertyName = "Color",
                    colorGradient = gradStress
                }
            };

            string path = "Assets/_Core/SDFTemplate/Stress.asset";
            if (AssetDatabase.LoadAssetAtPath<SdfSceneTemplate>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(template, path);
        }

        private static void CreateIpossiaTemplate()
        {
            var template = ScriptableObject.CreateInstance<SdfSceneTemplate>();
            template.name = "Ipossia";
            template.textureResolution = 96;
            template.volumeSize = 2.0f;
            template.renderSolidMesh = false;

            template.globalBlendSoftness = 0.18f;
            template.pulseMultiplier = 0.6f;
            template.pulseDecaySpeed = 2.0f;
            template.shaderPulseParam = "_RWavePulse";

            // Nodi SDF (Sfera + Curva/Infinito da Assets/_Core/VFX/4curvaunita2.asset)
            var texCurva = AssetDatabase.LoadAssetAtPath<Texture3D>("Assets/_Core/VFX/4curvaunita2.asset") 
                         ?? AssetDatabase.LoadAssetAtPath<Texture3D>("Assets/_Core/VFX/6infinito2.asset");

            template.nodes = new List<SdfNodeSnapshot>
            {
                new SdfNodeSnapshot
                {
                    nodeName = "SdfNode_HypoxicCore",
                    shapeType = SdfShapeType.Sphere,
                    size = new Vector3(0.14f, 0.14f, 0.14f),
                    combineOp = SdfCombineOp.SmoothUnion,
                    shapeColor = new Color(0.35f, 0.05f, 0.45f, 1.0f), // Viola cianotico scuro
                    pulseSensitivity = 0.4f,
                    localPosition = Vector3.zero,
                    localScale = Vector3.one,
                    cyclicAmplitude = new Vector3(0.08f, 0f, 0.08f), // Movimento circolare/oscillatorio
                    cyclicFrequency = new Vector3(1f, 0f, 1f),
                    cyclicAmplitudeMultiplier = 1f
                },
                new SdfNodeSnapshot
                {
                    nodeName = "SdfNode_Distortion",
                    shapeType = texCurva != null ? SdfShapeType.BakedTexture3D : SdfShapeType.Cylinder,
                    bakedSdfTexture = texCurva,
                    size = new Vector3(0.2f, 0.2f, 0.2f),
                    combineOp = SdfCombineOp.SmoothUnion,
                    blendSoftness = 0.18f, // Blend softness per-nodo
                    shapeColor = new Color(0.12f, 0.18f, 0.35f, 1.0f), // Blu freddo asfittico
                    pulseSensitivity = 0.6f,
                    localPosition = new Vector3(0f, 0.03f, 0f),
                    localScale = Vector3.one
                }
            };

            template.channels = new List<ChannelSnapshot>
            {
                new ChannelSnapshot { id = "heart_rate", sourceType = ChannelSourceType.External, inputRange = new Vector2(40, 180), outputRange = new Vector2(0.5f, 2f), smoothing = 0.5f },
                new ChannelSnapshot { id = "spo2", sourceType = ChannelSourceType.External, inputRange = new Vector2(75, 100), outputRange = new Vector2(0, 1), smoothing = 0.5f },
                new ChannelSnapshot { id = "hrv_rmssd", sourceType = ChannelSourceType.External, inputRange = new Vector2(10, 100), outputRange = new Vector2(0, 1), smoothing = 0.5f },
                new ChannelSnapshot { id = "hrv_sdnn", sourceType = ChannelSourceType.External, inputRange = new Vector2(10, 100), outputRange = new Vector2(0, 1), smoothing = 0.5f },
                new ChannelSnapshot { id = "perfusion_index", sourceType = ChannelSourceType.External, inputRange = new Vector2(0.5f, 10f), outputRange = new Vector2(0, 1), smoothing = 0.5f },
                new ChannelSnapshot { id = "heart_pulse", sourceType = ChannelSourceType.Virtual, filterType = ChannelFilterType.SineWavePulse, sourceChannelId = "heart_rate", inputRange = new Vector2(0, 1), outputRange = new Vector2(0, 1), smoothing = 0f }
            };

            template.propertyBinders = new List<PropertyBinderSnapshot>
            {
                new PropertyBinderSnapshot
                {
                    channelId = "hrv_rmssd",
                    targetGameObjectName = "SdfNode_HypoxicCore",
                    targetComponentType = typeof(SdfNode).FullName,
                    targetPropertyName = "cyclicAmplitudeMultiplier",
                    useLocalRemap = true,
                    localRemap = new Vector2(-10f, 10f) // Remap estremo tra -10 e 10
                },
                new PropertyBinderSnapshot
                {
                    channelId = "heart_pulse",
                    targetGameObjectName = "SDF_Volume",
                    targetComponentType = typeof(SdfGroupController).FullName,
                    targetPropertyName = "continuousPulse",
                    useLocalRemap = true,
                    localRemap = new Vector2(0f, 0.18f)
                },
                new PropertyBinderSnapshot
                {
                    channelId = "heart_rate",
                    targetGameObjectName = "VfxCheckme_Target",
                    targetComponentType = typeof(UnityEngine.VFX.VisualEffect).FullName,
                    targetPropertyName = "TurbulenceIntensity",
                    useLocalRemap = true,
                    localRemap = new Vector2(0.05f, 0.25f)
                }
            };

            // Gradiente cianotico per SpO2
            var gradIpossia = new Gradient();
            gradIpossia.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.35f, 0.05f, 0.45f), 0.0f),  // SpO2 bassa = Viola cianotico
                    new GradientColorKey(new Color(0.0f, 0.75f, 0.95f), 1.0f)     // SpO2 normale = Ciano limpido
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 1.0f)
                }
            );

            template.colorBinders = new List<ColorBinderSnapshot>
            {
                new ColorBinderSnapshot
                {
                    channelId = "spo2",
                    targetGameObjectName = "VfxCheckme_Target",
                    targetComponentType = typeof(UnityEngine.VFX.VisualEffect).FullName,
                    targetPropertyName = "Color",
                    colorGradient = gradIpossia
                }
            };

            string path = "Assets/_Core/SDFTemplate/Ipossia.asset";
            if (AssetDatabase.LoadAssetAtPath<SdfSceneTemplate>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(template, path);
        }
    }
}
#endif
