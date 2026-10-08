using UnityEngine;

namespace GenericDataStreaming.Modifiers
{
    /// <summary>
    /// Genera un trigger discreto interno basandosi su un canale continuo (es. BPM).
    /// Elimina completamente il jitter di rete (OSC/Bluetooth) garantendo una fluidità perfetta.
    /// </summary>
    [AddComponentMenu("Generic Data Streaming/Modifiers/Metronome")]
    [ExecuteAlways]
    public class DataStreamMetronome : MonoBehaviour
    {
        [Tooltip("Il canale continuo da cui leggere la frequenza (es. 'heart_rate').")]
        public string bpmChannelId = "heart_rate";

        [Tooltip("L'ID del trigger discreto da generare (es. 'heartbeat').")]
        public string outputTriggerId = "heartbeat";

        [Tooltip("Valore di BPM da usare come fallback se il canale è a 0.")]
        public float fallbackBPM = 60f;

        private float timer = 0f;
        private double lastEditorTime;

        void Update()
        {
            if (DataStreamRegistry.Instance == null || string.IsNullOrEmpty(outputTriggerId)) return;

            // Calcolo preciso del tempo che scorre (stesso fix usato nel Simulator)
#if UNITY_EDITOR
            double currentEditorTime = UnityEditor.EditorApplication.timeSinceStartup;
            float dt = Application.isPlaying ? Time.deltaTime : (float)(currentEditorTime - lastEditorTime);
            if (dt <= 0f || dt > 1f) dt = 0.016f;
            lastEditorTime = currentEditorTime;
#else
            float dt = Time.deltaTime;
#endif

            // Leggiamo il battito attuale dal registro (usiamo il valore Raw, non quello rimappato per i VFX!)
            float currentBPM = DataStreamRegistry.Instance.GetRawValue(bpmChannelId, fallbackBPM);
            if (currentBPM <= 0f) currentBPM = fallbackBPM;

            // Calcoliamo quanti secondi passano tra un battito e l'altro
            float interval = 60f / currentBPM;

            timer += dt;
            if (timer >= interval)
            {
                // Manteniamo il resto per evitare derive temporali
                timer -= interval;
                
                // Boom! Generiamo il battito perfetto
                Debug.Log($"[Metronome] Beep! BPM: {currentBPM}, Interval: {interval:F2}s, Output Trigger: {outputTriggerId}");
                DataStreamRegistry.Instance.PushTrigger(outputTriggerId);
            }
        }
    }
}
