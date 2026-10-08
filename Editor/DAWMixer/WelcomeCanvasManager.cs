#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace MedicalXR.EditorScripts
{
    [InitializeOnLoad]
    public static class WelcomeCanvasManager
    {
        static WelcomeCanvasManager()
        {
            EditorApplication.delayCall += CleanAndHideCanvas;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.ExitingEditMode)
            {
                HideCanvas();
            }
        }

        [MenuItem("MedicalXR/Toggle Tutorial Overlay Canvas")]
        public static void ToggleCanvas()
        {
            var canvas = GameObject.Find("Welcome_Canvas");
            if (canvas == null)
            {
                // Cerca anche tra gli inattivi
                var allCanvases = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var go in allCanvases)
                {
                    if (go.name == "Welcome_Canvas" && go.scene.isLoaded)
                    {
                        canvas = go;
                        break;
                    }
                }
            }

            if (canvas != null)
            {
                Undo.RecordObject(canvas, "Toggle Welcome Canvas");
                canvas.SetActive(!canvas.activeSelf);
                EditorUtility.SetDirty(canvas);
                Debug.Log($"[MedicalXR] Welcome_Canvas ora è: {(canvas.activeSelf ? "VISIBILE" : "NASCOSTO")}");
            }
        }

        public static void CleanAndHideCanvas()
        {
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in allObjects)
            {
                if (go.name == "Welcome_Canvas" && go.scene.isLoaded)
                {
                    var textComp = go.GetComponentInChildren<UnityEngine.UI.Text>(true);
                    if (textComp != null)
                    {
                        Undo.RecordObject(textComp, "Update Tutorial Text");
                        textComp.text = "MedicalXR Bio-Feedback Engine\n\n1. Open 'MedicalXR -> DAW Mixer' to manage data routing.\n2. Choose protocol: Unity Simulator, OSC / Checkme, WebSocket, Serial.\n3. Switch clinical scenarios (Calma, Stress, Ipossia) or apply Scene Templates.";
                        textComp.verticalOverflow = VerticalWrapMode.Overflow;
                        EditorUtility.SetDirty(textComp);
                    }

                    if (go.activeSelf)
                    {
                        Undo.RecordObject(go, "Hide Welcome Canvas");
                        go.SetActive(false);
                        EditorUtility.SetDirty(go);
                    }
                }
            }
        }

        private static void HideCanvas()
        {
            var canvas = GameObject.Find("Welcome_Canvas");
            if (canvas != null && canvas.activeSelf)
            {
                canvas.SetActive(false);
            }
        }
    }
}
#endif
