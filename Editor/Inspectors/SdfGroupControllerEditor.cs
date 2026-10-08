using UnityEngine;
using UnityEditor;

namespace MedicalXR.RayMarching
{
    /// <summary>
    /// Ispettore personalizzato per SdfGroupController.
    /// Aggiunge un pulsante nell'ispettore di Unity per inserire dinamicamente
    /// nuovi nodi figli (SdfNode) e configurare le loro forme di default.
    /// </summary>
    [CustomEditor(typeof(SdfGroupController))]
    public class SdfGroupControllerEditor : Editor
    {
        private SdfShapeType shapeTypeToAdd = SdfShapeType.Sphere;
        private SdfModifierType modifierTypeToAdd = SdfModifierType.Twist;
        private SdfModifierScope modifierScopeToAdd = SdfModifierScope.AllNodesInVolume;
        private SdfEffectTemplate templateToApply;

        public override void OnInspectorGUI()
        {
            // Disegna l'ispettore standard per le proprietà pubbliche
            DrawDefaultInspector();

            SdfGroupController controller = (SdfGroupController)target;

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Template Settings", EditorStyles.boldLabel);
            templateToApply = (SdfEffectTemplate)EditorGUILayout.ObjectField("Template", templateToApply, typeof(SdfEffectTemplate), false);
            if (GUILayout.Button("Apply Template"))
            {
                if (templateToApply != null)
                {
                    controller.ApplyTemplate(templateToApply);
                    EditorUtility.SetDirty(controller);
                }
            }

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Procedural Node Creator", EditorStyles.boldLabel);

            // Menu a tendina per selezionare quale forma creare
            shapeTypeToAdd = (SdfShapeType)EditorGUILayout.EnumPopup("Shape to Add", shapeTypeToAdd);

            // Pulsante per aggiungere il nodo
            if (GUILayout.Button("Add Node", GUILayout.Height(30)))
            {
                SdfNode node = controller.AddNode(shapeTypeToAdd);
                Undo.RegisterCreatedObjectUndo(node.gameObject, "Create SDF Node");
                Selection.activeGameObject = node.gameObject;
                EditorUtility.SetDirty(controller);
            }

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Procedural Modifier Volume Creator (MudBun)", EditorStyles.boldLabel);

            modifierTypeToAdd = (SdfModifierType)EditorGUILayout.EnumPopup("Modifier Type", modifierTypeToAdd);
            modifierScopeToAdd = (SdfModifierScope)EditorGUILayout.EnumPopup("Influence Scope", modifierScopeToAdd);

            if (GUILayout.Button("Add Modifier Volume", GUILayout.Height(30)))
            {
                SdfModifierVolume mod = controller.AddModifierVolume(modifierTypeToAdd, modifierScopeToAdd);
                Undo.RegisterCreatedObjectUndo(mod.gameObject, "Create SDF Modifier Volume");
                Selection.activeGameObject = mod.gameObject;
                EditorUtility.SetDirty(controller);
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Twist Volume", EditorStyles.miniButtonLeft))
            {
                SdfModifierVolume mod = controller.AddModifierVolume(SdfModifierType.Twist, modifierScopeToAdd);
                Undo.RegisterCreatedObjectUndo(mod.gameObject, "Create Twist Modifier Volume");
                Selection.activeGameObject = mod.gameObject;
                EditorUtility.SetDirty(controller);
            }
            if (GUILayout.Button("+ Bend Volume", EditorStyles.miniButtonMid))
            {
                SdfModifierVolume mod = controller.AddModifierVolume(SdfModifierType.Bend, modifierScopeToAdd);
                Undo.RegisterCreatedObjectUndo(mod.gameObject, "Create Bend Modifier Volume");
                Selection.activeGameObject = mod.gameObject;
                EditorUtility.SetDirty(controller);
            }
            if (GUILayout.Button("+ Noise Volume", EditorStyles.miniButtonRight))
            {
                SdfModifierVolume mod = controller.AddModifierVolume(SdfModifierType.Noise, modifierScopeToAdd);
                Undo.RegisterCreatedObjectUndo(mod.gameObject, "Create Noise Modifier Volume");
                Selection.activeGameObject = mod.gameObject;
                EditorUtility.SetDirty(controller);
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
