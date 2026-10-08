using UnityEngine;
using UnityEditor;

namespace GenericDataStreaming.EditorScripts
{
    [CustomEditor(typeof(DataStreamColorBinder))]
    public class DataStreamColorBinderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("channelId"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("colorGradient"));

            SerializedProperty compProp = serializedObject.FindProperty("targetComponent");
            EditorGUILayout.PropertyField(compProp);

            if (compProp.objectReferenceValue != null)
            {
                Component comp = (Component)compProp.objectReferenceValue;
                string[] options = DataStreamEditorUtils.GetPropertiesForComponent(comp, true);
                
                SerializedProperty nameProp = serializedObject.FindProperty("targetPropertyName");
                nameProp.stringValue = DataStreamEditorUtils.DrawPropertyDropdown("Target Property (Color)", nameProp.stringValue, options);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
