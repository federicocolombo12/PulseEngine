using UnityEngine;
using UnityEditor;

namespace GenericDataStreaming.EditorScripts
{
    [CustomEditor(typeof(DataStreamPropertyBinder))]
    public class DataStreamPropertyBinderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("channelId"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("weightMultiplier"));

            SerializedProperty compProp = serializedObject.FindProperty("targetComponent");
            EditorGUILayout.PropertyField(compProp);

            if (compProp.objectReferenceValue != null)
            {
                Component comp = (Component)compProp.objectReferenceValue;
                string[] options = DataStreamEditorUtils.GetPropertiesForComponent(comp, false);
                
                SerializedProperty nameProp = serializedObject.FindProperty("targetPropertyName");
                nameProp.stringValue = DataStreamEditorUtils.DrawPropertyDropdown("Target Property", nameProp.stringValue, options);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
