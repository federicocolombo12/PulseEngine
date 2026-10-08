using UnityEngine;

namespace GenericDataStreaming.Modifiers
{
    public enum MathOperation
    {
        ThresholdToBinary, // Se > Threshold = 1, altrimenti 0
        ThresholdToBinaryInverted, // Se < Threshold = 1, altrimenti 0
        Clamp,
        Multiply,
        Add
    }

    /// <summary>
    /// Modificatore logico che prende un canale continuo, applica una formula matematica/logica
    /// e genera un nuovo canale continuo. Sostituisce la logica hardcoded nei bridge.
    /// </summary>
    [AddComponentMenu("Generic Data Streaming/Modifiers/Math Modifier")]
    public class DataStreamMathModifier : MonoBehaviour
    {
        [Tooltip("L'ID del canale sorgente da leggere (es: 'spo2', 'hrv_rmssd').")]
        public string sourceChannelId;

        [Tooltip("L'ID del canale di destinazione in cui scrivere il risultato (es: 'is_hypoxic', 'hrv_modulator').")]
        public string targetChannelId;

        [Tooltip("Operazione da applicare al valore sorgente.")]
        public MathOperation operation;

        [Header("Operation Parameters")]
        public float operand = 90f; // Usato come soglia, moltiplicatore, ecc.
        public Vector2 clampRange = new Vector2(0f, 1f);

        void Update()
        {
            if (DataStreamRegistry.Instance == null || string.IsNullOrEmpty(sourceChannelId) || string.IsNullOrEmpty(targetChannelId))
                return;

            float inputValue = DataStreamRegistry.Instance.GetValue(sourceChannelId);
            float outputValue = inputValue;

            switch (operation)
            {
                case MathOperation.ThresholdToBinary:
                    outputValue = inputValue >= operand ? 1.0f : 0.0f;
                    break;
                case MathOperation.ThresholdToBinaryInverted:
                    outputValue = inputValue <= operand ? 1.0f : 0.0f;
                    break;
                case MathOperation.Clamp:
                    outputValue = Mathf.Clamp(inputValue, clampRange.x, clampRange.y);
                    break;
                case MathOperation.Multiply:
                    outputValue = inputValue * operand;
                    break;
                case MathOperation.Add:
                    outputValue = inputValue + operand;
                    break;
            }

            DataStreamRegistry.Instance.PushValue(targetChannelId, outputValue);
        }
    }
}
