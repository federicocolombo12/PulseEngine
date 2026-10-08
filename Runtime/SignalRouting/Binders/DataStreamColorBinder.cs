using UnityEngine;
using UnityEngine.VFX;

namespace GenericDataStreaming
{
    [AddComponentMenu("Generic Data Streaming/Data Stream Color Binder")]
    [ExecuteAlways]
    public class DataStreamColorBinder : MonoBehaviour
    {
        [Tooltip("Attiva o disattiva temporaneamente il binding. Se disattivato, ripristina il colore originale.")]
        public bool isEnabled = true;

        [Tooltip("L'ID del canale dati da leggere dal DataStreamRegistry.")]
        public string channelId;

        [Tooltip("Il gradiente usato per mappare il valore float (0.0-1.0) in un colore.")]
        public Gradient colorGradient = new Gradient();

        [Tooltip("Il Componente di destinazione (VisualEffect, Renderer, o script custom).")]
        public Component targetComponent;

        [Tooltip("Il nome esatto della proprietà colore da modificare.")]
        public string targetPropertyName;

        private VisualEffect cachedVfx;
        private Material cachedMaterial;
        private System.Reflection.FieldInfo cachedField;
        private System.Reflection.PropertyInfo cachedProperty;

        private Color? originalColorValue = null;
        private bool isRestored = false;

        void OnEnable()
        {
            InitializeBindings();
        }

        void OnValidate()
        {
            InitializeBindings();
        }


        public void InitializeBindings()
        {
            if (targetComponent == null || string.IsNullOrEmpty(targetPropertyName)) return;

            cachedVfx = targetComponent as VisualEffect;
            if (cachedVfx != null)
            {
                if (cachedVfx.HasVector4(targetPropertyName) && originalColorValue == null)
                    originalColorValue = cachedVfx.GetVector4(targetPropertyName);
                return;
            }

            var renderer = targetComponent as Renderer;
            if (renderer != null)
            {
                cachedMaterial = Application.isPlaying ? renderer.material : renderer.sharedMaterial;
                if (cachedMaterial != null && cachedMaterial.HasColor(targetPropertyName) && originalColorValue == null)
                    originalColorValue = cachedMaterial.GetColor(targetPropertyName);
                return;
            }

            var type = targetComponent.GetType();
            cachedField = type.GetField(targetPropertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (cachedField == null)
                cachedProperty = type.GetProperty(targetPropertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            if (originalColorValue == null)
            {
                if (cachedField != null && cachedField.FieldType == typeof(Color)) 
                    originalColorValue = (Color)cachedField.GetValue(targetComponent);
                else if (cachedProperty != null && cachedProperty.PropertyType == typeof(Color)) 
                    originalColorValue = (Color)cachedProperty.GetValue(targetComponent);
            }
        }

        void OnDestroy() => RestoreOriginalValue();
        void OnDisable() => RestoreOriginalValue();

        private void RestoreOriginalValue()
        {
            if (cachedVfx != null && originalColorValue.HasValue)
                cachedVfx.SetVector4(targetPropertyName, originalColorValue.Value);
            else if (cachedMaterial != null && originalColorValue.HasValue && cachedMaterial.HasColor(targetPropertyName))
                cachedMaterial.SetColor(targetPropertyName, originalColorValue.Value);
            else if (originalColorValue.HasValue)
            {
                if (cachedField != null) cachedField.SetValue(targetComponent, originalColorValue.Value);
                else if (cachedProperty != null) cachedProperty.SetValue(targetComponent, originalColorValue.Value);
            }
        }

        void Update()
        {
            if (DataStreamRegistry.Instance == null || string.IsNullOrEmpty(channelId)) return;
            if (targetComponent == null || string.IsNullOrEmpty(targetPropertyName)) return;

            bool isSilenced = DataStreamRegistry.Instance != null && DataStreamRegistry.Instance.IsChannelSilenced(channelId);
            if (!isEnabled || isSilenced)
            {
                if (!isRestored)
                {
                    RestoreOriginalValue();
                    isRestored = true;
                }
                return;
            }
            isRestored = false;

            if (cachedVfx == null && cachedMaterial == null && cachedField == null && cachedProperty == null)
            {
                InitializeBindings();
            }

            float val = DataStreamRegistry.Instance.GetValue(channelId);
            Color mappedColor = colorGradient.Evaluate(Mathf.Clamp01(val));

            if (cachedVfx != null)
            {
                cachedVfx.SetVector4(targetPropertyName, mappedColor);
            }
            else if (cachedMaterial != null)
            {
                if (cachedMaterial.HasColor(targetPropertyName)) cachedMaterial.SetColor(targetPropertyName, mappedColor);
            }
            else
            {
                if (cachedField != null) cachedField.SetValue(targetComponent, mappedColor);
                else if (cachedProperty != null) cachedProperty.SetValue(targetComponent, mappedColor);
            }
        }
    }
}
