using System;
using UnityEngine;

namespace GenericDataStreaming
{
    /// <summary>
    /// Rappresenta un singolo canale di dati in tempo reale (es. heart_rate, gyro_y).
    /// Gestisce la calibrazione dell'input, la rimappatura del range, il clamping e lo smoothing temporale.
    /// </summary>
    [System.Serializable]
    
    public enum ChannelSourceType { External, Virtual }
    public enum ChannelFilterType { None, SineWavePulse }

    public class DataStreamChannel
    {
        [Tooltip("Identificativo univoco del canale (es: 'heart_rate', 'gyro_x').")]
        public string id;

                [Header("Routing (DAW)")]
        public ChannelSourceType sourceType = ChannelSourceType.External;
        public string sourceChannelId = "";
        public ChannelFilterType filterType = ChannelFilterType.None;
        public bool isMuted = false;
        public bool isSolo = false;

        [Header("Range Mapping")]
        [Tooltip("Range minimo e massimo previsto in ingresso (es. 40 e 180 BPM).")]
        public Vector2 inputRange = new Vector2(0f, 100f);

        [Tooltip("Range minimo e massimo in uscita desiderato su Unity (es. 0 e 1 per shader).")]
        public Vector2 outputRange = new Vector2(0f, 1f);

        [Tooltip("Se attivo, il valore in uscita viene limitato rigidamente al range di output.")]
        public bool clampOutput = true;

        [Header("Smoothing & Filtering")]
        [Range(0f, 0.999f)]
        [Tooltip("Fattore di smorzamento. 0 = reazione istantanea, 0.99 = smoothing molto lento e morbido.")]
        public float smoothing = 0.5f;

        // Proprietà di sola lettura dei valori correnti
        public float RawValue { get; private set; }
        public float TargetValue { get; private set; } // Valore mappato prima dell'interpolazione
        public float Value { get; private set; }       // Valore finale (mappato e filtrato)

        public event Action<float> OnValueChanged;

        /// <summary>
        /// Aggiorna il valore grezzo in ingresso e calcola il valore target mappato.
        /// </summary>
        public void UpdateValue(float rawInput)
        {
            RawValue = rawInput;

            // Mapping lineare del valore grezzo dall'inputRange all'outputRange
            float t = Mathf.InverseLerp(inputRange.x, inputRange.y, rawInput);
            float mapped = Mathf.Lerp(outputRange.x, outputRange.y, t);

            if (clampOutput)
            {
                mapped = Mathf.Clamp(mapped, Mathf.Min(outputRange.x, outputRange.y), Mathf.Max(outputRange.x, outputRange.y));
            }

            TargetValue = mapped;

            // Se non c'è smoothing, applica il valore istantaneamente
            if (smoothing <= 0.001f)
            {
                SetValue(TargetValue);
            }
        }

        /// <summary>
        /// Esegue l'interpolazione temporale per garantire transizioni fluide indipendentemente dal framerate.
        /// Chiamato ad ogni Update da DataStreamRegistry.
        /// </summary>
                private float phase = 0f;
        
        public void ProcessVirtual(float sourceValue, float rawSourceValue, float deltaTime)
        {
            float computedInput = sourceValue;
            
            if (filterType == ChannelFilterType.SineWavePulse)
            {
                // Usa il valore raw (BPM grezzo, 60-180) per calcolare la frequenza reale
                float freq = rawSourceValue / 60f; 
                if (freq <= 0.01f) freq = sourceValue; // Fallback se raw non ha senso
                phase += freq * deltaTime * Mathf.PI * 2f;
                computedInput = (Mathf.Sin(phase) + 1f) / 2f; // Oscillates between 0 and 1
                
                // Forza l'input range a 0-1 e azzera lo smoothing per non attenuare la sinusoide
                inputRange = new Vector2(0f, 1f);
                smoothing = 0f;
            }

            UpdateValue(computedInput);
        }

        public void Interpolate(float deltaTime)
        {
            if (smoothing > 0.001f)
            {
                // Filtro passa-basso esponenziale dipendente dal delta-time (indipendente dal frame rate)
                float t = 1.0f - Mathf.Pow(smoothing, deltaTime * 60f);
                float nextValue = Mathf.Lerp(Value, TargetValue, t);
                SetValue(nextValue);
            }
        }

        private void SetValue(float newValue)
        {
            if (!Mathf.Approximately(Value, newValue))
            {
                Value = newValue;
                OnValueChanged?.Invoke(Value);
            }
        }
    }
}
