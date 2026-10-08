using UnityEngine;

namespace MedicalXR.RayMarching
{
    public enum SdfModifierType
    {
        Twist = 0,
        Bend = 1,
        Noise = 2
    }

    public enum SdfModifierScope
    {
        AllNodesInVolume = 0,     // Modifica qualsiasi nodo geometrico che si trova all'interno del volume
        PrecedingInHierarchy = 1  // Stile MudBun: modifica esclusivamente i nodi geometrici sovrastanti nella gerarchia
    }

    /// <summary>
    /// Nodo di volume autonomo (stile MudBun) che definisce un Bounding Box di forza locale.
    /// Deforma le coordinate spaziali di campionamento di tutti i nodi sovrastanti (o interni)
    /// che cadono all'interno del suo volume.
    /// </summary>
    [ExecuteAlways]
    public class SdfModifierVolume : MonoBehaviour
    {
        [Header("Modifier Configuration")]
        [Tooltip("Il tipo di deformazione applicata dal volume.")]
        public SdfModifierType modifierType = SdfModifierType.Twist;

        [Tooltip("Ambito di influenza: 'AllNodesInVolume' deforma tutti i nodi nel volume ovunque siano nella gerarchia; 'PrecedingInHierarchy' deforma solo i nodi sopra di esso (stile MudBun).")]
        public SdfModifierScope scope = SdfModifierScope.AllNodesInVolume;

        [Tooltip("Semi-estensione (metà larghezza/altezza/profondità) del Bounding Box di forza.")]
        public Vector3 size = new Vector3(0.25f, 0.25f, 0.25f);

        [Range(0.005f, 0.5f)]
        [Tooltip("Fascia di decadimento (falloff) sui bordi del box per un raccordo morbidissimo.")]
        public float falloff = 0.05f;

        [Header("Intensity & Parameters")]
        [Tooltip("Intensità dell'effetto (forza torsione Y, curvatura XY o intensità distorsione rumore 3D).")]
        public float strength = 1.0f;

        [Range(0.1f, 30f)]
        [Tooltip("Frequenza spaziale delle ondulazioni (utilizzata principalmente per il modificatore Noise).")]
        public float frequency = 5.0f;

        [Header("Gizmo Visualizer")]
        public Color gizmoColor = new Color(1f, 0.6f, 0.1f, 0.35f);

        private void OnDrawGizmos()
        {
            Vector3 s = transform.localScale;
            Vector3 scaledHalf = new Vector3(
                Mathf.Max(0.0001f, Mathf.Abs(size.x * s.x)),
                Mathf.Max(0.0001f, Mathf.Abs(size.y * s.y)),
                Mathf.Max(0.0001f, Mathf.Abs(size.z * s.z))
            );

            Matrix4x4 prev = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

            // Wireframe del volume interno di massima influenza
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireCube(Vector3.zero, scaledHalf * 2.0f);

            // Volume esterno di falloff completo
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, gizmoColor.a * 0.35f);
            Gizmos.DrawWireCube(Vector3.zero, (scaledHalf + Vector3.one * falloff) * 2.0f);

            Gizmos.matrix = prev;
        }
    }
}
