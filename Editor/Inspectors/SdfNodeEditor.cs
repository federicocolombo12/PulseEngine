using UnityEngine;
using UnityEditor;

namespace MedicalXR.RayMarching
{
    /// <summary>
    /// Custom Inspector for SdfNode.
    /// Handles conditional visibility and auto-assignment of blendSoftness based on the selected combineOp.
    /// </summary>
    [CustomEditor(typeof(SdfNode))]
    [CanEditMultipleObjects]
    public class SdfNodeEditor : Editor
    {
        SerializedProperty shapeType;
        SerializedProperty size;
        SerializedProperty bakedSdfTexture;
        SerializedProperty combineOp;
        SerializedProperty pulseSensitivity;
        SerializedProperty shapeColor;
        
        private bool showAdvanced = false;

        void OnEnable()
        {
            shapeType = serializedObject.FindProperty("shapeType");
            size = serializedObject.FindProperty("size");
            bakedSdfTexture = serializedObject.FindProperty("bakedSdfTexture");
            combineOp = serializedObject.FindProperty("combineOp");
            pulseSensitivity = serializedObject.FindProperty("pulseSensitivity");
            shapeColor = serializedObject.FindProperty("shapeColor");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(shapeType);
            EditorGUILayout.PropertyField(size);
            
            if (shapeType.enumValueIndex == (int)SdfShapeType.BakedTexture3D)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(bakedSdfTexture);
                bool texChanged = EditorGUI.EndChangeCheck();

                Texture3D tex = bakedSdfTexture.objectReferenceValue as Texture3D;
                if (tex != null)
                {
                    EditorGUILayout.HelpBox($"Risoluzione Texture3D: {tex.width} x {tex.height} x {tex.depth}", MessageType.None);
                    if (GUILayout.Button("📐 Adatta Proporzioni Texture (Aspect Ratio)"))
                    {
                        foreach (var t in targets)
                        {
                            SdfNode node = t as SdfNode;
                            if (node != null)
                            {
                                Undo.RecordObject(node, "Match Texture Aspect Ratio");
                                node.MatchTextureAspectRatio();
                                EditorUtility.SetDirty(node);
                            }
                        }
                    }
                }
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Blending & Combination (MudBun Cascade)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(combineOp);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("blendSoftness"));

            EditorGUILayout.Space(10);
            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced / Visual Overrides", true);
            if (showAdvanced)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(pulseSensitivity);
                EditorGUILayout.PropertyField(shapeColor);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Cyclic Movement (Lissajous)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cyclicAmplitude"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cyclicFrequency"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cyclicAmplitudeMultiplier"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
