using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace MedicalXR.RayMarching
{
    [CustomEditor(typeof(SdfVolumeVoxelizer))]
    public class SdfVolumeVoxelizerEditor : Editor
    {
        private BoxBoundsHandle m_BoundsHandle = new BoxBoundsHandle();
        private bool m_IsEditingBounds = false;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            SdfVolumeVoxelizer voxelizer = (SdfVolumeVoxelizer)target;

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Volume Bounds & Sizing", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = m_IsEditingBounds ? Color.green : Color.white;
            if (GUILayout.Button(m_IsEditingBounds ? "✔ Stop Editing Bounds" : "✏ Edit Bounds (Scene)", GUILayout.Height(30)))
            {
                m_IsEditingBounds = !m_IsEditingBounds;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("📐 Fit to Nodes", GUILayout.Height(30)))
            {
                Undo.RecordObject(voxelizer, "Fit Bounds to Nodes");
                voxelizer.FitToNodes();
            }

            if (GUILayout.Button("↺ Reset Default", GUILayout.Height(30)))
            {
                Undo.RecordObject(voxelizer, "Reset Volume Size");
                voxelizer.volumeSize = 2.0f;
                voxelizer.transform.localScale = Vector3.one;
                voxelizer.targetVoxelDensity = 48f; // 96 res / 2.0m = 48
                EditorUtility.SetDirty(voxelizer);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("Il volume delimita la griglia cubica voxel 3D trasmessa al VFX Graph. " +
                                  "Una dimensione tra 1.0m e 2.0m garantisce che la geometria e l'alone di particelle fluttuino liberamente senza troncamenti.", MessageType.None);
        }

        void OnDisable()
        {
            Tools.hidden = false;
        }

        public void OnSceneGUI()
        {
            if (!m_IsEditingBounds)
            {
                Tools.hidden = false;
                return;
            }

            Tools.hidden = true;

            SdfVolumeVoxelizer voxelizer = (SdfVolumeVoxelizer)target;

            // Set matrix to draw handles in local space of the voxelizer (without scale)
            Matrix4x4 handleMatrix = Matrix4x4.TRS(voxelizer.transform.position, voxelizer.transform.rotation, Vector3.one);
            Handles.matrix = handleMatrix;

            // Testo a schermo per confermare che il tool è attivo
            Handles.BeginGUI();
            var style = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = Color.green } };
            GUI.Label(new Rect(10, 10, 300, 30), "✏ EDITING SDF VOLUME BOUNDS", style);
            Handles.EndGUI();

            Vector3 vol3D = voxelizer.VolumeSize3D;

            // Fallback visivo per essere certi che disegni qualcosa anche se il BoundsHandle fallisce
            Handles.color = new Color(1f, 0.6f, 0f, 1f);
            Handles.DrawWireCube(Vector3.zero, vol3D);

            // Configure the handle
            m_BoundsHandle.center = Vector3.zero;
            m_BoundsHandle.size = vol3D;
            m_BoundsHandle.SetColor(new Color(1f, 0.7f, 0f, 1f));

            EditorGUI.BeginChangeCheck();
            m_BoundsHandle.DrawHandle();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(voxelizer, "Resize SDF Volume");
                
                // Enforce uniform scalar size based on max handle dimension
                Vector3 newSize = m_BoundsHandle.size;
                float maxDim = Mathf.Max(0.1f, Mathf.Max(newSize.x, Mathf.Max(newSize.y, newSize.z)));

                voxelizer.volumeSize = maxDim;
                voxelizer.transform.localScale = Vector3.one;
                m_BoundsHandle.center = Vector3.zero;
                
                EditorUtility.SetDirty(voxelizer);
            }
        }
    }
}
