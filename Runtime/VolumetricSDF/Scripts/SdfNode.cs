using UnityEngine;

namespace MedicalXR.RayMarching
{
    public enum SdfShapeType
    {
        Sphere = 0,
        Box = 1,
        Torus = 2,
        Capsule = 3,
        Cylinder = 4,
        BakedTexture3D = 5
    }

    public enum SdfCombineOp
    {
        SmoothUnion = 0,
        Union = 1,
        Subtraction = 2,
        Intersection = 3,
        SmoothSubtraction = 4,
        SmoothIntersection = 5
    }

    /// <summary>
    /// Rappresenta un singolo nodo geometrico (SDF) nello spazio.
    /// Consente di impostare la forma, le dimensioni e l'operazione di fusione booleana (CSG).
    /// </summary>
    [ExecuteAlways]
    public class SdfNode : MonoBehaviour
    {
        [Header("Shape Definition")]
        [Tooltip("La forma geometrica tridimensionale di questo nodo.")]
        public SdfShapeType shapeType = SdfShapeType.Sphere;

        [Tooltip("Dimensioni specifiche della forma:\n" +
                 "- Sphere: X = Raggio\n" +
                 "- Box/BakedTexture3D: XYZ = Semi-estensione (metà larghezza/altezza/profondità)\n" +
                 "- Torus: X = Raggio Maggiore, Y = Raggio Minore (spessore dell'anello)\n" +
                 "- Capsule: X = Raggio, Y = Altezza totale\n" +
                 "- Cylinder: X = Raggio, Y = Altezza totale")]
        public Vector3 size = new Vector3(0.12f, 0.12f, 0.12f);

        [Tooltip("La Texture3D pre-calcolata dell'SDF. Viene utilizzata solo se la forma è impostata su BakedTexture3D.")]
        public Texture3D bakedSdfTexture;

        [Tooltip("Moltiplicatore di scala uniforme dinamico. Utile per animare la dimensione tramite DataStreamPropertyBinder senza alterare la size di base.")]
        public float uniformScale = 1.0f;

        [Header("Blending & Combination")]
        [Tooltip("Come questo nodo si combina con la geometria dei nodi precedenti nella gerarchia.")]
        public SdfCombineOp combineOp = SdfCombineOp.SmoothUnion;

        [Range(0f, 1f)]
        [Tooltip("Morbidezza di fusione (raggio di smusso) specifica per questo nodo. Di default è 0.0 (taglio o unione netta). Se > 0 attiva la fusione smussata.")]
        public float blendSoftness = 0.0f;

        [Header("Bio-Signal Reactivity")]
        [Range(0f, 1f)]
        [Tooltip("Sensibilità di questo nodo all'impulso del battito cardiaco.")]
        public float pulseSensitivity = 1.0f;

        [Header("Visual Customization")]
        [Tooltip("Colore specifico di questa forma. Verrà trasmesso al VFX Graph per colorare le particelle in quest'area.")]
        public Color shapeColor = Color.cyan;

        [Header("Cyclic Movement (Lissajous)")]
        public Vector3 cyclicAmplitude = Vector3.zero;
        public Vector3 cyclicFrequency = new Vector3(1f, 0f, 2f); // Frequenze per figura a 8
        [Tooltip("Moltiplicatore dell'ampiezza, ideale da collegare a un DataStreamPropertyBinder (es. HRV)")]
        public float cyclicAmplitudeMultiplier = 0f;

        private Vector3 _basePosition;
        private float _timeAccumulator;
        private bool _isInitialized;

        void Start()
        {
            if (Application.isPlaying)
            {
                _basePosition = transform.localPosition;
                _isInitialized = true;
            }
        }

        void Update()
        {
            if (!Application.isPlaying) return;

            if (!_isInitialized)
            {
                _basePosition = transform.localPosition;
                _isInitialized = true;
            }

            if (cyclicAmplitude.sqrMagnitude > 0)
            {
                _timeAccumulator += Time.deltaTime;
                Vector3 offset = new Vector3(
                    Mathf.Sin(_timeAccumulator * cyclicFrequency.x) * cyclicAmplitude.x,
                    Mathf.Sin(_timeAccumulator * cyclicFrequency.y) * cyclicAmplitude.y,
                    Mathf.Sin(_timeAccumulator * cyclicFrequency.z) * cyclicAmplitude.z
                );
                transform.localPosition = _basePosition + offset * cyclicAmplitudeMultiplier;
            }
        }

        [HideInInspector]
        public float currentPulse = 0.0f;

        public float GetInteractionRadius()
        {
            switch (shapeType)
            {
                case SdfShapeType.Sphere:
                    return size.x;
                case SdfShapeType.Box:
                case SdfShapeType.BakedTexture3D:
                    return size.magnitude; // lunghezza della diagonale per bounds sicuri
                case SdfShapeType.Torus:
                    return size.x + size.y;
                case SdfShapeType.Capsule:
                case SdfShapeType.Cylinder:
                    return Mathf.Max(size.x, size.y * 0.5f);
                default:
                    return 0.1f;
            }
        }

        [ContextMenu("Adatta Proporzioni a Texture 3D")]
        public void MatchTextureAspectRatio()
        {
            if (bakedSdfTexture == null) return;
            float maxDim = Mathf.Max(bakedSdfTexture.width, Mathf.Max(bakedSdfTexture.height, bakedSdfTexture.depth));
            float currentMax = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (currentMax <= 0.001f) currentMax = 0.22f;
            size = new Vector3(
                currentMax * ((float)bakedSdfTexture.width / maxDim),
                currentMax * ((float)bakedSdfTexture.height / maxDim),
                currentMax * ((float)bakedSdfTexture.depth / maxDim)
            );
        }

        private void OnValidate()
        {
            // La validazione e sincronizzazione del blendSoftness è stata rimossa, 
            // poiché il parametro è ora globale e gestito da SdfGroupController.
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = shapeColor;
            Matrix4x4 rotationMatrix = transform.localToWorldMatrix;
            Gizmos.matrix = rotationMatrix;

            switch (shapeType)
            {
                case SdfShapeType.Sphere:
                    Gizmos.DrawWireSphere(Vector3.zero, size.x);
                    break;
                case SdfShapeType.Box:
                case SdfShapeType.BakedTexture3D:
                    Gizmos.DrawWireCube(Vector3.zero, size * 2.0f);
                    break;
                case SdfShapeType.Torus:
#if UNITY_EDITOR
                    UnityEditor.Handles.color = GetGizmoColor();
                    UnityEditor.Handles.matrix = rotationMatrix;
                    UnityEditor.Handles.DrawWireDisc(Vector3.zero, Vector3.up, size.x + size.y);
                    UnityEditor.Handles.DrawWireDisc(Vector3.zero, Vector3.up, size.x - size.y);
#endif
                    break;
                case SdfShapeType.Capsule:
                    float h = Mathf.Max(0.0f, size.y - size.x * 2.0f);
                    Gizmos.DrawWireSphere(Vector3.up * (h * 0.5f), size.x);
                    Gizmos.DrawWireSphere(Vector3.down * (h * 0.5f), size.x);
                    Gizmos.DrawLine(new Vector3(size.x, -h * 0.5f, 0), new Vector3(size.x, h * 0.5f, 0));
                    Gizmos.DrawLine(new Vector3(-size.x, -h * 0.5f, 0), new Vector3(-size.x, h * 0.5f, 0));
                    Gizmos.DrawLine(new Vector3(0, -h * 0.5f, size.x), new Vector3(0, h * 0.5f, size.x));
                    Gizmos.DrawLine(new Vector3(0, -h * 0.5f, -size.x), new Vector3(0, h * 0.5f, -size.x));
                    break;
                case SdfShapeType.Cylinder:
#if UNITY_EDITOR
                    UnityEditor.Handles.color = GetGizmoColor();
                    UnityEditor.Handles.matrix = rotationMatrix;
                    UnityEditor.Handles.DrawWireDisc(Vector3.up * (size.y * 0.5f), Vector3.up, size.x);
                    UnityEditor.Handles.DrawWireDisc(Vector3.down * (size.y * 0.5f), Vector3.up, size.x);
#endif
                    Gizmos.DrawLine(new Vector3(size.x, -size.y * 0.5f, 0), new Vector3(size.x, size.y * 0.5f, 0));
                    Gizmos.DrawLine(new Vector3(-size.x, -size.y * 0.5f, 0), new Vector3(-size.x, size.y * 0.5f, 0));
                    break;
            }
        }

        private Color GetGizmoColor()
        {
            switch (combineOp)
            {
                case SdfCombineOp.Union:
                case SdfCombineOp.SmoothUnion:
                    return Color.cyan;
                case SdfCombineOp.Subtraction:
                case SdfCombineOp.SmoothSubtraction:
                    return Color.red; // Rosso indica sottrazione (scavo)
                case SdfCombineOp.Intersection:
                case SdfCombineOp.SmoothIntersection:
                    return Color.green; // Verde indica intersezione
                default:
                    return Color.white;
            }
        }
    }
}
