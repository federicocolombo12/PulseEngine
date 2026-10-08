using System;
using System.Collections.Generic;
using UnityEngine;

namespace GenericDataStreaming
{
    /// <summary>
    /// Informazioni in tempo reale su un segnale rilevato in ingresso dallo sniffer.
    /// </summary>
    [System.Serializable]
    public class InboundSignalInfo
    {
        public string signalId;
        public float lastRawValue;
        public double lastReceivedTime;
        public float observedMin = float.MaxValue;
        public float observedMax = float.MinValue;
        public int packetCount;
        public bool isMappedToChannel;
        public bool isTrigger;
    }

    /// <summary>
    /// Registro centrale e manager per lo streaming dati generico.
    /// Centralizza l'accesso ai canali, ne gestisce l'interpolazione ed espone eventi sia continui che discreti (trigger).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [ExecuteAlways]
    public class DataStreamRegistry : MonoBehaviour
    {
        private static DataStreamRegistry _instance;
        public static DataStreamRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindAnyObjectByType<DataStreamRegistry>();
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Continuous Channels")]
        [Tooltip("Lista dei canali continui registrati per la rimappatura e lo smoothing.")]
        public List<DataStreamChannel> channels = new List<DataStreamChannel>();

        [Header("Discrete Triggers")]
        [Tooltip("Lista di identificativi per eventi istantanei (es. 'heartbeat', 'gasp', 'button_press').")]
        public List<string> registeredTriggers = new List<string>();

        [Header("Auto-Discovery & Signal Ingestion")]
        [Tooltip("Se abilitato, qualsiasi segnale mai visto prima crea istantaneamente un nuovo canale al primo arrivo.")]
        public bool autoCreateUnknownChannels = false;

        private readonly Dictionary<string, InboundSignalInfo> inboundSignals = new Dictionary<string, InboundSignalInfo>();
        private readonly List<InboundSignalInfo> inboundSignalsList = new List<InboundSignalInfo>();

        private Dictionary<string, DataStreamChannel> channelMap = new Dictionary<string, DataStreamChannel>();
        private Dictionary<string, Action> triggerMap = new Dictionary<string, Action>();

        void Awake()
        {
            SetupInstance();
        }

        void OnEnable()
        {
            SetupInstance();
        }

        void OnDisable()
        {
            if (_instance == this && !Application.isPlaying)
            {
                _instance = null;
            }
        }

        void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void SetupInstance()
        {
            if (_instance == null || _instance == this)
            {
                _instance = this;
                InitializeRegistry();
            }
            else if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                // In Edit Mode non distruggere mai il GameObject creato dall'utente
                _instance = this;
                InitializeRegistry();
            }
        }

        public void RefreshRegistry()
        {
            channelMap.Clear();
            if (channels == null) return;
            foreach (var channel in channels)
            {
                if (channel == null || string.IsNullOrEmpty(channel.id)) continue;
                if (!channelMap.ContainsKey(channel.id))
                {
                    channelMap.Add(channel.id, channel);
                }
                else
                {
                    Debug.LogWarning($"[DataStreamRegistry] Rilevato ID canale duplicato: '{channel.id}'");
                }
            }
        }

        private void InitializeRegistry()
        {
            RefreshRegistry();

            triggerMap.Clear();
            foreach (var triggerId in registeredTriggers)
            {
                if (string.IsNullOrEmpty(triggerId)) continue;
                if (!triggerMap.ContainsKey(triggerId))
                {
                    triggerMap.Add(triggerId, null);
                }
            }
        }

        private double lastUpdateTime;

        void Update()
        {
#if UNITY_EDITOR
            double currentTime = UnityEditor.EditorApplication.timeSinceStartup;
            float dt = Application.isPlaying ? Time.deltaTime : (float)(currentTime - lastUpdateTime);
            if (dt <= 0 || dt > 1f) dt = 0.016f;
            lastUpdateTime = currentTime;
#else
            float dt = Time.deltaTime;
#endif
            
            if (dt <= 0 || channels == null) return;

            // Esegue i canali virtuali (Insert FX) prima dell'interpolazione
            for (int i = 0; i < channels.Count; i++)
            {
                var ch = channels[i];
                if (ch != null && ch.sourceType == ChannelSourceType.Virtual)
                {
                    float sourceVal = 0f;
                    float rawSourceVal = 0f;
                    string srcId = string.IsNullOrEmpty(ch.sourceChannelId) ? "heart_rate" : ch.sourceChannelId;
                    var sourceCh = GetChannel(srcId);
                    if (sourceCh != null)
                    {
                        sourceVal = sourceCh.Value;
                        rawSourceVal = sourceCh.RawValue;
                    }
                    ch.ProcessVirtual(sourceVal, rawSourceVal, dt);
                }
            }

            // Esegue l'interpolazione temporale costante per tutti i canali
            for (int i = 0; i < channels.Count; i++)
            {
                if (channels[i] != null)
                {
                    channels[i].Interpolate(dt);
                }
            }
        }

        #region Continuous Channels Interface

        /// <summary>
        /// Inietta un valore grezzo in un canale specifico. Il valore verrà automaticamente rimappato e smorzato.
        /// </summary>
        public void PushValue(string channelId, float rawValue)
        {
            if (string.IsNullOrEmpty(channelId)) return;
            TrackInboundValue(channelId, rawValue);

            if (channels == null) return;
            for (int i = 0; i < channels.Count; i++)
            {
                var ch = channels[i];
                if (ch != null && ch.id == channelId)
                {
                    ch.UpdateValue(rawValue);
                    return;
                }
            }

            // Se autoCreateUnknownChannels è attivo e il canale non è ancora presente
            if (autoCreateUnknownChannels)
            {
                var newCh = AutoCreateChannel(channelId);
                newCh?.UpdateValue(rawValue);
            }
        }

        /// <summary>
        /// Ritorna il valore corrente (mappato ed interpolato) del canale richiesto.
        /// </summary>
        public float GetValue(string channelId, float defaultValue = 0f)
        {
            if (string.IsNullOrEmpty(channelId) || channels == null) return defaultValue;
            for (int i = 0; i < channels.Count; i++)
            {
                var ch = channels[i];
                if (ch != null && ch.id == channelId)
                {
                    return ch.Value;
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Ritorna il valore grezzo (puro) in ingresso per il canale richiesto.
        /// </summary>
        public float GetRawValue(string channelId, float defaultValue = 0f)
        {
            if (string.IsNullOrEmpty(channelId) || channels == null) return defaultValue;
            for (int i = 0; i < channels.Count; i++)
            {
                var ch = channels[i];
                if (ch != null && ch.id == channelId)
                {
                    return ch.RawValue;
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Ottiene la referenza al canale per poter configurare ranges o sottoscrivere eventi programmaticamente.
        /// </summary>
        public DataStreamChannel GetChannel(string channelId)
        {
            if (string.IsNullOrEmpty(channelId) || channels == null) return null;
            for (int i = 0; i < channels.Count; i++)
            {
                var ch = channels[i];
                if (ch != null && ch.id == channelId)
                {
                    return ch;
                }
            }
            return null;
        }

        /// <summary>
        /// Verifica se un canale è silenziato (perché impostato su Mute o perché un altro canale è in Solo).
        /// </summary>
        public bool IsChannelSilenced(string channelId)
        {
            if (string.IsNullOrEmpty(channelId) || channels == null) return false;
            var target = GetChannel(channelId);
            if (target == null) return false;
            if (target.isMuted) return true;

            bool anySolo = false;
            for (int i = 0; i < channels.Count; i++)
            {
                var c = channels[i];
                if (c != null && c.isSolo)
                {
                    anySolo = true;
                    break;
                }
            }

            if (anySolo && !target.isSolo) return true;
            return false;
        }

        #endregion

        #region Discrete Triggers Interface

        /// <summary>
        /// Registra un listener per un trigger specifico.
        /// </summary>
        public void RegisterTriggerListener(string triggerId, Action callback)
        {
            if (triggerMap.ContainsKey(triggerId))
            {
                triggerMap[triggerId] += callback;
            }
            else
            {
                // Crea al volo se non registrato nell'ispettore per flessibilità runtime
                triggerMap.Add(triggerId, callback);
                if (!registeredTriggers.Contains(triggerId))
                {
                    registeredTriggers.Add(triggerId);
                }
            }
        }

        /// <summary>
        /// Rimuove un listener da un trigger specifico.
        /// </summary>
        public void DeregisterTriggerListener(string triggerId, Action callback)
        {
            if (triggerMap.ContainsKey(triggerId))
            {
                triggerMap[triggerId] -= callback;
            }
        }

        /// <summary>
        /// Solleva istantaneamente un evento trigger notificando tutti i sottoscrittori sul thread principale.
        /// </summary>
        public void PushTrigger(string triggerId)
        {
            if (string.IsNullOrEmpty(triggerId)) return;
            TrackInboundTrigger(triggerId);

            if (triggerMap.TryGetValue(triggerId, out var callback))
            {
                callback?.Invoke();
            }
        }

        #endregion

        #region Inbound Traffic Sniffer & Auto-Discovery

        private double GetCurrentTimestamp()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorApplication.timeSinceStartup;
#else
            return Time.timeAsDouble;
#endif
        }

        private void TrackInboundValue(string signalId, float rawValue)
        {
            double now = GetCurrentTimestamp();
            if (!inboundSignals.TryGetValue(signalId, out var info))
            {
                info = new InboundSignalInfo
                {
                    signalId = signalId,
                    lastRawValue = rawValue,
                    lastReceivedTime = now,
                    observedMin = rawValue,
                    observedMax = rawValue,
                    packetCount = 1,
                    isMappedToChannel = GetChannel(signalId) != null,
                    isTrigger = false
                };
                inboundSignals.Add(signalId, info);
                inboundSignalsList.Add(info);
            }
            else
            {
                info.lastRawValue = rawValue;
                info.lastReceivedTime = now;
                if (rawValue < info.observedMin) info.observedMin = rawValue;
                if (rawValue > info.observedMax) info.observedMax = rawValue;
                info.packetCount++;
                info.isMappedToChannel = GetChannel(signalId) != null;
            }
        }

        private void TrackInboundTrigger(string triggerId)
        {
            double now = GetCurrentTimestamp();
            if (!inboundSignals.TryGetValue(triggerId, out var info))
            {
                info = new InboundSignalInfo
                {
                    signalId = triggerId,
                    lastRawValue = 1f,
                    lastReceivedTime = now,
                    observedMin = 0f,
                    observedMax = 1f,
                    packetCount = 1,
                    isMappedToChannel = false,
                    isTrigger = true
                };
                inboundSignals.Add(triggerId, info);
                inboundSignalsList.Add(info);
            }
            else
            {
                info.lastReceivedTime = now;
                info.packetCount++;
            }
        }

        /// <summary>
        /// Ritorna la lista live di tutti i segnali e trigger rilevati dallo sniffer.
        /// </summary>
        public IReadOnlyList<InboundSignalInfo> GetActiveInboundSignals()
        {
            return inboundSignalsList;
        }

        /// <summary>
        /// Crea automaticamente un nuovo canale partendo dall'ID di un segnale rilevato.
        /// Applica preset clinici noti per HR, SpO2, HRV RMSSD/SDNN, PI o auto-calibra per canali custom.
        /// </summary>
        public DataStreamChannel AutoCreateChannel(string signalId)
        {
            if (string.IsNullOrEmpty(signalId)) return null;
            var existing = GetChannel(signalId);
            if (existing != null)
            {
                if (inboundSignals.TryGetValue(signalId, out var sigInfo)) sigInfo.isMappedToChannel = true;
                return existing;
            }

            DataStreamChannel newCh = new DataStreamChannel();
            newCh.id = signalId;
            newCh.sourceType = ChannelSourceType.External;
            newCh.clampOutput = true;
            newCh.outputRange = new Vector2(0f, 1f);

            // Preset clinici noti
            switch (signalId.ToLowerInvariant())
            {
                case "heart_rate":
                case "/checkme/hr":
                    newCh.inputRange = new Vector2(40f, 180f);
                    newCh.smoothing = 0.4f;
                    break;
                case "spo2":
                case "/checkme/spo2":
                    newCh.inputRange = new Vector2(70f, 100f);
                    newCh.smoothing = 0.6f;
                    break;
                case "hrv_rmssd":
                case "rmssd":
                case "/checkme/hrv/rmssd":
                case "/checkme/rmssd":
                    newCh.inputRange = new Vector2(10f, 100f);
                    newCh.smoothing = 0.5f;
                    break;
                case "hrv_sdnn":
                case "sdnn":
                case "/checkme/hrv/sdnn":
                case "/checkme/sdnn":
                    newCh.inputRange = new Vector2(10f, 100f);
                    newCh.smoothing = 0.5f;
                    break;
                case "perfusion_index":
                case "pi":
                case "/checkme/pi":
                    newCh.inputRange = new Vector2(0.5f, 10f);
                    newCh.smoothing = 0.5f;
                    break;
                default:
                    // Sconosciuto / Custom: se abbiamo osservato una variazione, usiamola
                    if (inboundSignals.TryGetValue(signalId, out var info) && info.observedMax > info.observedMin + 0.001f)
                    {
                        newCh.inputRange = new Vector2(info.observedMin, info.observedMax);
                    }
                    else
                    {
                        newCh.inputRange = new Vector2(0f, 100f);
                    }
                    newCh.smoothing = 0.3f;
                    break;
            }

            if (channels == null) channels = new List<DataStreamChannel>();
            channels.Add(newCh);
            RefreshRegistry();

            if (inboundSignals.TryGetValue(signalId, out var sInfo))
            {
                sInfo.isMappedToChannel = true;
            }

            Debug.Log($"[DataStreamRegistry] Canale auto-creato con successo: '{signalId}' [Input: {newCh.inputRange.x}-{newCh.inputRange.y} -> Output: {newCh.outputRange.x}-{newCh.outputRange.y}]");
            return newCh;
        }

        /// <summary>
        /// Auto-crea in blocco tutti i segnali rilevati che non hanno ancora un canale fader.
        /// </summary>
        public int AutoCreateAllDiscoveredChannels()
        {
            int createdCount = 0;
            for (int i = 0; i < inboundSignalsList.Count; i++)
            {
                var sig = inboundSignalsList[i];
                if (sig != null && !sig.isTrigger && !sig.isMappedToChannel)
                {
                    if (AutoCreateChannel(sig.signalId) != null)
                    {
                        createdCount++;
                    }
                }
            }
            return createdCount;
        }

        #endregion
    }
}
