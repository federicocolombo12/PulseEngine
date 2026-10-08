using System.Collections.Generic;
using UnityEngine;
using GenericDataStreaming;

namespace MedicalXR.RayMarching
{
    /// <summary>
    /// Master controller per il raymarching multi-nodo. Raccoglie tutti i nodi SdfNode
    /// attivi, calcola le loro matrici di trasformazione locali, e invia gli array
    /// delle forme, dimensioni, e operazioni booleane allo shader.
    /// Completamente slegato (decoupled) dai sensori fisici.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Renderer))]
    public class SdfGroupController : MonoBehaviour
    {
        [Header("SDF Blending Settings")]
        [Range(0.001f, 2f)]
        [Tooltip("Morbidezza k globale per le operazioni di Smooth CSG tra i nodi. 0 = unione netta, >0 = unione morbida.")]
        public float globalBlendSoftness = 0.2f;

        [Header("XR Rendering Optimization")]
        [Tooltip("Se disattivato, spegne il rendering della mesh solida (Pixel-Fill Decoupling) mantenendo attivo il voxelizer per il VFX Graph.")]
        public bool renderSolidMesh = false;

        [Header("Group Nodes & Modifiers Settings")]
        [Tooltip("Lista manuale dei nodi. Se vuota, cercherà tutti i nodi nei figli.")]
        public List<SdfNode> nodes = new List<SdfNode>();
        [Tooltip("Lista manuale dei volumi modificatori di dominio (MudBun style). Se vuota, cercherà nei figli.")]
        public List<SdfModifierVolume> modifierVolumes = new List<SdfModifierVolume>();

        [Header("Pulse Animation Settings")]
        [Tooltip("Velocità con cui l'impulso torna a zero.")]
        public float pulseDecaySpeed = 4.0f;
        [Tooltip("Moltiplicatore visivo per l'espansione del battito sui nodi.")]
        public float pulseMultiplier = 1.0f;
        [Tooltip("Usa questo parametro per pilotare il battito tramite un segnale continuo (es. SineWavePulse) invece che a trigger.")]
        public float continuousPulse = 0.0f;
        [Tooltip("Nome del parametro shader float che riceve il valore dell'impulso.")]
        public string shaderPulseParam = "_RWavePulse";

        [Tooltip("Flag pubblico impostabile da bridge esterni per avviare l'impulso.")]
        public bool pulseTriggered = false;

        // Limite massimo di elementi supportati dallo shader
        private const int MAX_NODES = 16;

        private Material targetMaterial;
        private float pulseValue = 0.0f;

        private struct ElementItem
        {
            public Transform transform;
            public SdfNode node;
            public SdfModifierVolume modifier;
        }
        private List<ElementItem> currentElements = new List<ElementItem>();

        // Array per la GPU
        private Matrix4x4[] nodeMatrices = new Matrix4x4[MAX_NODES];
        private Matrix4x4[] nodeInvMatrices = new Matrix4x4[MAX_NODES];
        private Vector4[] nodeSizes = new Vector4[MAX_NODES];
        private Vector4[] nodeParams = new Vector4[MAX_NODES];
        private float[] nodeShapeTypes = new float[MAX_NODES];
        private float[] nodeCombineOps = new float[MAX_NODES];
        private float[] nodeBlendSoftnesses = new float[MAX_NODES];
        private float[] nodePulseIntensities = new float[MAX_NODES];
        private Vector4[] nodeColors = new Vector4[MAX_NODES];
        private Texture3D dummyTexture3D;

        // Proprietà pubbliche per l'accesso esterno (es. SdfVolumeVoxelizer)
        public int ActiveNodeCount => Mathf.Min(currentElements.Count, MAX_NODES);
        public Matrix4x4[] NodeMatrices => nodeMatrices;
        public Matrix4x4[] NodeInvMatrices => nodeInvMatrices;
        public Vector4[] NodeSizes => nodeSizes;
        public Vector4[] NodeParams => nodeParams;
        public float[] NodeShapeTypes => nodeShapeTypes;
        public float[] NodeCombineOps => nodeCombineOps;
        public float[] NodeBlendSoftnesses => nodeBlendSoftnesses;
        public float[] NodePulseIntensities => nodePulseIntensities;
        public Vector4[] NodeColors => nodeColors;
        public float PulseValue => pulseValue;
        public float CurrentBlendSoftness => globalBlendSoftness;

        public Texture3D GetElementBakedTexture(int index)
        {
            if (index >= 0 && index < currentElements.Count)
            {
                var elem = currentElements[index];
                if (elem.node != null && elem.node.shapeType == SdfShapeType.BakedTexture3D)
                {
                    return elem.node.bakedSdfTexture;
                }
            }
            return null;
        }

        private Renderer cachedRenderer;
        private MeshFilter cachedMeshFilter;

        void OnEnable()
        {
            cachedMeshFilter = GetComponent<MeshFilter>();
            if (cachedMeshFilter != null)
            {
                if (cachedMeshFilter.sharedMesh == null || cachedMeshFilter.sharedMesh.name != "SdfVolumeBoundsMesh")
                {
                    var tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    var meshInstance = Instantiate(tempCube.GetComponent<MeshFilter>().sharedMesh);
                    meshInstance.name = "SdfVolumeBoundsMesh";
                    // Imposta un bounding box generoso iniziale per prevenire il frustum culling
                    meshInstance.bounds = new Bounds(Vector3.zero, Vector3.one * 10.0f);
                    cachedMeshFilter.sharedMesh = meshInstance;
                    DestroyImmediate(tempCube);
                }
            }

            cachedRenderer = GetComponent<Renderer>();
            if (cachedRenderer != null)
            {
                cachedRenderer.allowOcclusionWhenDynamic = false;
#if UNITY_EDITOR
                var so = new UnityEditor.SerializedObject(cachedRenderer);
                var smcProp = so.FindProperty("m_SmallMeshCulling");
                if (smcProp != null && smcProp.boolValue)
                {
                    smcProp.boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                var dynOccProp = so.FindProperty("m_DynamicOccludee");
                if (dynOccProp != null && dynOccProp.boolValue)
                {
                    dynOccProp.boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
#endif
                EnsureMaterial();
                cachedRenderer.enabled = renderSolidMesh;
            }
            else
            {
                Debug.LogError("[SdfGroupController] Nessun Renderer trovato su questo GameObject.");
            }

            // Se la lista dei nodi è vuota, cerca nei figli
            if (nodes == null || nodes.Count == 0)
            {
                nodes = new List<SdfNode>(GetComponentsInChildren<SdfNode>());
            }

            // Se la lista dei volumi modificatori è vuota, cerca nei figli
            if (modifierVolumes == null || modifierVolumes.Count == 0)
            {
                modifierVolumes = new List<SdfModifierVolume>(GetComponentsInChildren<SdfModifierVolume>());
            }
            
            EnsureDummyTexture();
        }

        void OnValidate()
        {
            if (renderSolidMesh)
            {
                EnsureMaterial();
            }
        }

        public void EnsureMaterial()
        {
            if (cachedRenderer == null) cachedRenderer = GetComponent<Renderer>();
            if (cachedRenderer == null) return;

            bool isBroken = cachedRenderer.sharedMaterial == null || 
                            cachedRenderer.sharedMaterial.shader == null || 
                            cachedRenderer.sharedMaterial.shader.name == "Hidden/InternalErrorShader";

            if (isBroken)
            {
#if UNITY_EDITOR
                var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Packages/com.federicocolombo.pulseengine/Runtime/VolumetricSDF/Materials/BioSignalSDF_Mat.mat");
                if (mat == null)
                {
                    mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Core/RayMarching/Material/BioSignalSDF_Mat.mat");
                }
                if (mat != null)
                {
                    cachedRenderer.sharedMaterial = mat;
                }
                else
                {
                    var shader = Shader.Find("MedicalXR/BioSignalSDF");
                    if (shader != null)
                    {
                        cachedRenderer.sharedMaterial = new Material(shader) { name = "BioSignalSDF_RuntimeMat" };
                    }
                }
#else
                var shader = Shader.Find("MedicalXR/BioSignalSDF");
                if (shader != null)
                {
                    cachedRenderer.sharedMaterial = new Material(shader) { name = "BioSignalSDF_RuntimeMat" };
                }
#endif
            }

            if (targetMaterial == null || targetMaterial.shader == null || targetMaterial.shader.name == "Hidden/InternalErrorShader")
            {
                targetMaterial = Application.isPlaying ? cachedRenderer.material : cachedRenderer.sharedMaterial;
            }
        }

        private void EnsureDummyTexture()
        {
            if (dummyTexture3D == null)
            {
                dummyTexture3D = new Texture3D(1, 1, 1, TextureFormat.Alpha8, false);
                dummyTexture3D.SetPixels(new Color[] { new Color(1, 1, 1, 1) });
                dummyTexture3D.Apply();
            }
        }

        void Update()
        {
            // 1. Gestione dell'impulso generico (Pulse)
            if (pulseTriggered)
            {
                pulseValue = 1.0f;
                pulseTriggered = false; // Consuma il trigger
            }
            else
            {
                pulseValue = Mathf.Max(0.0f, pulseValue - Time.deltaTime * pulseDecaySpeed);
            }

            // 2. Raccolta e fusione gerarchica di nodi e volumi modificatori
            nodes.RemoveAll(item => item == null);
            modifierVolumes.RemoveAll(item => item == null);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var childNodes = GetComponentsInChildren<SdfNode>(true);
                foreach (var cn in childNodes)
                {
                    if (cn != null && !nodes.Contains(cn))
                    {
                        nodes.Add(cn);
                    }
                }
                var childMods = GetComponentsInChildren<SdfModifierVolume>(true);
                foreach (var cm in childMods)
                {
                    if (cm != null && !modifierVolumes.Contains(cm))
                    {
                        modifierVolumes.Add(cm);
                    }
                }
            }
#endif

            currentElements.Clear();
            foreach (var n in nodes)
            {
                if (n.gameObject.activeInHierarchy)
                {
                    currentElements.Add(new ElementItem { transform = n.transform, node = n, modifier = null });
                }
            }
            foreach (var m in modifierVolumes)
            {
                if (m.gameObject.activeInHierarchy)
                {
                    currentElements.Add(new ElementItem { transform = m.transform, node = null, modifier = m });
                }
            }

            // Ordina rigorosamente in base all'ordine gerarchico top-to-bottom dei GameObject in Unity (Sibling Index)
            currentElements.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            int activeElementCount = Mathf.Min(currentElements.Count, MAX_NODES);

            for (int i = 0; i < MAX_NODES; i++)
            {
                if (i < activeElementCount)
                {
                    var elem = currentElements[i];
                    Transform elemTransform = elem.transform;

                    // Matrice Rigida Ortonormale (Posizione e Rotazione pure nello spazio locale del controller)
                    Vector3 relPos = transform.InverseTransformPoint(elemTransform.position);
                    Quaternion relRot = Quaternion.Inverse(transform.rotation) * elemTransform.rotation;
                    Matrix4x4 localToController = Matrix4x4.TRS(relPos, relRot, Vector3.one);

                    nodeMatrices[i] = localToController.inverse;
                    nodeInvMatrices[i] = localToController;

                    if (elem.node != null)
                    {
                        var node = elem.node;
                        Vector3 s = node.transform.localScale;
                        Vector3 scaledSize = new Vector3(
                            Mathf.Max(0.0001f, Mathf.Abs(node.size.x * s.x)),
                            Mathf.Max(0.0001f, Mathf.Abs(node.size.y * s.y)),
                            Mathf.Max(0.0001f, Mathf.Abs(node.size.z * s.z))
                        );
                        nodeSizes[i] = new Vector4(scaledSize.x, scaledSize.y, scaledSize.z, 0f);
                        nodeShapeTypes[i] = (float)node.shapeType;
                        nodeCombineOps[i] = (float)node.combineOp;
                        nodeBlendSoftnesses[i] = Mathf.Max(0f, node.blendSoftness);

                        node.currentPulse = (pulseValue + continuousPulse) * node.pulseSensitivity;
                        nodePulseIntensities[i] = node.currentPulse * pulseMultiplier;
                        nodeColors[i] = node.shapeColor;

                        nodeParams[i] = new Vector4(
                            nodeShapeTypes[i],
                            nodeCombineOps[i],
                            nodePulseIntensities[i],
                            nodeBlendSoftnesses[i]
                        );
                    }
                    else if (elem.modifier != null)
                    {
                        var mod = elem.modifier;
                        Vector3 s = mod.transform.localScale;
                        Vector3 scaledSize = new Vector3(
                            Mathf.Max(0.0001f, Mathf.Abs(mod.size.x * s.x)),
                            Mathf.Max(0.0001f, Mathf.Abs(mod.size.y * s.y)),
                            Mathf.Max(0.0001f, Mathf.Abs(mod.size.z * s.z))
                        );
                        nodeSizes[i] = new Vector4(scaledSize.x, scaledSize.y, scaledSize.z, 0f);
                        
                        float scopeOffset = (mod.scope == SdfModifierScope.AllNodesInVolume) ? 10.0f : 0.0f;
                        float modTypeVal = 100.0f + scopeOffset + (float)mod.modifierType;
                        nodeShapeTypes[i] = modTypeVal;
                        nodeCombineOps[i] = 0f;
                        nodeBlendSoftnesses[i] = 0f;
                        nodePulseIntensities[i] = 0f;
                        nodeColors[i] = mod.gizmoColor;

                        nodeParams[i] = new Vector4(
                            modTypeVal,
                            mod.strength,
                            mod.frequency,
                            Mathf.Max(0.001f, mod.falloff)
                        );
                    }
                }
                else
                {
                    // Disattiva gli slot extra
                    nodeMatrices[i] = Matrix4x4.identity;
                    nodeInvMatrices[i] = Matrix4x4.identity;
                    nodeSizes[i] = Vector4.zero;
                    nodeParams[i] = new Vector4(-1.0f, 0.0f, 0.0f, 0.0f);
                    nodeShapeTypes[i] = -1.0f; 
                    nodeCombineOps[i] = 0.0f;
                    nodeBlendSoftnesses[i] = 0.0f;
                    nodePulseIntensities[i] = 0f;
                    nodeColors[i] = Vector4.zero;
                }
            }

            // Sincronizza stato mesh solida (Pixel-Fill Decoupling per XR)
            if (cachedRenderer != null)
            {
                if (cachedRenderer.enabled != renderSolidMesh)
                {
                    cachedRenderer.enabled = renderSolidMesh;
                }
                if (renderSolidMesh && (targetMaterial == null || cachedRenderer.sharedMaterial == null))
                {
                    EnsureMaterial();
                }
            }

            // 3. Invio degli array allo shader
            if (targetMaterial != null)
            {
                var voxelizer = GetComponent<SdfVolumeVoxelizer>();
                Vector3 volSize = voxelizer != null ? voxelizer.VolumeSize3D : Vector3.one * 2.0f;

                // Assicura che il bounding box CPU della mesh inglobi completamente il volume espanso GPU
                // con un margine di sicurezza di almeno 6 metri per evitare che Unity applichi il frustum culling a distanza
                if (cachedMeshFilter == null) cachedMeshFilter = GetComponent<MeshFilter>();
                if (cachedMeshFilter != null && cachedMeshFilter.sharedMesh != null)
                {
                    Vector3 safeBoundsSize = Vector3.Max(volSize * 3.0f, Vector3.one * 6.0f);
                    if (cachedMeshFilter.sharedMesh.bounds.size != safeBoundsSize)
                    {
                        cachedMeshFilter.sharedMesh.bounds = new Bounds(Vector3.zero, safeBoundsSize);
                    }
                }

                targetMaterial.SetVector("_VolumeSize", volSize);
                targetMaterial.SetInt("_NodeCount", activeElementCount);
                targetMaterial.SetMatrixArray("_NodeMatrices", nodeMatrices);
                targetMaterial.SetMatrixArray("_NodeInvMatrices", nodeInvMatrices);
                targetMaterial.SetVectorArray("_NodeSizes", nodeSizes);
                targetMaterial.SetVectorArray("_NodeParams", nodeParams);
                targetMaterial.SetFloatArray("_NodeShapeTypes", nodeShapeTypes);
                targetMaterial.SetFloatArray("_NodeCombineOps", nodeCombineOps);
                targetMaterial.SetFloatArray("_NodeBlendSoftnesses", nodeBlendSoftnesses);
                targetMaterial.SetFloat("_GlobalBlendSoftness", globalBlendSoftness);
                targetMaterial.SetFloatArray("_NodePulseIntensity", nodePulseIntensities);
                targetMaterial.SetVectorArray("_NodeColors", nodeColors);

                EnsureDummyTexture();
                for (int i = 0; i < MAX_NODES; i++)
                {
                    var tex = GetElementBakedTexture(i);
                    targetMaterial.SetTexture($"_NodeTex{i}", tex != null ? tex : dummyTexture3D);
                }

                // Imposta il valore dell'impulso sul parametro dello shader
                if (!string.IsNullOrEmpty(shaderPulseParam))
                {
                    targetMaterial.SetFloat(shaderPulseParam, pulseValue);
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
            {
                Update();
            }
        }
#endif

        public void TriggerPulse()
        {
            pulseTriggered = true;
        }

        public SdfModifierVolume AddModifierVolume(SdfModifierType type = SdfModifierType.Twist, SdfModifierScope scope = SdfModifierScope.AllNodesInVolume)
        {
            GameObject newModGo = new GameObject($"SdfModifierVolume_{type}_{modifierVolumes.Count + 1}");
            newModGo.transform.SetParent(transform);
            newModGo.transform.localPosition = Vector3.zero;
            newModGo.transform.localRotation = Quaternion.identity;
            newModGo.transform.localScale = Vector3.one;

            SdfModifierVolume mod = newModGo.AddComponent<SdfModifierVolume>();
            mod.modifierType = type;
            mod.scope = scope;
            mod.size = new Vector3(0.25f, 0.25f, 0.25f);
            mod.falloff = 0.05f;
            mod.strength = 1.0f;
            mod.frequency = 5.0f;

            if (!modifierVolumes.Contains(mod))
            {
                modifierVolumes.Add(mod);
            }
            return mod;
        }

        public SdfNode AddNode(SdfShapeType shapeType = SdfShapeType.Sphere)
        {
            GameObject newNodeGo = new GameObject($"SdfNode_{shapeType}_{nodes.Count + 1}");
            newNodeGo.transform.SetParent(transform);
            newNodeGo.transform.localPosition = Vector3.zero;
            newNodeGo.transform.localRotation = Quaternion.identity;
            newNodeGo.transform.localScale = Vector3.one;

            SdfNode node = newNodeGo.AddComponent<SdfNode>();
            node.shapeType = shapeType;

            switch (shapeType)
            {
                case SdfShapeType.Sphere:
                    node.size = new Vector3(0.12f, 0f, 0f);
                    break;
                case SdfShapeType.Box:
                    node.size = new Vector3(0.1f, 0.1f, 0.1f);
                    break;
                case SdfShapeType.Torus:
                    node.size = new Vector3(0.15f, 0.04f, 0f);
                    break;
                case SdfShapeType.Capsule:
                case SdfShapeType.Cylinder:
                    node.size = new Vector3(0.08f, 0.25f, 0f);
                    break;
            }

            if (!nodes.Contains(node))
            {
                nodes.Add(node);
            }
            return node;
        }

        void OnDestroy()
        {
            if (Application.isPlaying && targetMaterial != null)
            {
                Destroy(targetMaterial);
            }
            if (dummyTexture3D != null)
            {
                if (Application.isPlaying) Destroy(dummyTexture3D);
                else DestroyImmediate(dummyTexture3D);
                dummyTexture3D = null;
            }
        }

        public void ApplyTemplate(SdfEffectTemplate template)
        {
            if (template == null) return;

            globalBlendSoftness = template.globalBlendSoftness;

            // 2. Applicazione dei colori e operatori ai nodi in base al template
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] == null) continue;
                
                // Nodo primario (indice 0) vs Nodi secondari
                nodes[i].shapeColor = (i == 0) ? template.primaryColor : template.secondaryColor;
                
                // Ai nodi secondari applichiamo l'operazione dominante (es. Subtraction o SmoothUnion)
                if (i > 0)
                {
                    nodes[i].combineOp = template.primaryCombineOp;
                }
            }

            // 3. Gestione dei DataStreamPropertyBinder dinamici
            // Trova il VFX Graph target tramite il voxelizer (se esiste)
            SdfVolumeVoxelizer voxelizer = GetComponent<SdfVolumeVoxelizer>();
            UnityEngine.VFX.VisualEffect vfx = voxelizer != null ? voxelizer.targetVfxGraph : null;
            if (vfx == null) vfx = GetComponentInChildren<UnityEngine.VFX.VisualEffect>();
            if (vfx == null) vfx = GetComponent<UnityEngine.VFX.VisualEffect>();
            
            GameObject binderTargetGo = vfx != null ? vfx.gameObject : gameObject;

            // Rimuoviamo i binder globalmente dalla scena per evitare duplicati
            DataStreamPropertyBinder[] allBinders = Object.FindObjectsByType<DataStreamPropertyBinder>(FindObjectsSortMode.None);
            foreach (var binder in allBinders)
            {
                if (Application.isPlaying) Destroy(binder);
                else DestroyImmediate(binder);
            }

            // Aggiungiamo i nuovi binder basati sul template
            if (template.dataBindings != null)
            {
                Renderer rend = GetComponent<Renderer>();
                
                foreach (var bindingRule in template.dataBindings)
                {
                    GenericDataStreaming.DataStreamPropertyBinder newBinder = binderTargetGo.AddComponent<GenericDataStreaming.DataStreamPropertyBinder>();
                    newBinder.channelId = bindingRule.channelId;
                    newBinder.targetPropertyName = bindingRule.targetPropertyName;
                    
                    if (bindingRule.targetType == GenericDataStreaming.BindingTargetType.MaterialProperty)
                    {
                        newBinder.targetComponent = rend;
                    }
                    else if (bindingRule.targetType == GenericDataStreaming.BindingTargetType.VfxProperty || bindingRule.targetType == GenericDataStreaming.BindingTargetType.VfxEvent)
                    {
                        newBinder.targetComponent = vfx;
                    }
                    else 
                    {
                        newBinder.targetComponent = this;
                    }
                    
                    newBinder.InitializeBindings();
                }
            }

            // Siccome siamo in EditMode potremmo dover segnare la scena come "da salvare"
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            foreach (var node in nodes)
            {
                if (node != null) UnityEditor.EditorUtility.SetDirty(node);
            }
#endif
            
            // Adatta automaticamente il volume
            if (voxelizer != null)
            {
                voxelizer.FitToNodes();
            }
        }
    }
}
