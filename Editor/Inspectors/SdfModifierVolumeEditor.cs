using UnityEngine;
using UnityEditor;

namespace MedicalXR.RayMarching
{
    [CustomEditor(typeof(SdfModifierVolume))]
    [CanEditMultipleObjects]
    public class SdfModifierVolumeEditor : Editor
    {
        SerializedProperty modifierType;
        SerializedProperty scope;
        SerializedProperty size;
        SerializedProperty falloff;
        SerializedProperty strength;
        SerializedProperty frequency;
        SerializedProperty gizmoColor;

        void OnEnable()
        {
            modifierType = serializedObject.FindProperty("modifierType");
            scope = serializedObject.FindProperty("scope");
            size = serializedObject.FindProperty("size");
            falloff = serializedObject.FindProperty("falloff");
            strength = serializedObject.FindProperty("strength");
            frequency = serializedObject.FindProperty("frequency");
            gizmoColor = serializedObject.FindProperty("gizmoColor");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("MudBun Domain Modifier Volume", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(modifierType);
            EditorGUILayout.PropertyField(scope);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Volume Force Field Bounds", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(size);
            EditorGUILayout.PropertyField(falloff);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(strength);
            if (modifierType.enumValueIndex == (int)SdfModifierType.Noise)
            {
                EditorGUILayout.PropertyField(frequency);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.PropertyField(gizmoColor);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
