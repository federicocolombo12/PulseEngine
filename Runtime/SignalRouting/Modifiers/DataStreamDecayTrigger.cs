using UnityEngine;

namespace GenericDataStreaming.Modifiers
{
    /// <summary>
    /// Ascolta un trigger discreto e genera un segnale continuo che decade nel tempo.
    /// Utile per trasformare un "battito" in un'onda in discesa per effetti visivi morbidi.
    /// </summary>
    [AddComponentMenu("Generic Data Streaming/Modifiers/Decay Trigger")]
    [ExecuteAlways]
    public class DataStreamDecayTrigger : MonoBehaviour
    {
        [Tooltip("L'ID del trigger discreto da ascoltare (es: 'heartbeat').")]
        public string triggerId = "heartbeat";

        [Tooltip("L'ID del canale continuo in cui scrivere il valore calcolato (es: 'heart_pulse').")]
        public string outputChannelId = "heart_pulse";

        [Tooltip("Valore di picco raggiunto istantaneamente quando scatta il trigger.")]
        public float maxIntensity = 1.0f;

        [Tooltip("Velocità lineare con cui il segnale torna a zero.")]
        public float decaySpeed = 5.0f;

        private float currentValue = 0f;
        private bool isRegistered = false;

        void OnEnable()
        {
            Register();
        }

        void OnDisable()
        {
            Deregister();
        }

        private void Register()
        {
            if (DataStreamRegistry.Instance != null && !string.IsNullOrEmpty(triggerId))
            {
                DataStreamRegistry.Instance.RegisterTriggerListener(triggerId, OnTriggerFired);
                isRegistered = true;
            }
        }

        private void Deregister()
        {
            if (DataStreamRegistry.Instance != null && isRegistered)
            {
                DataStreamRegistry.Instance.DeregisterTriggerListener(triggerId, OnTriggerFired);
                isRegistered = false;
            }
        }

        private void OnTriggerFired()
        {
            currentValue = maxIntensity;
        }

        private double lastEditorTime;

        void Update()
        {
            // Prova a registrarsi in ritardo se il registro non era pronto
            if (!isRegistered && DataStreamRegistry.Instance != null)
            {
                Register();
            }

#if UNITY_EDITOR
            double currentEditorTime = UnityEditor.EditorApplication.timeSinceStartup;
            float dt = Application.isPlaying ? Time.deltaTime : (float)(currentEditorTime - lastEditorTime);
            if (dt <= 0f || dt > 1f) dt = 0.016f; // Fallback e tetto massimo se l'editor si era "addormentato"
            lastEditorTime = currentEditorTime;
#else
            float dt = Time.deltaTime;
#endif

            if (currentValue > 0f)
            {
                // Decadimento lineare
                currentValue = Mathf.Max(0.0f, currentValue - dt * decaySpeed);
            }

            // Invia il valore al canale di uscita se specificato
            if (DataStreamRegistry.Instance != null && !string.IsNullOrEmpty(outputChannelId))
            {
                DataStreamRegistry.Instance.PushValue(outputChannelId, currentValue);
            }
        }
    }
}
