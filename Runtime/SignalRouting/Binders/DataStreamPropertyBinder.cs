using UnityEngine;
using UnityEngine.VFX;

namespace GenericDataStreaming
{
    
    public enum BindingTargetType
    {
        MaterialProperty,
        VfxProperty,
        VfxEvent,
        UnityEvent,
        TransformScale,
        ComponentProperty
    }

    [AddComponentMenu("Generic Data Streaming/Data Stream Property Binder")]
    [ExecuteAlways]
    public class DataStreamPropertyBinder : MonoBehaviour
    {
        [Tooltip("Attiva o disattiva temporaneamente il binding. Se disattivato, ripristina il valore originale.")]
        public bool isEnabled = true;

        [Tooltip("L'ID del canale dati da leggere dal DataStreamRegistry (es: 'heart_rate').")]
        public string channelId;

        [Tooltip("Il moltiplicatore (peso) da applicare al valore del canale.")]
        public float weightMultiplier = 1.0f;

        [Header("Local Remapping")]
        [Tooltip("Se attivo, ignora il Weight e mappa il valore (supposto 0-1) a un nuovo range locale.")]
        public bool useLocalRemap = false;
        public Vector2 localRemap = new Vector2(0f, 1f);

        [Tooltip("Il Componente di destinazione (VisualEffect, Renderer, Transform, o script custom).")]
        public Component targetComponent;

        [Tooltip("Il nome esatto della proprietà, parametro o variabile da modificare.")]
        public string targetPropertyName;

        // Cache for performance
        private VisualEffect cachedVfx;
        private Material cachedMaterial;
        private Transform cachedTransform;
        private System.Reflection.FieldInfo cachedField;
        private System.Reflection.PropertyInfo cachedProperty;

        private float? originalFloatValue = null;
        private Vector3? originalScaleValue = null;
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
                if (cachedVfx.HasFloat(targetPropertyName) && originalFloatValue == null)
                    originalFloatValue = cachedVfx.GetFloat(targetPropertyName);
                return;
            }

            var renderer = targetComponent as Renderer;
            if (renderer != null)
            {
                cachedMaterial = Application.isPlaying ? renderer.material : renderer.sharedMaterial;
                if (cachedMaterial != null && cachedMaterial.HasFloat(targetPropertyName) && originalFloatValue == null)
                    originalFloatValue = cachedMaterial.GetFloat(targetPropertyName);
                return;
            }

            cachedTransform = targetComponent as Transform;
            if (cachedTransform != null)
            {
                if (originalScaleValue == null) originalScaleValue = cachedTransform.localScale;
                return;
            }

            // Fallback: Custom Component via Reflection
            var type = targetComponent.GetType();
            cachedField = type.GetField(targetPropertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (cachedField == null)
                cachedProperty = type.GetProperty(targetPropertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            if (originalFloatValue == null)
            {
                try
                {
                    if (cachedField != null)
                    {
                        var val = cachedField.GetValue(targetComponent);
                        if (val != null)
                        {
                            originalFloatValue = System.Convert.ToSingle(val);
                        }
                    }
                    else if (cachedProperty != null)
                    {
                        var val = cachedProperty.GetValue(targetComponent);
                        if (val != null)
                        {
                            originalFloatValue = System.Convert.ToSingle(val);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[DataStreamPropertyBinder] Impossibile inizializzare il valore originale per '{targetPropertyName}': {ex.Message}");
                }
            }
        }

        void OnDestroy() => RestoreOriginalValue();
        void OnDisable() => RestoreOriginalValue();

        private void RestoreOriginalValue()
        {
            if (cachedVfx != null && originalFloatValue.HasValue && cachedVfx.HasFloat(targetPropertyName))
                cachedVfx.SetFloat(targetPropertyName, originalFloatValue.Value);
            else if (cachedMaterial != null && originalFloatValue.HasValue && cachedMaterial.HasFloat(targetPropertyName))
                cachedMaterial.SetFloat(targetPropertyName, originalFloatValue.Value);
            else if (cachedTransform != null && originalScaleValue.HasValue)
                cachedTransform.localScale = originalScaleValue.Value;
            else if (originalFloatValue.HasValue)
            {
                if (cachedField != null)
                {
                    if (cachedField.FieldType.IsEnum) cachedField.SetValue(targetComponent, System.Enum.ToObject(cachedField.FieldType, Mathf.RoundToInt(originalFloatValue.Value)));
                    else if (cachedField.FieldType == typeof(int)) cachedField.SetValue(targetComponent, Mathf.RoundToInt(originalFloatValue.Value));
                    else cachedField.SetValue(targetComponent, originalFloatValue.Value);
                }
                else if (cachedProperty != null)
                {
                    if (cachedProperty.PropertyType.IsEnum) cachedProperty.SetValue(targetComponent, System.Enum.ToObject(cachedProperty.PropertyType, Mathf.RoundToInt(originalFloatValue.Value)));
                    else if (cachedProperty.PropertyType == typeof(int)) cachedProperty.SetValue(targetComponent, Mathf.RoundToInt(originalFloatValue.Value));
                    else cachedProperty.SetValue(targetComponent, originalFloatValue.Value);
                }
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

            if (cachedVfx == null && cachedMaterial == null && cachedTransform == null && cachedField == null && cachedProperty == null)
            {
                InitializeBindings();
            }

            float rawVal = DataStreamRegistry.Instance.GetValue(channelId);
            float val = useLocalRemap ? Mathf.Lerp(localRemap.x, localRemap.y, rawVal) : rawVal * weightMultiplier;

            if (cachedVfx != null)
            {
                if (cachedVfx.HasFloat(targetPropertyName))
                {
                    cachedVfx.SetFloat(targetPropertyName, val);
                }
                else if (cachedVfx.HasInt(targetPropertyName))
                {
                    cachedVfx.SetInt(targetPropertyName, Mathf.RoundToInt(val));
                }
            }
            else if (cachedMaterial != null)
            {
                if (cachedMaterial.HasFloat(targetPropertyName)) cachedMaterial.SetFloat(targetPropertyName, val);
            }
            else if (cachedTransform != null)
            {
                Vector3 scale = cachedTransform.localScale;
                if (targetPropertyName == "UniformScale") scale = Vector3.one * val;
                else if (targetPropertyName == "ScaleX") scale.x = val;
                else if (targetPropertyName == "ScaleY") scale.y = val;
                else if (targetPropertyName == "ScaleZ") scale.z = val;
                cachedTransform.localScale = scale;
            }
            else
            {
                if (cachedField != null)
                {
                    if (cachedField.FieldType.IsEnum) cachedField.SetValue(targetComponent, System.Enum.ToObject(cachedField.FieldType, Mathf.RoundToInt(val)));
                    else if (cachedField.FieldType == typeof(int)) cachedField.SetValue(targetComponent, Mathf.RoundToInt(val));
                    else cachedField.SetValue(targetComponent, val);
                }
                else if (cachedProperty != null)
                {
                    if (cachedProperty.PropertyType.IsEnum) cachedProperty.SetValue(targetComponent, System.Enum.ToObject(cachedProperty.PropertyType, Mathf.RoundToInt(val)));
                    else if (cachedProperty.PropertyType == typeof(int)) cachedProperty.SetValue(targetComponent, Mathf.RoundToInt(val));
                    else cachedProperty.SetValue(targetComponent, val);
                }
            }
        }
    }
}
