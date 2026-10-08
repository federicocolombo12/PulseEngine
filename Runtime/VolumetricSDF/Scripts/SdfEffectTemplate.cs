using UnityEngine;
using GenericDataStreaming;
using System.Collections.Generic;

namespace MedicalXR.RayMarching
{
    [System.Serializable]
    public struct TemplateDataBinding
    {
        [Tooltip("La stringa ID del canale da cui prelevare il segnale (es: 'heart_rate', 'stress_level').")]
        public string channelId;

        [Tooltip("Il target a cui applicare questo segnale.")]
        public BindingTargetType targetType;

        [Tooltip("Il nome della proprietà esposta (es: '_NoiseScale' per Material, o 'HeartPulse' per VFX).")]
        public string targetPropertyName;
    }

    [CreateAssetMenu(fileName = "New Sdf Template", menuName = "MedicalXR/SDF Template")]
    public class SdfEffectTemplate : ScriptableObject
    {
        [Header("Bio-Signal Overrides")]
        [Tooltip("Softness globale del blending tra le forme. Valori bassi = forme separate, valori alti = forme fuse.")]
        [Range(0f, 1f)] public float globalBlendSoftness = 0.2f;
        
        [Tooltip("Velocità del 'respiro' o pulsazione dell'organo.")]
        [Range(0f, 5f)] public float breathingSpeed = 2.0f;
        
        [Tooltip("Forza della pulsazione dell'organo.")]
        [Range(0f, 1f)] public float breathingAmplitude = 0.1f;
        
        [Header("Procedural Noise (Stress/Arousal)")]
        [Tooltip("Scala del rumore procedurale. Più è alto, più è frammentato.")]
        [Range(1f, 15f)] public float noiseScale = 6.0f;
        
        [Tooltip("Intensità del rumore (deformazione della superficie).")]
        [Range(0f, 0.2f)] public float noiseStrength = 0.04f;

        [Header("Color Profile")]
        [Tooltip("Colore principale da applicare ai nodi primari.")]
        [ColorUsage(true, true)] public Color primaryColor = new Color(0.0f, 0.8f, 0.95f, 1.0f);
        
        [Tooltip("Colore secondario da applicare ai nodi secondari.")]
        [ColorUsage(true, true)] public Color secondaryColor = new Color(0.0f, 0.4f, 0.8f, 1.0f);
        
        [Header("CSG Operations")]
        [Tooltip("Operazione dominante tra i nodi (SmoothUnion = Coerenza, Subtraction/SmoothSubtraction = Stress).")]
        public SdfCombineOp primaryCombineOp = SdfCombineOp.SmoothUnion;

        [Header("Dynamic Data Bindings")]
        [Tooltip("Regole per mappare automaticamente i canali biologici ai parametri di questo stato.")]
        public List<TemplateDataBinding> dataBindings = new List<TemplateDataBinding>();
    }
}
