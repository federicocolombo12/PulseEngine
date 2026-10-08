using UnityEngine;
using UnityEngine.VFX;

namespace MedicalXR.RayMarching
{
    /// <summary>
    /// Gestisce la voxelizzazione in tempo reale dell'SDF combinato (CSG Blended) su GPU.
    /// Esegue un Compute Shader ad ogni Update per generare una RenderTexture 3D,
    /// e la inietta nel VFX Graph per gestire le collisioni dinamiche delle particelle.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SdfGroupController))]
    public class SdfVolumeVoxelizer : MonoBehaviour
    {
        [Header("Shader References")]
        [Tooltip("Il Compute Shader per la voxelizzazione dell'SDF.")]
        public ComputeShader voxelizerShader;

        [Header("Volume Configuration")]
        [Range(16, 256)]
        [Tooltip("Risoluzione del volume 3D (multiplo di 4 consigliato, es. 64, 128 o 192).")]
        public int textureResolution = 32;

        [Tooltip("Risoluzione massima ammessa per proteggere la VRAM (default 192 per PC/Editor, riducibile per Quest standalone).")]
        public int maxResolutionLimit = 192;

        [Tooltip("Dimensione cubica reale del volume in metri (local space) rappresentata dalla texture.")]
        public float volumeSize = 2.0f;

        // Proprietà helper per compatibilità Vector3
        public Vector3 VolumeSize3D => new Vector3(volumeSize, volumeSize, volumeSize);

        [Tooltip("Densità desiderata in voxel per metro. Adatta automaticamente textureResolution in base alla dimensione del volume.")]
        public float targetVoxelDensity = 32f;

        [Header("VFX Graph Binding")]
        [Tooltip("Il VFX Graph target che deve ricevere la texture 3D.")]
        public VisualEffect targetVfxGraph;

        [Tooltip("Nome del parametro Texture3D nel VFX Graph per il volume SDF.")]
        public string vfxTextureParamName = "SdfVolumeTexture";

        [Tooltip("Nome del parametro Vector3 nel VFX Graph per la dimensione del volume.")]
        public string vfxVolumeSizeParamName = "SdfVolumeSize";

        [Tooltip("Nome del parametro Vector3 nel VFX Graph per il centro del volume.")]
        public string vfxVolumeCenterParamName = "SdfVolumeCenter";

        [Tooltip("Nome del parametro Matrix4x4 nel VFX Graph per la matrice World-to-Local del volume.")]
        public string vfxTransformParamName = "SdfVolumeTransform";

        private SdfGroupController sdfController;
        private RenderTexture sdfVolumeTex;
        private int kernelIndex;
        
        // Costante per il numero massimo di nodi
        private const int MAX_NODES = 16;
        private bool hasLoggedVfxBindings = false;

        private float lastVolumeSize;

        // Cache degli ID dei parametri per ottimizzare le performance ed evitare allocazioni GC
        private static readonly int ResolutionId = Shader.PropertyToID("_VolumeResolution");
        private static readonly int SizeId = Shader.PropertyToID("_VolumeSize");
        private static readonly int ResultId = Shader.PropertyToID("Result");
        private static readonly int NodeCountId = Shader.PropertyToID("_NodeCount");
        private static readonly int NodeMatricesId = Shader.PropertyToID("_NodeMatrices");
        private static readonly int NodeInvMatricesId = Shader.PropertyToID("_NodeInvMatrices");
        private static readonly int NodeSizesId = Shader.PropertyToID("_NodeSizes");
        private static readonly int NodeParamsId = Shader.PropertyToID("_NodeParams");
        private static readonly int NodeColorsId = Shader.PropertyToID("_NodeColors");
        private static readonly int GlobalBlendSoftnessId = Shader.PropertyToID("_GlobalBlendSoftness");

        private Texture3D dummyTexture3D;

        public RenderTexture VolumeTexture => sdfVolumeTex;


        void OnEnable()
        {
            sdfController = GetComponent<SdfGroupController>();
            hasLoggedVfxBindings = false;
            lastVolumeSize = volumeSize;
            
            if (voxelizerShader != null)
            {
                kernelIndex = voxelizerShader.FindKernel("CSMain");
            }
            else
            {
                Debug.LogWarning("[SdfVolumeVoxelizer] Compute Shader non assegnato nell'Ispettore.", this);
            }

            EnsureDummyTexture();
            CreateVolumeTexture();
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

        void OnDisable()
        {
            ReleaseVolumeTexture();
        }

        void OnDestroy()
        {
            ReleaseVolumeTexture();
            if (dummyTexture3D != null)
            {
                if (Application.isPlaying) Destroy(dummyTexture3D);
                else DestroyImmediate(dummyTexture3D);
                dummyTexture3D = null;
            }
        }

        void CreateVolumeTexture()
        {
            ReleaseVolumeTexture();

            // Creiamo una RenderTexture 3D. Usiamo UnityEngine.Rendering.GraphicsFormat.R16G16B16A16_SFloat (16-bit floating point per canale in ordine RGBA)
            // in modo da contenere la distanza nel canale R e il colore RGB nei canali G, B, A.
            sdfVolumeTex = new RenderTexture(textureResolution, textureResolution, 0, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat);
            sdfVolumeTex.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            sdfVolumeTex.volumeDepth = textureResolution;
            sdfVolumeTex.enableRandomWrite = true;
            sdfVolumeTex.filterMode = FilterMode.Bilinear;
            sdfVolumeTex.wrapMode = TextureWrapMode.Clamp;
            sdfVolumeTex.Create();
        }

        void ReleaseVolumeTexture()
        {
            if (sdfVolumeTex != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(sdfVolumeTex);
                }
                else
                {
                    DestroyImmediate(sdfVolumeTex);
                }
                sdfVolumeTex = null;
            }
        }

        void Update()
        {
            // Protegge i nodi figli: se SDF_Volume viene scalato con lo strumento di Unity,
            // trasferisci la scala uniforme su volumeSize e mantieni il Transform a (1,1,1) per non deformare i figli.
            if (transform.localScale != Vector3.one)
            {
                float avgScale = (transform.localScale.x + transform.localScale.y + transform.localScale.z) / 3.0f;
                volumeSize = Mathf.Max(0.1f, volumeSize * avgScale);
                transform.localScale = Vector3.one;
            }

            if (sdfController == null || voxelizerShader == null) return;

            if (kernelIndex < 0)
            {
                kernelIndex = voxelizerShader.FindKernel("CSMain");
            }

            EnsureDummyTexture();

            // Auto-Resolution Logic for constant density
            if (Mathf.Abs(volumeSize - lastVolumeSize) > 0.001f)
            {
                lastVolumeSize = volumeSize;
                int optimalRes = Mathf.RoundToInt((volumeSize * targetVoxelDensity) / 4f) * 4;
                textureResolution = Mathf.Clamp(optimalRes, 16, Mathf.Max(16, maxResolutionLimit));
            }

            // Rigenera la texture se la risoluzione viene modificata a runtime o in editor
            if (sdfVolumeTex == null || !sdfVolumeTex.IsCreated() || sdfVolumeTex.width != textureResolution)
            {
                CreateVolumeTexture();
            }

            // 2. Passaggio dei parametri uniform globali al Compute Shader
            Vector3 vol3D = VolumeSize3D;
            voxelizerShader.SetVector(ResolutionId, new Vector3(textureResolution, textureResolution, textureResolution));
            voxelizerShader.SetVector(SizeId, vol3D);
            voxelizerShader.SetFloat(GlobalBlendSoftnessId, sdfController.CurrentBlendSoftness);

            // 3. Invio degli array geometrici estratti dal controller SDF
            int activeNodeCount = sdfController.ActiveNodeCount;
            voxelizerShader.SetInt(NodeCountId, activeNodeCount);
            
            voxelizerShader.SetMatrixArray(NodeMatricesId, sdfController.NodeMatrices);
            voxelizerShader.SetMatrixArray(NodeInvMatricesId, sdfController.NodeInvMatrices);
            voxelizerShader.SetVectorArray(NodeSizesId, sdfController.NodeSizes);
            voxelizerShader.SetVectorArray(NodeParamsId, sdfController.NodeParams);
            voxelizerShader.SetVectorArray(NodeColorsId, sdfController.NodeColors);

            for (int i = 0; i < MAX_NODES; i++)
            {
                var tex = (activeNodeCount > 0) ? sdfController.GetElementBakedTexture(i) : null;
                voxelizerShader.SetTexture(kernelIndex, $"_NodeTex{i}", tex != null ? tex : dummyTexture3D);
            }

            // 4. Dispatch del Compute Shader
            voxelizerShader.SetTexture(kernelIndex, ResultId, sdfVolumeTex);
            
            // Poiché numthreads è [4, 4, 4], dividiamo la risoluzione per 4 arrotondando per eccesso
            int threadGroups = Mathf.CeilToInt((float)textureResolution / 4f);
            voxelizerShader.Dispatch(kernelIndex, threadGroups, threadGroups, threadGroups);

            // 5. Aggiornamento parametri sul VFX Graph
            if (targetVfxGraph != null)
            {
                bool hasTex = targetVfxGraph.HasTexture(vfxTextureParamName);
                bool hasSize = targetVfxGraph.HasVector3(vfxVolumeSizeParamName);
                bool hasCenter = targetVfxGraph.HasVector3(vfxVolumeCenterParamName);
                bool hasTransform = targetVfxGraph.HasMatrix4x4(vfxTransformParamName);

                if (!hasLoggedVfxBindings)
                {
                    hasLoggedVfxBindings = true;
                    UnityEngine.Debug.Log($"[SDF Voxelizer] VFX Graph Bindings: hasTex={hasTex}, hasSize={hasSize}, hasCenter={hasCenter}, hasTransform={hasTransform}");
                }

                if (hasTex)
                {
                    targetVfxGraph.SetTexture(vfxTextureParamName, sdfVolumeTex);
                }

                if (hasSize)
                {
                    targetVfxGraph.SetVector3(vfxVolumeSizeParamName, VolumeSize3D);
                }

                if (hasCenter)
                {
                    // Fornisce il centro in coordinate mondiali (World Space) del volume
                    targetVfxGraph.SetVector3(vfxVolumeCenterParamName, transform.position);
                }

                if (hasTransform)
                {
                    // Fornisce la matrice World-to-Local per convertire le posizioni delle particelle nello spazio locale dell'SDF
                    targetVfxGraph.SetMatrix4x4(vfxTransformParamName, transform.worldToLocalMatrix);
                }
            }
            else
            {
                if (!hasLoggedVfxBindings)
                {
                    hasLoggedVfxBindings = true;
                    UnityEngine.Debug.LogWarning("[SDF Voxelizer] targetVfxGraph is NULL!");
                }
            }
        }

        public void FitToNodes()
        {
            if (sdfController == null) sdfController = GetComponent<SdfGroupController>();
            if (sdfController != null && sdfController.nodes != null && sdfController.nodes.Count > 0)
            {
                float maxAbsX = 0f;
                float maxAbsY = 0f;
                float maxAbsZ = 0f;
                bool hasActiveNodes = false;

                foreach (var node in sdfController.nodes)
                {
                    if (node == null || !node.gameObject.activeInHierarchy) continue;

                    hasActiveNodes = true;

                    // Posizione e rotazione del nodo relative al volume
                    Vector3 nodeLocalPos = transform.InverseTransformPoint(node.transform.position);
                    Quaternion nodeLocalRot = Quaternion.Inverse(transform.rotation) * node.transform.rotation;

                    // Estrazione semi-estensioni reali della forma geometrica scalate dal Transform
                    Vector3 s = node.transform.localScale;
                    Vector3 scaledNodeSize = Vector3.Scale(node.size, s);
                    Vector3 halfExtents = Vector3.one * 0.1f;
                    switch (node.shapeType)
                    {
                        case SdfShapeType.Sphere:
                            halfExtents = Vector3.one * (node.size.x * Mathf.Max(s.x, Mathf.Max(s.y, s.z)));
                            break;
                        case SdfShapeType.Box:
                        case SdfShapeType.BakedTexture3D:
                            halfExtents = scaledNodeSize;
                            break;
                        case SdfShapeType.Torus:
                            halfExtents = new Vector3((node.size.x + node.size.y) * s.x, node.size.y * s.y, (node.size.x + node.size.y) * s.z);
                            break;
                        case SdfShapeType.Capsule:
                        case SdfShapeType.Cylinder:
                            halfExtents = new Vector3(node.size.x * s.x, node.size.y * 0.5f * s.y, node.size.x * s.z);
                            break;
                    }

                    // Considera l'espansione pulsante del battito cardiaco
                    float pulseMargin = node.pulseSensitivity * 0.08f;
                    float cyclicMargin = Mathf.Max(node.cyclicAmplitude.x, Mathf.Max(node.cyclicAmplitude.y, node.cyclicAmplitude.z));
                    halfExtents += Vector3.one * (pulseMargin + cyclicMargin);

                    // Calcola gli 8 vertici del bounding box del nodo nello spazio del volume
                    for (int sx = -1; sx <= 1; sx += 2)
                    {
                        for (int sy = -1; sy <= 1; sy += 2)
                        {
                            for (int sz = -1; sz <= 1; sz += 2)
                            {
                                Vector3 cornerLocal = Vector3.Scale(halfExtents, new Vector3(sx, sy, sz));
                                Vector3 cornerInVolume = nodeLocalPos + nodeLocalRot * cornerLocal;

                                maxAbsX = Mathf.Max(maxAbsX, Mathf.Abs(cornerInVolume.x));
                                maxAbsY = Mathf.Max(maxAbsY, Mathf.Abs(cornerInVolume.y));
                                maxAbsZ = Mathf.Max(maxAbsZ, Mathf.Abs(cornerInVolume.z));
                            }
                        }
                    }
                }

                if (hasActiveNodes)
                {
                    // Aggiunge margine per la morbidezza del blend CSG e l'orbita delle particelle VFX
                    float blendPadding = Mathf.Max(0.12f, sdfController.globalBlendSoftness * 0.5f);
                    float halfX = maxAbsX + blendPadding;
                    float halfY = maxAbsY + blendPadding;
                    float halfZ = maxAbsZ + blendPadding;

                    // Per preservare l'isotropia dei voxel e la coerenza con l'OrientedBox di VFX Graph
                    float maxHalf = Mathf.Max(halfX, Mathf.Max(halfY, halfZ));
                    float fullDim = Mathf.Max(maxHalf * 2.0f, 1.0f); // Minimo 1.0 metro per dare respiro alle particelle

                    volumeSize = fullDim;
                    transform.localScale = Vector3.one;
                    
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(this);
#endif
                    Debug.Log($"[SdfVolumeVoxelizer] Fit Bounds completato: volumeSize={volumeSize:F2}m (Raggio max: {maxHalf:F2}m). Risoluzione si auto-aggiornerà.");
                }
            }
        }

        #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Disegna il volume di voxelizzazione in arancione trasparente per facilitare il posizionamento dei VFX
            Vector3 vol3D = VolumeSize3D;
            Gizmos.color = new Color(0.9f, 0.5f, 0.1f, 0.4f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, vol3D);
            
            Gizmos.color = new Color(0.9f, 0.5f, 0.1f, 0.05f);
            Gizmos.DrawCube(Vector3.zero, vol3D);
        }
        #endif
    }
}
