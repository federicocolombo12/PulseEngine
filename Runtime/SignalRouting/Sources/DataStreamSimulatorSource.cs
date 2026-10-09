using UnityEngine;

namespace GenericDataStreaming
{
    /// <summary>
    /// Sorgente mock per simulare flussi di dati continui ed eventi discreti (trigger)
    /// direttamente nell'editor di Unity senza hardware reale collegato.
    /// Utilissimo per il testing offline e dimostrazioni.
    /// </summary>
    [AddComponentMenu("Generic Data Streaming/Data Stream Simulator Source")]
    [ExecuteAlways]
    public class DataStreamSimulatorSource : MonoBehaviour, IDataStreamSource
    {
        public string ProtocolName => "Unity Simulator";
        public bool IsConnected => isStreaming;

        public enum SimulationScenario
        {
            Manual,
            Calma,
            Stress,
            Ipossia
        }

        [Header("Clinical Simulation Scenarios")]
        [Tooltip("Scenario clinico attivo con interpolazione fluida dei parametri biologici.")]
        public SimulationScenario currentScenario = SimulationScenario.Manual;

        [Header("Simulation Settings")]
        [Tooltip("Avvia lo streaming simulato automaticamente allo Start.")]
        public bool autoStart = true;
        
        [Header("Heartbeat Simulation Settings")]
        [Tooltip("Frequenza cardiaca simulata (BPM) per il calcolo del ritmo dei battiti.")]
        public float simulatedHeartRate = 75f;
        private float heartbeatTimer = 0f;

        [Header("Continuous Channels Simulation")]
        [Tooltip("Canale simulato tramite onda sinusoidale (es. 'heart_rate').")]
        public string sineChannelId = "heart_rate";
        public float sineMin = 60f;
        public float sineMax = 120f;
        public float sineSpeed = 1f;

        [Tooltip("Canale simulato tramite oscillazioni casuali (es. 'spo2').")]
        public string noiseChannelId = "spo2";
        public float noiseMin = 95f;
        public float noiseMax = 99f;

        [Tooltip("Canale simulato per l'indice di perfusione (es. 'perfusion_index').")]
        public string perfusionChannelId = "perfusion_index";
        public float perfusionMin = 2f;
        public float perfusionMax = 7f;

        [Header("HRV Simulation Settings (Clinical Stress Biomarker)")]
        [Tooltip("Canale simulato per HRV RMSSD (variabilità cardiaca a breve termine, sensibile allo stress, in ms).")]
        public string hrvRmssdChannelId = "hrv_rmssd";
        public float hrvRmssdMin = 65f;
        public float hrvRmssdMax = 85f;

        [Tooltip("Canale simulato per HRV SDNN (variabilità complessiva del sistema nervoso autonomo, in ms).")]
        public string hrvSdnnChannelId = "hrv_sdnn";
        public float hrvSdnnMin = 70f;
        public float hrvSdnnMax = 95f;

        // Target di interpolazione per transizioni fluide tra scenari
        private float targetHeartRate = 75f;
        private float targetSineMin = 60f;
        private float targetSineMax = 120f;
        private float targetSineSpeed = 1f;
        private float targetNoiseMin = 95f;
        private float targetNoiseMax = 99f;
        private float targetPerfusionMin = 2f;
        private float targetPerfusionMax = 7f;
        private float targetHrvRmssdMin = 65f;
        private float targetHrvRmssdMax = 85f;
        private float targetHrvSdnnMin = 70f;
        private float targetHrvSdnnMax = 95f;

        private bool isStreaming;

        void Awake()
        {
            if (autoStart)
            {
                StartStreaming();
            }
        }

        void Start()
        {
            if (autoStart)
            {
                StartStreaming();
            }
        }

        void OnEnable()
        {
            if (autoStart)
            {
                StartStreaming();
            }
        }

        void OnDisable()
        {
            StopStreaming();
        }

        public void StartStreaming()
        {
            isStreaming = true;
            Debug.Log("[DataStreamSimulatorSource] Simulatore avviato.");
        }

        public void StopStreaming()
        {
            isStreaming = false;
            Debug.Log("[DataStreamSimulatorSource] Simulatore arrestato.");
        }

        public void SetScenario(SimulationScenario scenario)
        {
            currentScenario = scenario;
            switch (scenario)
            {
                case SimulationScenario.Calma:
                    targetHeartRate = 60f;
                    targetSineMin = 55f;
                    targetSineMax = 65f;
                    targetSineSpeed = 0.8f;
                    targetNoiseMin = 98f;
                    targetNoiseMax = 99.5f;
                    targetPerfusionMin = 5.5f;
                    targetPerfusionMax = 8.0f;
                    targetHrvRmssdMin = 65f;
                    targetHrvRmssdMax = 85f;
                    targetHrvSdnnMin = 70f;
                    targetHrvSdnnMax = 95f;
                    break;
                case SimulationScenario.Stress:
                    targetHeartRate = 135f;
                    targetSineMin = 120f;
                    targetSineMax = 150f;
                    targetSineSpeed = 2.4f;
                    targetNoiseMin = 93.5f;
                    targetNoiseMax = 96f;
                    targetPerfusionMin = 1.0f;
                    targetPerfusionMax = 2.2f;
                    targetHrvRmssdMin = 12f;
                    targetHrvRmssdMax = 22f;
                    targetHrvSdnnMin = 18f;
                    targetHrvSdnnMax = 30f;
                    break;
                case SimulationScenario.Ipossia:
                    targetHeartRate = 85f;
                    targetSineMin = 75f;
                    targetSineMax = 95f;
                    targetSineSpeed = 1.0f;
                    targetNoiseMin = 82f;
                    targetNoiseMax = 88f;
                    targetPerfusionMin = 0.8f;
                    targetPerfusionMax = 1.5f;
                    targetHrvRmssdMin = 25f;
                    targetHrvRmssdMax = 40f;
                    targetHrvSdnnMin = 30f;
                    targetHrvSdnnMax = 45f;
                    break;
                case SimulationScenario.Manual:
                    break;
            }
        }

        private double lastEditorTime;

        void Update()
        {
            // Auto-recovery: se autoStart è attivo ma lo streaming si è interrotto (es. domain reload, enter play mode), riavvialo
            if (!isStreaming && autoStart)
            {
                StartStreaming();
            }

            if (!isStreaming || DataStreamRegistry.Instance == null) return;

#if UNITY_EDITOR
            double currentEditorTime = UnityEditor.EditorApplication.timeSinceStartup;
            float t = Application.isPlaying ? Time.time : (float)currentEditorTime;
            float dt = Application.isPlaying ? Time.deltaTime : (float)(currentEditorTime - lastEditorTime);
            if (dt <= 0f || dt > 1f) dt = 0.016f; // Fallback per il primo frame
            lastEditorTime = currentEditorTime;
#else
            float t = Time.time;
            float dt = Time.deltaTime;
#endif

            // Interpolazione fluida tra scenari
            if (currentScenario != SimulationScenario.Manual)
            {
                float lerpSpeed = dt * 1.5f; // Transizione completa in circa 2.5 secondi
                simulatedHeartRate = Mathf.Lerp(simulatedHeartRate, targetHeartRate, lerpSpeed);
                sineMin = Mathf.Lerp(sineMin, targetSineMin, lerpSpeed);
                sineMax = Mathf.Lerp(sineMax, targetSineMax, lerpSpeed);
                sineSpeed = Mathf.Lerp(sineSpeed, targetSineSpeed, lerpSpeed);
                noiseMin = Mathf.Lerp(noiseMin, targetNoiseMin, lerpSpeed);
                noiseMax = Mathf.Lerp(noiseMax, targetNoiseMax, lerpSpeed);
                perfusionMin = Mathf.Lerp(perfusionMin, targetPerfusionMin, lerpSpeed);
                perfusionMax = Mathf.Lerp(perfusionMax, targetPerfusionMax, lerpSpeed);
                hrvRmssdMin = Mathf.Lerp(hrvRmssdMin, targetHrvRmssdMin, lerpSpeed);
                hrvRmssdMax = Mathf.Lerp(hrvRmssdMax, targetHrvRmssdMax, lerpSpeed);
                hrvSdnnMin = Mathf.Lerp(hrvSdnnMin, targetHrvSdnnMin, lerpSpeed);
                hrvSdnnMax = Mathf.Lerp(hrvSdnnMax, targetHrvSdnnMax, lerpSpeed);
            }

            // 1. Simulazione Canale Sinusoidale (es: Heart Rate che fluttua gradualmente)
            float sineVal = Mathf.Lerp(sineMin, sineMax, (Mathf.Sin(t * sineSpeed) + 1f) * 0.5f);
            DataStreamRegistry.Instance.PushValue(sineChannelId, sineVal);

            // 2. Simulazione Canale Rumoroso (es: SpO2 con piccole oscillazioni)
            float noiseVal = Random.Range(noiseMin, noiseMax);
            DataStreamRegistry.Instance.PushValue(noiseChannelId, noiseVal);

            // 3. Simulazione Indice di Perfusione
            float perfusionVal = Mathf.Lerp(perfusionMin, perfusionMax, Mathf.PingPong(t * 0.5f, 1f));
            DataStreamRegistry.Instance.PushValue(perfusionChannelId, perfusionVal);

            // 4. Simulazione Biomarcatori Clinici dello Stress (HRV RMSSD & SDNN)
            float hrvRmssdVal = Mathf.Lerp(hrvRmssdMin, hrvRmssdMax, Mathf.PerlinNoise(t * 0.4f, 13.37f));
            DataStreamRegistry.Instance.PushValue(hrvRmssdChannelId, hrvRmssdVal);

            float hrvSdnnVal = Mathf.Lerp(hrvSdnnMin, hrvSdnnMax, Mathf.PerlinNoise(t * 0.2f, 42.42f));
            DataStreamRegistry.Instance.PushValue(hrvSdnnChannelId, hrvSdnnVal);

            // 5. Simulazione Evento Discreto (Battito R-Wave / Heartbeat Trigger)
            float effectiveBPM = simulatedHeartRate > 0f ? simulatedHeartRate : 75f;
            float heartbeatInterval = 60f / effectiveBPM;
            heartbeatTimer += dt;
            if (heartbeatTimer >= heartbeatInterval)
            {
                heartbeatTimer -= heartbeatInterval;
                DataStreamRegistry.Instance.PushTrigger("heartbeat");
            }
        }

    }
}
