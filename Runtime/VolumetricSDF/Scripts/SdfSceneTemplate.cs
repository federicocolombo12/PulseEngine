using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GenericDataStreaming;

namespace MedicalXR.RayMarching
{
    [System.Serializable]
    public class SdfNodeSnapshot
    {
        public string nodeName = "SdfNode";
        public int siblingIndex = 0;
        public SdfShapeType shapeType = SdfShapeType.Sphere;
        public Texture3D bakedSdfTexture;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
        public Vector3 size = new Vector3(0.12f, 0.12f, 0.12f);
        public SdfCombineOp combineOp = SdfCombineOp.SmoothUnion;
        public float blendSoftness = 0f;
        public Color shapeColor = Color.white;
        public float pulseSensitivity = 1.0f;
        public Vector3 cyclicAmplitude = Vector3.zero;
        public Vector3 cyclicFrequency = new Vector3(1f, 0f, 2f);
        public float cyclicAmplitudeMultiplier = 0f;
    }

    [System.Serializable]
    public class SdfModifierVolumeSnapshot
    {
        public string volumeName = "SdfModifierVolume";
        public int siblingIndex = 0;
        public SdfModifierType modifierType = SdfModifierType.Twist;
        public SdfModifierScope scope = SdfModifierScope.AllNodesInVolume;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
        public Vector3 size = new Vector3(0.25f, 0.25f, 0.25f);
        public float falloff = 0.05f;
        public float strength = 1.0f;
        public float frequency = 5.0f;
        public Color gizmoColor = new Color(1f, 0.6f, 0.1f, 0.35f);
    }

    [System.Serializable]
    public class ChannelSnapshot
    {
        public string id;
        public ChannelSourceType sourceType = ChannelSourceType.External;
        public ChannelFilterType filterType = ChannelFilterType.None;
        public string sourceChannelId;
        public Vector2 inputRange = new Vector2(0f, 1f);
        public Vector2 outputRange = new Vector2(0f, 1f);
        public float smoothing = 0.5f;
        public bool isMuted = false;
        public bool isSolo = false;
    }

    [System.Serializable]
    public class PropertyBinderSnapshot
    {
        public string channelId;
        public string targetGameObjectName;
        public string targetComponentType;
        public string targetPropertyName;
        public float weightMultiplier = 1.0f;
        public bool useLocalRemap = false;
        public Vector2 localRemap = new Vector2(0f, 1f);
    }

    [System.Serializable]
    public class ColorBinderSnapshot
    {
        public string channelId;
        public string targetGameObjectName;
        public string targetComponentType;
        public string targetPropertyName;
        public Gradient colorGradient;
    }

    /// <summary>
    /// Snapshot completo dello stato clinico ed estetico della scena:
    /// Nodi SDF, parametri del Voxelizer, canali del DataStreamRegistry
    /// e tutti i binder (inclusi Local Remap e Gradienti di colore).
    /// </summary>
    [CreateAssetMenu(fileName = "New Scene Template", menuName = "MedicalXR/Scene Template (Full)")]
    public class SdfSceneTemplate : ScriptableObject
    {
        [Header("Volume & Optimization")]
        public int textureResolution = 64;
        public float volumeSize = 2.0f;
        public bool renderSolidMesh = false;

        [Header("Surface Texturing")]
        public Texture2D albedoTexture;
        public float textureScale = 2.0f;
        [Range(0f, 1f)] public float textureBlend = 0.0f;
        [Range(1f, 16f)] public float triplanarSharpness = 4.0f;
        public bool enableBumpMap = false;
        public Texture2D bumpMap;
        public float bumpScale = 0.5f;
        public bool enableMatCap = false;
        public Texture2D matCapTexture;
        [Range(0f, 1f)] public float matCapBlend = 0.0f;

        [Header("SDF Blending & Pulse")]
        [Range(0.001f, 2f)] public float globalBlendSoftness = 0.2f;
        public float pulseMultiplier = 1.0f;
        public float pulseDecaySpeed = 4.0f;
        public string shaderPulseParam = "_RWavePulse";

        [Header("SDF Node Hierarchy")]
        public List<SdfNodeSnapshot> nodes = new List<SdfNodeSnapshot>();
        public List<SdfModifierVolumeSnapshot> modifierVolumes = new List<SdfModifierVolumeSnapshot>();

        [Header("DataStream Channels")]
        public List<ChannelSnapshot> channels = new List<ChannelSnapshot>();

        [Header("DataStream Binders")]
        public List<PropertyBinderSnapshot> propertyBinders = new List<PropertyBinderSnapshot>();
        public List<ColorBinderSnapshot> colorBinders = new List<ColorBinderSnapshot>();

        /// <summary>
        /// Crea uno snapshot istantaneo della scena attiva.
        /// </summary>
        public static SdfSceneTemplate CaptureCurrentScene(string templateName = "")
        {
            var template = ScriptableObject.CreateInstance<SdfSceneTemplate>();
            if (!string.IsNullOrEmpty(templateName))
            {
                template.name = templateName;
            }

            // 1. Controller & Volume
            var controller = UnityEngine.Object.FindAnyObjectByType<SdfGroupController>();
            var voxelizer = UnityEngine.Object.FindAnyObjectByType<SdfVolumeVoxelizer>();

            if (controller != null)
            {
                template.globalBlendSoftness = controller.globalBlendSoftness;
                template.pulseMultiplier = controller.pulseMultiplier;
                template.pulseDecaySpeed = controller.pulseDecaySpeed;
                template.shaderPulseParam = controller.shaderPulseParam;
                template.renderSolidMesh = controller.renderSolidMesh;

                // Nodi (ordinati rigorosamente per l'ordine di valutazione CSG top-to-bottom)
                if (controller.nodes != null)
                {
                    var sortedNodes = controller.nodes
                        .Where(n => n != null)
                        .OrderBy(n => n.transform.GetSiblingIndex())
                        .ToList();

                    foreach (var n in sortedNodes)
                    {
                        var snap = new SdfNodeSnapshot
                        {
                            nodeName = n.gameObject.name,
                            siblingIndex = n.transform.GetSiblingIndex(),
                            shapeType = n.shapeType,
                            bakedSdfTexture = n.bakedSdfTexture,
                            localPosition = n.transform.localPosition,
                            localEulerAngles = n.transform.localEulerAngles,
                            localScale = n.transform.localScale,
                            size = n.size,
                            combineOp = n.combineOp,
                            blendSoftness = n.blendSoftness,
                            shapeColor = n.shapeColor,
                            pulseSensitivity = n.pulseSensitivity,
                            cyclicAmplitude = n.cyclicAmplitude,
                            cyclicFrequency = n.cyclicFrequency,
                            cyclicAmplitudeMultiplier = n.cyclicAmplitudeMultiplier
                        };
                        template.nodes.Add(snap);
                    }
                }

                // Volumi Modificatori
                if (controller.modifierVolumes != null)
                {
                    var sortedMods = controller.modifierVolumes
                        .Where(m => m != null)
                        .OrderBy(m => m.transform.GetSiblingIndex())
                        .ToList();

                    foreach (var m in sortedMods)
                    {
                        var mSnap = new SdfModifierVolumeSnapshot
                        {
                            volumeName = m.gameObject.name,
                            siblingIndex = m.transform.GetSiblingIndex(),
                            modifierType = m.modifierType,
                            scope = m.scope,
                            localPosition = m.transform.localPosition,
                            localEulerAngles = m.transform.localEulerAngles,
                            localScale = m.transform.localScale,
                            size = m.size,
                            falloff = m.falloff,
                            strength = m.strength,
                            frequency = m.frequency,
                            gizmoColor = m.gizmoColor
                        };
                        template.modifierVolumes.Add(mSnap);
                    }
                }

                var rend = controller.GetComponent<Renderer>();
                if (rend != null && rend.sharedMaterial != null)
                {
                    var mat = rend.sharedMaterial;
                    template.albedoTexture = mat.GetTexture("_MainTex") as Texture2D;
                    template.textureScale = mat.HasProperty("_TextureScale") ? mat.GetFloat("_TextureScale") : 2.0f;
                    template.textureBlend = mat.HasProperty("_TextureBlend") ? mat.GetFloat("_TextureBlend") : 0.0f;
                    template.triplanarSharpness = mat.HasProperty("_TriplanarSharpness") ? mat.GetFloat("_TriplanarSharpness") : 4.0f;
                    template.enableBumpMap = mat.HasProperty("_EnableBumpMap") && mat.GetFloat("_EnableBumpMap") > 0.5f;
                    template.bumpMap = mat.GetTexture("_BumpMap") as Texture2D;
                    template.bumpScale = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 0.5f;
                    template.enableMatCap = mat.HasProperty("_EnableMatCap") && mat.GetFloat("_EnableMatCap") > 0.5f;
                    template.matCapTexture = mat.GetTexture("_MatCapTex") as Texture2D;
                    template.matCapBlend = mat.HasProperty("_MatCapBlend") ? mat.GetFloat("_MatCapBlend") : 0.0f;
                }
            }

            if (voxelizer != null)
            {
                template.textureResolution = voxelizer.textureResolution;
                template.volumeSize = voxelizer.volumeSize;
            }

            // 2. DataStream Registry & Canali
            if (DataStreamRegistry.Instance != null && DataStreamRegistry.Instance.channels != null)
            {
                foreach (var ch in DataStreamRegistry.Instance.channels)
                {
                    template.channels.Add(new ChannelSnapshot
                    {
                        id = ch.id,
                        sourceType = ch.sourceType,
                        filterType = ch.filterType,
                        sourceChannelId = ch.sourceChannelId,
                        inputRange = ch.inputRange,
                        outputRange = ch.outputRange,
                        smoothing = ch.smoothing,
                        isMuted = ch.isMuted,
                        isSolo = ch.isSolo
                    });
                }
            }

            // 3. Property Binders
            var pBinders = UnityEngine.Object.FindObjectsByType<DataStreamPropertyBinder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var pb in pBinders)
            {
                if (pb == null || pb.targetComponent == null) continue;
                template.propertyBinders.Add(new PropertyBinderSnapshot
                {
                    channelId = pb.channelId,
                    targetGameObjectName = pb.targetComponent.gameObject.name,
                    targetComponentType = pb.targetComponent.GetType().FullName,
                    targetPropertyName = pb.targetPropertyName,
                    weightMultiplier = pb.weightMultiplier,
                    useLocalRemap = pb.useLocalRemap,
                    localRemap = pb.localRemap
                });
            }

            // 4. Color Binders
            var cBinders = UnityEngine.Object.FindObjectsByType<DataStreamColorBinder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cb in cBinders)
            {
                if (cb == null || cb.targetComponent == null) continue;
                template.colorBinders.Add(new ColorBinderSnapshot
                {
                    channelId = cb.channelId,
                    targetGameObjectName = cb.targetComponent.gameObject.name,
                    targetComponentType = cb.targetComponent.GetType().FullName,
                    targetPropertyName = cb.targetPropertyName,
                    colorGradient = cb.colorGradient
                });
            }

            return template;
        }

        /// <summary>
        /// Applica questo template ricostruendo e aggiornando la scena corrente.
        /// </summary>
        public void ApplyToScene()
        {
            var controller = UnityEngine.Object.FindAnyObjectByType<SdfGroupController>();
            var voxelizer = UnityEngine.Object.FindAnyObjectByType<SdfVolumeVoxelizer>();

            // 1. Controller & Volume
            if (controller != null)
            {
#if UNITY_EDITOR
                UnityEditor.Undo.RecordObject(controller, "Apply Scene Template");
#endif
                controller.globalBlendSoftness = globalBlendSoftness;
                controller.pulseMultiplier = pulseMultiplier;
                controller.pulseDecaySpeed = pulseDecaySpeed;
                controller.shaderPulseParam = shaderPulseParam;
                controller.renderSolidMesh = renderSolidMesh;

                // Ricostruzione Nodi
                if (nodes != null && nodes.Count > 0)
                {
                    var existingNodes = controller.GetComponentsInChildren<SdfNode>(true).ToList();
                    var matchedNodes = new List<SdfNode>();
                    var unusedExisting = new List<SdfNode>(existingNodes);

                    // 1. Assegna o adatta i nodi, preservando l'identità dei GameObject per nome (e relativi Binders)
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        var snap = nodes[i];
                        SdfNode targetNode = unusedExisting.FirstOrDefault(en => en != null && en.gameObject.name == snap.nodeName);
                        if (targetNode != null)
                        {
                            unusedExisting.Remove(targetNode);
                        }
                        else if (unusedExisting.Count > 0)
                        {
                            targetNode = unusedExisting[0];
                            unusedExisting.RemoveAt(0);
                        }
                        else
                        {
                            var newGo = new GameObject(snap.nodeName);
                            newGo.transform.SetParent(controller.transform, false);
                            targetNode = newGo.AddComponent<SdfNode>();
#if UNITY_EDITOR
                            UnityEditor.Undo.RegisterCreatedObjectUndo(newGo, "Create Sdf Node");
#endif
                        }

#if UNITY_EDITOR
                        UnityEditor.Undo.RecordObject(targetNode.gameObject, "Configure Node");
                        UnityEditor.Undo.RecordObject(targetNode, "Configure Node");
#endif
                        targetNode.gameObject.name = snap.nodeName;
                        targetNode.shapeType = snap.shapeType;
                        targetNode.bakedSdfTexture = snap.bakedSdfTexture;
                        targetNode.transform.localPosition = snap.localPosition;
                        targetNode.transform.localEulerAngles = snap.localEulerAngles;
                        targetNode.transform.localScale = snap.localScale;
                        targetNode.size = snap.size;
                        targetNode.combineOp = snap.combineOp;
                        targetNode.blendSoftness = snap.blendSoftness;
                        targetNode.shapeColor = snap.shapeColor;
                        targetNode.pulseSensitivity = snap.pulseSensitivity;
                        targetNode.cyclicAmplitude = snap.cyclicAmplitude;
                        targetNode.cyclicFrequency = snap.cyclicFrequency;
                        targetNode.cyclicAmplitudeMultiplier = snap.cyclicAmplitudeMultiplier;

                        matchedNodes.Add(targetNode);
                    }

                    // 2. Rimuove eventuali nodi in eccesso non presenti nel template
                    foreach (var excess in unusedExisting)
                    {
                        if (excess != null)
                        {
#if UNITY_EDITOR
                            UnityEditor.Undo.DestroyObjectImmediate(excess.gameObject);
#else
                            UnityEngine.Object.Destroy(excess.gameObject);
#endif
                        }
                    }

                    // 3. Ripristina rigorosamente l'ordine gerarchico dei SiblingIndex
                    for (int i = 0; i < matchedNodes.Count; i++)
                    {
                        var snap = nodes[i];
                        int targetSibling = (snap.siblingIndex >= 0) ? snap.siblingIndex : i;
                        matchedNodes[i].transform.SetSiblingIndex(targetSibling);
                    }

                    controller.nodes = matchedNodes;
                }

                // Ricostruzione Volumi Modificatori
                if (modifierVolumes != null)
                {
                    var existingMods = controller.GetComponentsInChildren<SdfModifierVolume>(true).ToList();
                    var matchedMods = new List<SdfModifierVolume>();
                    var unusedMods = new List<SdfModifierVolume>(existingMods);

                    for (int i = 0; i < modifierVolumes.Count; i++)
                    {
                        var snap = modifierVolumes[i];
                        SdfModifierVolume targetMod = unusedMods.FirstOrDefault(em => em != null && em.gameObject.name == snap.volumeName);
                        if (targetMod != null)
                        {
                            unusedMods.Remove(targetMod);
                        }
                        else if (unusedMods.Count > 0)
                        {
                            targetMod = unusedMods[0];
                            unusedMods.RemoveAt(0);
                        }
                        else
                        {
                            var newGo = new GameObject(snap.volumeName);
                            newGo.transform.SetParent(controller.transform, false);
                            targetMod = newGo.AddComponent<SdfModifierVolume>();
#if UNITY_EDITOR
                            UnityEditor.Undo.RegisterCreatedObjectUndo(newGo, "Create Sdf Modifier Volume");
#endif
                        }

#if UNITY_EDITOR
                        UnityEditor.Undo.RecordObject(targetMod.gameObject, "Configure Modifier Volume");
                        UnityEditor.Undo.RecordObject(targetMod, "Configure Modifier Volume");
#endif
                        targetMod.gameObject.name = snap.volumeName;
                        targetMod.modifierType = snap.modifierType;
                        targetMod.scope = snap.scope;
                        targetMod.transform.localPosition = snap.localPosition;
                        targetMod.transform.localEulerAngles = snap.localEulerAngles;
                        targetMod.transform.localScale = snap.localScale;
                        targetMod.size = snap.size;
                        targetMod.falloff = snap.falloff;
                        targetMod.strength = snap.strength;
                        targetMod.frequency = snap.frequency;
                        targetMod.gizmoColor = snap.gizmoColor;

                        matchedMods.Add(targetMod);
                    }

                    foreach (var excess in unusedMods)
                    {
                        if (excess != null)
                        {
#if UNITY_EDITOR
                            UnityEditor.Undo.DestroyObjectImmediate(excess.gameObject);
#else
                            UnityEngine.Object.Destroy(excess.gameObject);
#endif
                        }
                    }

                    for (int i = 0; i < matchedMods.Count; i++)
                    {
                        var snap = modifierVolumes[i];
                        int targetSibling = (snap.siblingIndex >= 0) ? snap.siblingIndex : (nodes != null ? nodes.Count + i : i);
                        matchedMods[i].transform.SetSiblingIndex(targetSibling);
                    }

                    controller.modifierVolumes = matchedMods;
                }

                controller.SynchronizeHierarchyFromNodesList();

                var rend = controller.GetComponent<Renderer>();
                if (rend != null && rend.sharedMaterial != null)
                {
                    var mat = rend.sharedMaterial;
#if UNITY_EDITOR
                    UnityEditor.Undo.RecordObject(mat, "Apply Material Texture Settings");
#endif
                    if (albedoTexture != null) mat.SetTexture("_MainTex", albedoTexture);
                    mat.SetFloat("_TextureScale", textureScale);
                    mat.SetFloat("_TextureBlend", textureBlend);
                    mat.SetFloat("_TriplanarSharpness", triplanarSharpness);
                    mat.SetFloat("_EnableBumpMap", enableBumpMap ? 1.0f : 0.0f);
                    if (bumpMap != null) mat.SetTexture("_BumpMap", bumpMap);
                    mat.SetFloat("_BumpScale", bumpScale);
                    mat.SetFloat("_EnableMatCap", enableMatCap ? 1.0f : 0.0f);
                    if (matCapTexture != null) mat.SetTexture("_MatCapTex", matCapTexture);
                    mat.SetFloat("_MatCapBlend", matCapBlend);
                }
            }

            if (voxelizer != null)
            {
#if UNITY_EDITOR
                UnityEditor.Undo.RecordObject(voxelizer.transform, "Apply Volume Settings");
                UnityEditor.Undo.RecordObject(voxelizer, "Apply Volume Settings");
#endif
                voxelizer.transform.localScale = Vector3.one;
                voxelizer.volumeSize = volumeSize;
                voxelizer.textureResolution = textureResolution;
            }

            // 2. Canali del DataStreamRegistry
            if (DataStreamRegistry.Instance != null && channels != null && channels.Count > 0)
            {
#if UNITY_EDITOR
                UnityEditor.Undo.RecordObject(DataStreamRegistry.Instance, "Apply Channels");
#endif
                DataStreamRegistry.Instance.channels = channels.Select(ch => new DataStreamChannel
                {
                    id = ch.id,
                    sourceType = ch.sourceType,
                    filterType = ch.filterType,
                    sourceChannelId = ch.sourceChannelId,
                    inputRange = ch.inputRange,
                    outputRange = ch.outputRange,
                    smoothing = ch.smoothing,
                    isMuted = ch.isMuted,
                    isSolo = ch.isSolo
                }).ToList();
                DataStreamRegistry.Instance.RefreshRegistry();
            }

            // 3. Ripristino Binders
            // Elimina i vecchi binder
            var oldFloatBinders = UnityEngine.Object.FindObjectsByType<DataStreamPropertyBinder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var b in oldFloatBinders)
            {
#if UNITY_EDITOR
                UnityEditor.Undo.DestroyObjectImmediate(b);
#else
                UnityEngine.Object.Destroy(b);
#endif
            }

            var oldColorBinders = UnityEngine.Object.FindObjectsByType<DataStreamColorBinder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var b in oldColorBinders)
            {
#if UNITY_EDITOR
                UnityEditor.Undo.DestroyObjectImmediate(b);
#else
                UnityEngine.Object.Destroy(b);
#endif
            }

            // Ricrea Property Binders
            if (propertyBinders != null)
            {
                foreach (var pbSnap in propertyBinders)
                {
                    var targetGo = GameObject.Find(pbSnap.targetGameObjectName);
                    if (targetGo == null)
                    {
                        if (!string.IsNullOrEmpty(pbSnap.targetComponentType) && pbSnap.targetComponentType.Contains("VisualEffect"))
                        {
                            var vfx = UnityEngine.Object.FindAnyObjectByType<UnityEngine.VFX.VisualEffect>();
                            if (vfx != null) targetGo = vfx.gameObject;
                        }
                        if (targetGo == null && controller != null) targetGo = controller.gameObject;
                    }

                    if (targetGo != null)
                    {
                        Component targetComp = null;
                        if (!string.IsNullOrEmpty(pbSnap.targetComponentType))
                        {
                            targetComp = targetGo.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType().FullName == pbSnap.targetComponentType);
                        }
                        if (targetComp == null)
                        {
                            targetComp = targetGo.GetComponent<UnityEngine.VFX.VisualEffect>() as Component
                                      ?? targetGo.GetComponent<Renderer>() as Component
                                      ?? targetGo.transform;
                        }

#if UNITY_EDITOR
                        var newBinder = UnityEditor.Undo.AddComponent<DataStreamPropertyBinder>(targetGo);
#else
                        var newBinder = targetGo.AddComponent<DataStreamPropertyBinder>();
#endif
                        newBinder.channelId = pbSnap.channelId;
                        newBinder.targetComponent = targetComp;
                        newBinder.targetPropertyName = pbSnap.targetPropertyName;
                        newBinder.weightMultiplier = pbSnap.weightMultiplier;
                        newBinder.useLocalRemap = pbSnap.useLocalRemap;
                        newBinder.localRemap = pbSnap.localRemap;
                        newBinder.InitializeBindings();
                    }
                }
            }

            // Ricrea Color Binders
            if (colorBinders != null)
            {
                foreach (var cbSnap in colorBinders)
                {
                    var targetGo = GameObject.Find(cbSnap.targetGameObjectName);
                    if (targetGo == null)
                    {
                        var vfx = UnityEngine.Object.FindAnyObjectByType<UnityEngine.VFX.VisualEffect>();
                        if (vfx != null && (cbSnap.targetPropertyName == "Color" || (!string.IsNullOrEmpty(cbSnap.targetComponentType) && cbSnap.targetComponentType.Contains("VisualEffect"))))
                        {
                            targetGo = vfx.gameObject;
                        }
                        else if (controller != null)
                        {
                            targetGo = controller.gameObject;
                        }
                    }

                    if (targetGo != null)
                    {
                        Component targetComp = null;
                        if (!string.IsNullOrEmpty(cbSnap.targetComponentType))
                        {
                            targetComp = targetGo.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType().FullName == cbSnap.targetComponentType);
                        }
                        if (targetComp == null)
                        {
                            targetComp = targetGo.GetComponent<UnityEngine.VFX.VisualEffect>() as Component
                                      ?? targetGo.GetComponent<Renderer>() as Component;
                        }

#if UNITY_EDITOR
                        var newColorBinder = UnityEditor.Undo.AddComponent<DataStreamColorBinder>(targetGo);
#else
                        var newColorBinder = targetGo.AddComponent<DataStreamColorBinder>();
#endif
                        newColorBinder.channelId = cbSnap.channelId;
                        newColorBinder.targetComponent = targetComp;
                        newColorBinder.targetPropertyName = cbSnap.targetPropertyName;
                        newColorBinder.colorGradient = cbSnap.colorGradient;
                        newColorBinder.InitializeBindings();
                    }
                }
            }

#if UNITY_EDITOR
            if (controller != null) UnityEditor.EditorUtility.SetDirty(controller);
            if (voxelizer != null) UnityEditor.EditorUtility.SetDirty(voxelizer);
            if (DataStreamRegistry.Instance != null) UnityEditor.EditorUtility.SetDirty(DataStreamRegistry.Instance);
#endif

            // Solo se il template non specificava una dimensione valida, calcolala con FitToNodes
            if (voxelizer != null && voxelizer.volumeSize < 0.1f)
            {
                voxelizer.FitToNodes();
            }

            Debug.Log($"[SdfSceneTemplate] Template '{name}' applicato con successo alla scena!");
        }
    }
}
