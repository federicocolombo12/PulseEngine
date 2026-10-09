using UnityEngine;
using UnityEditor;
using GenericDataStreaming;
using System.Collections.Generic;
using System.Linq;
using MedicalXR.Checkme;

namespace GenericDataStreaming.EditorScripts
{
    public class DAWMixerWindow : EditorWindow
    {
        // ── DAW Theme & Styling ─────────────────────────────────────────────
        public static class DAWTheme
        {
            public static readonly Color ChassisDark    = new Color(0.11f, 0.12f, 0.14f, 1.0f);
            public static readonly Color StripBg        = new Color(0.15f, 0.16f, 0.18f, 1.0f);
            public static readonly Color StripBorder    = new Color(0.22f, 0.24f, 0.27f, 1.0f);
            public static readonly Color InsetDark      = new Color(0.08f, 0.09f, 0.10f, 1.0f);
            public static readonly Color InsetBorder    = new Color(0.18f, 0.20f, 0.23f, 1.0f);
            public static readonly Color RackHeaderBg   = new Color(0.13f, 0.14f, 0.16f, 1.0f);

            // Bio-Signal Hardware Accents
            public static readonly Color CrimsonRed     = new Color(1.0f, 0.23f, 0.23f, 1.0f); // Heart Rate / Pulse
            public static readonly Color ElectricCyan   = new Color(0.0f, 0.88f, 1.0f, 1.0f);  // SpO2
            public static readonly Color AmberGold      = new Color(1.0f, 0.65f, 0.0f, 1.0f);  // Perfusion Index
            public static readonly Color EmeraldZen     = new Color(0.10f, 0.90f, 0.46f, 1.0f); // HRV RMSSD
            public static readonly Color ElectricViolet = new Color(0.85f, 0.35f, 1.0f, 1.0f);  // HRV SDNN
            public static readonly Color CyberYellow    = new Color(1.0f, 0.88f, 0.12f, 1.0f); // Virtual / Modifiers
            public static readonly Color TechBlue       = new Color(0.25f, 0.65f, 0.95f, 1.0f); // Default

            public static Color GetChannelColor(string channelId)
            {
                if (string.IsNullOrEmpty(channelId)) return TechBlue;
                string id = channelId.ToLowerInvariant();
                if (id.Contains("heart") || id.Contains("pulse") || id.Contains("ecg")) return CrimsonRed;
                if (id.Contains("spo2") || id.Contains("oxygen") || id.Contains("oximeter")) return ElectricCyan;
                if (id.Contains("perfusion") || id.Contains("pi")) return AmberGold;
                if (id.Contains("rmssd") || id.Contains("calm") || id.Contains("zen")) return EmeraldZen;
                if (id.Contains("sdnn") || id.Contains("hrv")) return ElectricViolet;
                if (id.Contains("virtual") || id.Contains("sine") || id.Contains("math")) return CyberYellow;
                return TechBlue;
            }
        }

        private Vector2 mixerScrollPos;
        private bool showHelp = false;
        private DataStreamRegistry registry;
        private string newChannelName = "new_channel";

        // Active Preset & Master Top Bar State
        private static string activePresetName = "";
        private int selectedTemplateIndex = 0;
        private bool isRenamingActive = false;
        private string renameBuffer = "";
        private bool focusRenameField = false;

        // Live Waveform Oscilloscope History (48 samples)
        private const int OSCILLOSCOPE_SAMPLES = 48;
        private Dictionary<string, float[]> channelWaveHistory = new Dictionary<string, float[]>();

        // VU Peak-Hold
        private Dictionary<string, float> channelPeakHold = new Dictionary<string, float>();
        private Dictionary<string, double> channelPeakHoldTime = new Dictionary<string, double>();
        
        // Templates
        private List<MedicalXR.RayMarching.SdfSceneTemplate> cachedSceneTemplates;
        private bool showTemplates = false;

        // Sniffer
        private bool showSniffer = true;

        // Benchmark
        private bool showBenchmark = false;
        private List<MedicalXR.RayMarching.BenchmarkResultEntry> cachedBenchmarkResults;

        // Wizard State per channel
        private Dictionary<string, bool> wizardOpen = new Dictionary<string, bool>();
        private Dictionary<string, GameObject> wizardTarget = new Dictionary<string, GameObject>();
        private Dictionary<string, int> wizardCompIdx = new Dictionary<string, int>();
        private Dictionary<string, string> wizardProp = new Dictionary<string, string>();
        private Dictionary<string, bool> wizardIsColor = new Dictionary<string, bool>();
        private Dictionary<int, bool> editingBinder = new Dictionary<int, bool>();

        private class CopiedBinderData
        {
            public bool isColor;
            public GameObject targetGameObject;
            public string targetComponentType;
            public string targetPropertyName;
            public float weightMultiplier;
            public bool useLocalRemap;
            public Vector2 localRemap;
            public Gradient colorGradient;
        }
        private static CopiedBinderData clipboardBinder;

        [MenuItem("MedicalXR/DAW Mixer")]
        public static void ShowWindow()
        {
            var window = GetWindow<DAWMixerWindow>("DAW Mixer");
            window.minSize = new Vector2(800, 600);
            window.Show();
        }

        private void OnInspectorUpdate()
        {
            Repaint();
            if (!Application.isPlaying && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Repaint();
                EditorApplication.QueuePlayerLoopUpdate();
            }
        }

        private void OnEnable()
        {
            activePresetName = EditorPrefs.GetString("MedicalXR_ActivePreset", "");
            FindRegistry();
        }

        private void FindRegistry()
        {
            registry = Object.FindFirstObjectByType<DataStreamRegistry>();
            RefreshTemplates();
        }

        private void RefreshTemplates()
        {
            cachedSceneTemplates = new List<MedicalXR.RayMarching.SdfSceneTemplate>();
            string[] sceneGuids = AssetDatabase.FindAssets("t:SdfSceneTemplate");
            foreach (string guid in sceneGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var st = AssetDatabase.LoadAssetAtPath<MedicalXR.RayMarching.SdfSceneTemplate>(path);
                if (st != null && !cachedSceneTemplates.Contains(st))
                {
                    if (string.IsNullOrEmpty(st.name))
                    {
                        st.name = System.IO.Path.GetFileNameWithoutExtension(path);
                    }
                    cachedSceneTemplates.Add(st);
                }
            }

            if (!string.IsNullOrEmpty(activePresetName) && cachedSceneTemplates != null)
            {
                for (int i = 0; i < cachedSceneTemplates.Count; i++)
                {
                    if (cachedSceneTemplates[i] != null && cachedSceneTemplates[i].name == activePresetName)
                    {
                        selectedTemplateIndex = i;
                        break;
                    }
                }
            }
        }

        private void OnGUI()
        {
            if (registry == null)
            {
                registry = Object.FindFirstObjectByType<DataStreamRegistry>();
            }

            if (registry == null)
            {
                EditorGUILayout.HelpBox("Nessun DataStreamRegistry trovato in scena.", MessageType.Warning);
                if (GUILayout.Button("Create Registry", GUILayout.Height(40)))
                {
                    GameObject go = new GameObject("DataStreamRegistry");
                    Undo.RegisterCreatedObjectUndo(go, "Create Registry");
                    registry = go.AddComponent<DataStreamRegistry>();

                    // Inizializza automaticamente i canali base
                    registry.channels = new List<DataStreamChannel>
                    {
                        new DataStreamChannel { id = "heart_rate", inputRange = new Vector2(40, 180), outputRange = new Vector2(0.5f, 2f), smoothing = 0.3f },
                        new DataStreamChannel { id = "spo2", inputRange = new Vector2(85, 100), outputRange = new Vector2(0, 1), smoothing = 0.5f },
                        new DataStreamChannel { id = "perfusion_index", inputRange = new Vector2(0, 10), outputRange = new Vector2(0, 1), smoothing = 0.5f },
                        new DataStreamChannel { id = "heart_pulse", sourceType = ChannelSourceType.Virtual, filterType = ChannelFilterType.SineWavePulse, sourceChannelId = "heart_rate", inputRange = new Vector2(0, 1), outputRange = new Vector2(0, 1), smoothing = 0f }
                    };
                    EditorUtility.SetDirty(registry);
                    GUIUtility.ExitGUI();
                }
                return;
            }

            DrawMasterHeader();

            EditorGUILayout.Space(10);
            if (showHelp)
            {
                EditorGUILayout.HelpBox(
                    "🎛️ MEDICAL XR DAW MIXER - GUIDA COMPLETA & WORKFLOW\n\n" +
                    "Questa console centralizza l'ingestione di bio-segnali reali o simulati, la loro normalizzazione e il routing diretto verso shader SDF, volumi 3D e parametri di VFX Graph.\n\n" +
                    "1. PROTOCOLLI D'INGRESSO (Barra in alto):\n" +
                    "   • Unity Simulator: Generatore di segnali organici offline (senza hardware connesso).\n" +
                    "   • Scenari Clinici Rapidi: Calma (Parasimpatico), Stress (Arousal), Ipossia (SpO2 Critico) con transizione biologica continua (~2.5s).\n" +
                    "   • OSC / UDP: Ricezione in tempo reale da sensori Checkme / wearable (porta predefinita: 8000).\n" +
                    "   • WebSocket & Seriale COM: Per telemetria clinica di rete o schede Arduino USB.\n\n" +
                    "2. CANALI DATI (Strisce verticali):\n" +
                    "   • Normalizzazione: 'Range IN' cattura la scala biologica reale (es. 40-180 BPM), mentre 'Range OUT' la mappa sullo spazio visivo [0, 1].\n" +
                    "   • Smoothing: Filtro passa-basso temporale indipendente dal framerate per smussare picchi o jitter del sensore.\n" +
                    "   • Canali Virtuali (Insert FX): Possono processare un canale sorgente (es. generare un'onda continua SineWavePulse guidata dalla frequenza del battito).\n\n" +
                    "3. OUTPUT & SMART BINDERS (Sotto ogni canale):\n" +
                    "   • [+ Add Output]: Creazione guidata (Smart Wizard) che rileva automaticamente proprietà float o colori del target selezionato.\n" +
                    "   • [P] Property Binder: Modula parametri scalari (es. 'TurbulenceIntensity' su VisualEffect o 'continuousPulse' su SdfGroupController). Supporta 'Custom Remap' locale per scalare indipendentemente ogni effetto.\n" +
                    "   • [C] Color Binder: Valuta dinamicamente un Gradiente sul valore normalizzato per guidare colori esposti (es. 'Color' su VisualEffect o tinte di materiali).\n\n" +
                    "4. PRESET & TEMPLATE DI SCENA COMPLETI:\n" +
                    "   • Sezione Foldout 'TEMPLATES CLINICI & SCENA': Include configurazioni complete (Calma, Stress, Ipossia) che ripristinano nodi geometrici SDF, volumi Texture3D, risoluzione voxel, canali e tutti i binders associati.\n" +
                    "   • [APPLICA ALLA SCENA]: Carica il preset istantaneamente.\n" +
                    "   • [SOVRASCRIVI]: Salva l'esatto stato corrente della scena all'interno dell'asset selezionato.\n\n" +
                    "5. FX RACK (A destra):\n" +
                    "   • Moduli addizionali per operazioni matematiche tra canali (Math) ed eventi impulsivi istantanei con decadimento esponenziale (Decay Trigger).",
                    MessageType.Info
                );
                EditorGUILayout.Space(5);
            }
            
            mixerScrollPos = EditorGUILayout.BeginScrollView(mixerScrollPos);
            EditorGUILayout.BeginHorizontal();

            // Draw each channel strip
            if (registry.channels != null)
            {
                foreach (var channel in registry.channels.ToList())
                {
                    DrawChannelStrip(channel);
                }
            }

            // Draw Add Channel Strip
            DrawAddChannelStrip();
            
            // Draw FX Rack
            DrawFXRack();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        private bool showMasterRouting = true;

        private void DrawMasterHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            showMasterRouting = EditorGUILayout.Foldout(showMasterRouting, "🎛️ MASTER CONSOLE & HARDWARE ROUTING", true, EditorStyles.foldoutHeader);

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", GUILayout.Width(70))) FindRegistry();
            if (GUILayout.Button("Overlay Info", GUILayout.Width(90))) MedicalXR.EditorScripts.WelcomeCanvasManager.ToggleCanvas();
            GUI.backgroundColor = showBenchmark ? new Color(1f, 0.85f, 0.2f) : Color.white;
            if (GUILayout.Button(showBenchmark ? "Chiudi Benchmark" : "⚡ Benchmark", GUILayout.Width(120))) showBenchmark = !showBenchmark;
            GUI.backgroundColor = showHelp ? Color.cyan : Color.white;
            if (GUILayout.Button(showHelp ? "Chiudi Tutorial" : "? Tutorial", GUILayout.Width(110))) showHelp = !showHelp;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (!showMasterRouting)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.Space(3);
            DrawMasterPresetBar();
            EditorGUILayout.BeginHorizontal();

            // Multi-Protocol Ingestion
            var sim = Object.FindFirstObjectByType<DataStreamSimulatorSource>();
            var osc = Object.FindFirstObjectByType<CheckmeOscReceiver>();
            var ws = Object.FindFirstObjectByType<DataStreamWebSocketSource>();
            var ser = Object.FindFirstObjectByType<DataStreamSerialSource>();

            GUILayout.Label("Protocol:", GUILayout.Width(60));

            // Unity Simulator
            bool isSim = sim != null && sim.isActiveAndEnabled;
            GUI.backgroundColor = isSim ? DAWTheme.EmeraldZen : Color.white;
            if (GUILayout.Button(isSim ? "[ Unity Simulator ]" : "Unity Simulator", EditorStyles.miniButtonLeft, GUILayout.Width(120)))
            {
                if (sim == null && registry != null) sim = Undo.AddComponent<DataStreamSimulatorSource>(registry.gameObject);
                if (sim != null)
                {
                    sim.enabled = true;
                    sim.StartStreaming();
                }
                if (osc != null) osc.enabled = false;
                if (ws != null) ws.enabled = false;
                if (ser != null) ser.enabled = false;
            }

            // OSC / UDP
            bool isOsc = osc != null && osc.isActiveAndEnabled;
            GUI.backgroundColor = isOsc ? DAWTheme.EmeraldZen : Color.white;
            if (GUILayout.Button(isOsc ? "[ OSC Active ]" : "OSC / UDP", EditorStyles.miniButtonMid, GUILayout.Width(85)))
            {
                if (osc == null && registry != null) osc = Undo.AddComponent<CheckmeOscReceiver>(registry.gameObject);
                if (osc != null) osc.enabled = true;
                if (sim != null) sim.enabled = false;
                if (ws != null) ws.enabled = false;
                if (ser != null) ser.enabled = false;
            }

            // WebSocket
            bool isWs = ws != null && ws.isActiveAndEnabled;
            GUI.backgroundColor = isWs ? DAWTheme.EmeraldZen : Color.white;
            if (GUILayout.Button(isWs ? "[ WS Active ]" : "WebSocket", EditorStyles.miniButtonMid, GUILayout.Width(85)))
            {
                if (ws == null && registry != null) ws = Undo.AddComponent<DataStreamWebSocketSource>(registry.gameObject);
                if (ws != null) ws.enabled = true;
                if (sim != null) sim.enabled = false;
                if (osc != null) osc.enabled = false;
                if (ser != null) ser.enabled = false;
            }

            // Serial / COM
            bool isSer = ser != null && ser.isActiveAndEnabled;
            GUI.backgroundColor = isSer ? DAWTheme.EmeraldZen : Color.white;
            if (GUILayout.Button(isSer ? "[ Serial Active ]" : "Serial / COM", EditorStyles.miniButtonRight, GUILayout.Width(90)))
            {
                if (ser == null && registry != null) ser = Undo.AddComponent<DataStreamSerialSource>(registry.gameObject);
                if (ser != null) ser.enabled = true;
                if (sim != null) sim.enabled = false;
                if (osc != null) osc.enabled = false;
                if (ws != null) ws.enabled = false;
            }
            GUI.backgroundColor = Color.white;
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Protocol Subpanels
            if (sim != null && sim.isActiveAndEnabled)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // ── Hardware Macro Pads for Scenari Clinici ─────────────────────
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Scenari Clinici:", EditorStyles.boldLabel, GUILayout.Width(110));
                DrawScenarioMacroPad("CALMA (Parasimpatico)", sim.currentScenario == DataStreamSimulatorSource.SimulationScenario.Calma, DAWTheme.EmeraldZen, () => sim.SetScenario(DataStreamSimulatorSource.SimulationScenario.Calma));
                DrawScenarioMacroPad("STRESS (Arousal)", sim.currentScenario == DataStreamSimulatorSource.SimulationScenario.Stress, DAWTheme.CrimsonRed, () => sim.SetScenario(DataStreamSimulatorSource.SimulationScenario.Stress));
                DrawScenarioMacroPad("IPOSSIA (SpO2 Critico)", sim.currentScenario == DataStreamSimulatorSource.SimulationScenario.Ipossia, DAWTheme.ElectricViolet, () => sim.SetScenario(DataStreamSimulatorSource.SimulationScenario.Ipossia));
                DrawScenarioMacroPad("MANUALE", sim.currentScenario == DataStreamSimulatorSource.SimulationScenario.Manual, DAWTheme.AmberGold, () => sim.SetScenario(DataStreamSimulatorSource.SimulationScenario.Manual), 90);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(6);
                
                // ── 3-Column Rack Hardware Grid for Simulated Values ───────────
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginHorizontal();

                // Module 1: Cardiac & Pulse
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUI.color = DAWTheme.CrimsonRed;
                GUILayout.Label("❤️ CARDIAC & PULSE", EditorStyles.boldLabel);
                GUI.color = Color.white;
                sim.simulatedHeartRate = EditorGUILayout.Slider("Base HR", sim.simulatedHeartRate, 40f, 180f);
                sim.sineMin = EditorGUILayout.Slider("Sine Min", sim.sineMin, 40f, 180f);
                sim.sineMax = EditorGUILayout.Slider("Sine Max", sim.sineMax, 40f, 180f);
                EditorGUILayout.EndVertical();

                // Module 2: Respiratory & Blood Oxygenation
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUI.color = DAWTheme.ElectricCyan;
                GUILayout.Label("💨 RESPIRATORY & SPO2", EditorStyles.boldLabel);
                GUI.color = Color.white;
                sim.noiseMin = EditorGUILayout.Slider("SpO2 Min", sim.noiseMin, 80f, 100f);
                sim.noiseMax = EditorGUILayout.Slider("SpO2 Max", sim.noiseMax, 80f, 100f);
                sim.perfusionMin = EditorGUILayout.Slider("Perfusion Min (%)", sim.perfusionMin, 0.5f, 10f);
                sim.perfusionMax = EditorGUILayout.Slider("Perfusion Max (%)", sim.perfusionMax, 0.5f, 10f);
                EditorGUILayout.EndVertical();

                // Module 3: Autonomic & HRV
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUI.color = DAWTheme.EmeraldZen;
                GUILayout.Label("🌿 AUTONOMIC (HRV)", EditorStyles.boldLabel);
                GUI.color = Color.white;
                sim.hrvRmssdMin = EditorGUILayout.Slider("HRV RMSSD Min (ms)", sim.hrvRmssdMin, 5f, 120f);
                sim.hrvRmssdMax = EditorGUILayout.Slider("HRV RMSSD Max (ms)", sim.hrvRmssdMax, 5f, 120f);
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();

                if (EditorGUI.EndChangeCheck())
                {
                    sim.currentScenario = DataStreamSimulatorSource.SimulationScenario.Manual;
                    EditorUtility.SetDirty(sim);
                }
                EditorGUILayout.EndVertical();
            }
            else if (osc != null && osc.isActiveAndEnabled)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"OSC UDP Receiver: In ascolto su 127.0.0.1:{osc.port}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label(osc.isActiveAndEnabled ? "● In Streaming" : "○ Disconnesso", EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            else if (ws != null && ws.isActiveAndEnabled)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                ws.serverUri = EditorGUILayout.TextField("WebSocket Server URI", ws.serverUri);
                GUI.backgroundColor = ws.IsConnected ? Color.green : Color.white;
                if (GUILayout.Button(ws.IsConnected ? "Disconnect" : "Connect", GUILayout.Width(100)))
                {
                    if (ws.IsConnected) ws.StopStreaming();
                    else ws.StartStreaming();
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            else if (ser != null && ser.isActiveAndEnabled)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
#if !ENABLE_SERIAL_PORTS
                EditorGUILayout.HelpBox("Porta Seriale USB in standby. Per abilitare la lettura diretta da COM/Arduino su profilo .NET Standard, aggiungi 'ENABLE_SERIAL_PORTS' in Player Settings -> Scripting Define Symbols. Per test immediati senza driver, usa 'Unity Simulator' o 'OSC'.", MessageType.Info);
#endif
                EditorGUILayout.BeginHorizontal();
                ser.portName = EditorGUILayout.TextField("Port (COM)", ser.portName, GUILayout.Width(150));
                ser.baudRate = EditorGUILayout.IntField("Baud", ser.baudRate, GUILayout.Width(120));
                GUI.backgroundColor = ser.IsConnected ? Color.green : Color.white;
                if (GUILayout.Button(ser.IsConnected ? "Close Port" : "Open Port", GUILayout.Width(100)))
                {
                    if (ser.IsConnected) ser.StopStreaming();
                    else ser.StartStreaming();
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.Space(5);
            var inboundSignals = registry != null ? registry.GetActiveInboundSignals() : null;
            int totalSignals = inboundSignals != null ? inboundSignals.Count : 0;
            int unmappedCount = inboundSignals != null ? inboundSignals.Count(s => !s.isTrigger && !s.isMappedToChannel && registry.GetChannel(s.signalId) == null) : 0;

            EditorGUILayout.BeginHorizontal();
            string snifferTitle = $"📡 INBOUND SIGNAL SNIFFER & AUTO-DISCOVERY  ({totalSignals} Rilevati, {unmappedCount} Non Mappati)";
            showSniffer = EditorGUILayout.Foldout(showSniffer, snifferTitle, true, EditorStyles.foldoutHeader);

            if (registry != null)
            {
                EditorGUI.BeginChangeCheck();
                registry.autoCreateUnknownChannels = GUILayout.Toggle(registry.autoCreateUnknownChannels, "Auto-Discovery Runtime", "Button", GUILayout.Width(160), GUILayout.Height(18));
                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(registry);
                }

                GUI.backgroundColor = unmappedCount > 0 ? Color.green : Color.gray;
                GUI.enabled = unmappedCount > 0;
                if (GUILayout.Button(unmappedCount > 0 ? $"⚡ Auto-Crea {unmappedCount} Canali" : "Nessun Nuovo Canale", GUILayout.Width(180), GUILayout.Height(18)))
                {
                    int created = registry.AutoCreateAllDiscoveredChannels();
                    EditorUtility.SetDirty(registry);
                    Debug.Log($"[DAW Mixer] Creati con successo {created} canali dai segnali in ingresso!");
                }
                GUI.enabled = true;
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            if (showSniffer)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                if (inboundSignals == null || inboundSignals.Count == 0)
                {
                    EditorGUILayout.HelpBox("In attesa di segnali in ingresso... Avvia lo streaming da una delle sorgenti sopra (Simulatore, OSC, WebSocket, Seriale) per catturare il traffico dati dal vivo.", MessageType.Info);
                }
                else
                {
                    double now = EditorApplication.timeSinceStartup;
                    // Intestazione tabella
                    EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                    GUILayout.Label("Attività", EditorStyles.miniBoldLabel, GUILayout.Width(50));
                    GUILayout.Label("Segnale / Indirizzo", EditorStyles.miniBoldLabel, GUILayout.Width(170));
                    GUILayout.Label("Valore Live", EditorStyles.miniBoldLabel, GUILayout.Width(90));
                    GUILayout.Label("Range Osservato [Min, Max]", EditorStyles.miniBoldLabel, GUILayout.Width(170));
                    GUILayout.Label("Pacchetti", EditorStyles.miniBoldLabel, GUILayout.Width(80));
                    GUILayout.Label("Mappatura Canale DAW", EditorStyles.miniBoldLabel, GUILayout.Width(140));
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Azione", EditorStyles.miniBoldLabel, GUILayout.Width(80));
                    EditorGUILayout.EndHorizontal();

                    for (int i = 0; i < inboundSignals.Count; i++)
                    {
                        var sig = inboundSignals[i];
                        if (sig == null) continue;

                        bool isChannelMapped = registry.GetChannel(sig.signalId) != null;
                        bool isActiveRecently = (now - sig.lastReceivedTime) < 0.5;

                        EditorGUILayout.BeginHorizontal(i % 2 == 0 ? EditorStyles.textArea : EditorStyles.helpBox);
                        
                        // LED di attività
                        GUI.color = isActiveRecently ? Color.green : new Color(0.4f, 0.4f, 0.4f);
                        GUILayout.Label("●", EditorStyles.boldLabel, GUILayout.Width(45));
                        GUI.color = Color.white;

                        // ID del segnale
                        GUILayout.Label(sig.signalId, EditorStyles.boldLabel, GUILayout.Width(170));

                        // Valore live
                        if (sig.isTrigger)
                        {
                            GUI.color = isActiveRecently ? Color.yellow : Color.gray;
                            GUILayout.Label("[TRIGGER]", EditorStyles.miniBoldLabel, GUILayout.Width(90));
                            GUI.color = Color.white;
                        }
                        else
                        {
                            GUILayout.Label(sig.lastRawValue.ToString("F2"), GUILayout.Width(90));
                        }

                        // Range osservato
                        if (sig.isTrigger)
                        {
                            GUILayout.Label("Impulso discreto", EditorStyles.miniLabel, GUILayout.Width(170));
                        }
                        else
                        {
                            GUILayout.Label($"[{sig.observedMin:F1}  -  {sig.observedMax:F1}]", EditorStyles.miniLabel, GUILayout.Width(170));
                        }

                        // Pacchetti
                        GUILayout.Label(sig.packetCount.ToString(), EditorStyles.miniLabel, GUILayout.Width(80));

                        // Mappatura
                        if (sig.isTrigger)
                        {
                            GUILayout.Label("Trigger Event", EditorStyles.miniLabel, GUILayout.Width(140));
                            GUILayout.FlexibleSpace();
                        }
                        else if (isChannelMapped)
                        {
                            GUI.color = Color.green;
                            GUILayout.Label("✓ MAPPATO", EditorStyles.boldLabel, GUILayout.Width(140));
                            GUI.color = Color.white;
                            GUILayout.FlexibleSpace();
                        }
                        else
                        {
                            GUI.color = new Color(1f, 0.6f, 0.1f);
                            GUILayout.Label("⚠ UNMAPPED", EditorStyles.boldLabel, GUILayout.Width(140));
                            GUI.color = Color.white;
                            GUILayout.FlexibleSpace();
                            GUI.backgroundColor = Color.green;
                            if (GUILayout.Button("+ Crea", EditorStyles.miniButton, GUILayout.Width(75)))
                            {
                                registry.AutoCreateChannel(sig.signalId);
                                EditorUtility.SetDirty(registry);
                            }
                            GUI.backgroundColor = Color.white;
                        }

                        EditorGUILayout.EndHorizontal();
                    }
                }
                EditorGUILayout.EndVertical();
            }
            
            // Templates Section (Secondary Library & Inspector Drawer)
            EditorGUILayout.Space(5);
            showTemplates = EditorGUILayout.Foldout(showTemplates, "📁 LIBRERIA & DETTAGLI TEMPLATE (Scene Inspector)", true, EditorStyles.foldoutHeader);
            if (showTemplates)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Refresh List", GUILayout.Width(100))) RefreshTemplates();
                if (GUILayout.Button("Rigenera Template Base", GUILayout.Width(160)))
                {
                    if (EditorUtility.DisplayDialog("Rigenera Template Base", "ATTENZIONE: Questo comando ripristinerà i 3 template clinici base (Calma, Stress, Ipossia). I file esistenti verranno sovrascritti. Vuoi procedere?", "Sì, Procedi", "Annulla"))
                    {
                        MedicalXR.EditorScripts.GenerateClinicalTemplates.GenerateAll();
                        RefreshTemplates();
                    }
                }
                GUI.backgroundColor = DAWTheme.ElectricCyan;
                if (GUILayout.Button("➕ Salva Scena come Nuovo Template", GUILayout.Height(22))) SaveCurrentSceneAsTemplate();
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                
                // 1. Full Scene Templates
                if (cachedSceneTemplates != null && cachedSceneTemplates.Count > 0)
                {
                    EditorGUILayout.Space(3);
                    GUILayout.Label("Archivio Template di Scena (Nodi, Risoluzione, Canali, Binders e Colori):", EditorStyles.boldLabel);
                    foreach (var st in cachedSceneTemplates.ToArray())
                    {
                        if (st == null) continue;
                        bool isActive = (!string.IsNullOrEmpty(activePresetName) && st.name == activePresetName);

                        if (isActive) GUI.backgroundColor = new Color(0.12f, 0.40f, 0.30f, 1.0f);
                        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                        if (isActive) GUI.backgroundColor = Color.white;

                        // Active Indicator LED
                        if (isActive)
                        {
                            var prevC = GUI.contentColor;
                            GUI.contentColor = DAWTheme.EmeraldZen;
                            GUILayout.Label("● ATTIVO", EditorStyles.boldLabel, GUILayout.Width(70));
                            GUI.contentColor = prevC;
                        }
                        else
                        {
                            GUILayout.Label("○", EditorStyles.miniLabel, GUILayout.Width(20));
                        }

                        GUILayout.Label($"[Preset] {st.name}", EditorStyles.boldLabel, GUILayout.Width(180));
                        GUILayout.Label($"Nodi: {st.nodes.Count} | Res: {st.textureResolution} | Canali: {st.channels.Count} | Binders: {st.propertyBinders.Count + st.colorBinders.Count}", EditorStyles.miniLabel);

                        GUI.backgroundColor = isActive ? DAWTheme.EmeraldZen : Color.green;
                        string applyLabel = isActive ? "✓ ATTIVO (Ricarica)" : "APPLICA ALLA SCENA";
                        if (GUILayout.Button(applyLabel, GUILayout.Width(160), GUILayout.Height(22)))
                        {
                            st.ApplyToScene();
                            activePresetName = st.name;
                            EditorPrefs.SetString("MedicalXR_ActivePreset", activePresetName);
                            if (cachedSceneTemplates != null) selectedTemplateIndex = cachedSceneTemplates.IndexOf(st);
                        }

                        GUI.backgroundColor = new Color(1f, 0.65f, 0.2f);
                        if (GUILayout.Button("SOVRASCRIVI", GUILayout.Width(100), GUILayout.Height(22)))
                        {
                            if (EditorUtility.DisplayDialog("Sovrascrivi Template", $"Vuoi davvero sovrascrivere il template '{st.name}' con lo stato attuale della scena?", "Sì, Sovrascrivi", "Annulla"))
                            {
                                string preservedName = st.name;
                                var snapshot = MedicalXR.RayMarching.SdfSceneTemplate.CaptureCurrentScene(preservedName);
                                EditorUtility.CopySerialized(snapshot, st);
                                st.name = preservedName;
                                EditorUtility.SetDirty(st);
                                AssetDatabase.SaveAssets();
                                activePresetName = preservedName;
                                EditorPrefs.SetString("MedicalXR_ActivePreset", activePresetName);
                                RefreshTemplates();
                                if (cachedSceneTemplates != null) selectedTemplateIndex = cachedSceneTemplates.IndexOf(st);
                                Debug.Log($"[DAW Mixer] Template '{preservedName}' sovrascritto con successo con lo stato attuale della scena!");
                            }
                        }

                        GUI.backgroundColor = new Color(0.85f, 0.25f, 0.25f);
                        if (GUILayout.Button("ELIMINA", GUILayout.Width(75), GUILayout.Height(22)))
                        {
                            DeleteTemplate(st);
                            GUIUtility.ExitGUI();
                        }

                        GUI.backgroundColor = Color.white;
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }

            // -------------------------------------------------------------
            // BENCHMARK & PERFORMANCE PROFILING (Capitolo 5 Tesi)
            // -------------------------------------------------------------
            EditorGUILayout.Space(5);
            showBenchmark = EditorGUILayout.Foldout(showBenchmark, "⚡ BENCHMARK & PERFORMANCE (Tesi Cap. 5 - XR Profiling)", true, EditorStyles.foldoutHeader);
            if (showBenchmark)
            {
                DrawBenchmarkSection();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawScenarioMacroPad(string title, bool isActive, Color activeGlowColor, System.Action onClick, float width = -1)
        {
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = isActive ? activeGlowColor : new Color(0.22f, 0.24f, 0.27f, 1.0f);
            
            var prevContent = GUI.contentColor;
            GUI.contentColor = isActive ? Color.black : Color.white;
            
            GUILayoutOption[] opts = width > 0 
                ? new GUILayoutOption[] { GUILayout.Width(width), GUILayout.Height(24) } 
                : new GUILayoutOption[] { GUILayout.Height(24) };
                
            if (GUILayout.Button(isActive ? $"● {title}" : title, EditorStyles.miniButtonMid, opts))
            {
                onClick?.Invoke();
            }
            
            GUI.contentColor = prevContent;
            GUI.backgroundColor = prevBg;
        }

        private void DrawBenchmarkSection()
        {
            var runner = Object.FindFirstObjectByType<MedicalXR.RayMarching.MedicalXRBenchmarkRunner>();
            var hud = Object.FindFirstObjectByType<MedicalXR.RayMarching.MedicalXRPerformanceHUD>();
            var voxelizer = Object.FindFirstObjectByType<MedicalXR.RayMarching.SdfVolumeVoxelizer>();
            var controller = Object.FindFirstObjectByType<MedicalXR.RayMarching.SdfGroupController>();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 1. Status / Progress
            if (runner != null && runner.isRunning)
            {
                EditorGUILayout.HelpBox($"⚡ BENCHMARK IN CORSO: {runner.currentTestStatus}", MessageType.Warning);
                Rect pRect = GUILayoutUtility.GetRect(18, 22, "TextField");
                EditorGUI.ProgressBar(pRect, runner.progressPercent, $"{runner.currentTestStatus} ({runner.progressPercent * 100f:F0}%)");
                EditorGUILayout.Space(5);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                if (!Application.isPlaying)
                {
                    GUI.backgroundColor = new Color(0.3f, 1f, 0.4f);
                    if (GUILayout.Button("▶ ENTRA IN PLAY MODE ED AVVIA BENCHMARK", GUILayout.Height(30)))
                    {
                        EnsureBenchmarkComponents();
                        MedicalXR.RayMarching.MedicalXRBenchmarkRunner.AutoRunOnPlay = true;
                        EditorApplication.isPlaying = true;
                    }
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.backgroundColor = new Color(0.3f, 1f, 0.4f);
                    if (GUILayout.Button("⚡ AVVIA BENCHMARK COMPLETO (20s)", GUILayout.Height(30)))
                    {
                        EnsureBenchmarkComponents();
                        if (runner == null) runner = Object.FindFirstObjectByType<MedicalXR.RayMarching.MedicalXRBenchmarkRunner>();
                        if (runner != null) runner.StartBenchmark();
                    }
                    GUI.backgroundColor = Color.white;
                }

                if (GUILayout.Button("Setup Componenti in Scena", GUILayout.Width(190), GUILayout.Height(30)))
                {
                    EnsureBenchmarkComponents();
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(5);

            // 2. Quick Live Toggles
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Controlli Live:", EditorStyles.boldLabel, GUILayout.Width(100));

            // HUD Toggle
            if (hud != null)
            {
                GUI.backgroundColor = hud.showHUD ? Color.green : Color.gray;
                if (GUILayout.Button(hud.showHUD ? "Performance HUD: ON [F1]" : "Performance HUD: OFF [F1]", GUILayout.Width(180)))
                {
                    hud.showHUD = !hud.showHUD;
                    EditorUtility.SetDirty(hud);
                }
                GUI.backgroundColor = Color.white;
            }

            // Solid Mesh Raymarch Toggle (Pixel-Fill Decoupling)
            if (controller != null)
            {
                GUI.backgroundColor = controller.renderSolidMesh ? new Color(1f, 0.7f, 0.2f) : Color.cyan;
                string modeText = controller.renderSolidMesh ? "Raymarch Solido: ON [F2]" : "Decoupled Physics ONLY [F2]";
                if (GUILayout.Button(modeText, GUILayout.Width(200)))
                {
                    controller.renderSolidMesh = !controller.renderSolidMesh;
                    EditorUtility.SetDirty(controller);
                }
                GUI.backgroundColor = Color.white;
            }

            // Voxelizer Res cycle
            if (voxelizer != null)
            {
                if (GUILayout.Button($"Voxelizer Res: {voxelizer.textureResolution}³ [F3]", GUILayout.Width(160)))
                {
                    if (hud != null) hud.CycleResolution();
                    else
                    {
                        int[] resList = { 16, 32, 48, 64, 96, 128 };
                        int idx = System.Array.IndexOf(resList, voxelizer.textureResolution);
                        voxelizer.textureResolution = resList[(idx + 1) % resList.Length];
                    }
                    EditorUtility.SetDirty(voxelizer);
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 3. Results Preview & Files
            var results = (runner != null && runner.results != null && runner.results.Count > 0)
                ? runner.results
                : (MedicalXR.RayMarching.MedicalXRBenchmarkRunner.LatestResults.Count > 0
                    ? MedicalXR.RayMarching.MedicalXRBenchmarkRunner.LatestResults
                    : (cachedBenchmarkResults ?? (cachedBenchmarkResults = MedicalXR.RayMarching.MedicalXRBenchmarkRunner.LoadSavedResults())));

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(results != null && results.Count > 0 ? $"Dati Benchmark Disponibili ({results.Count} test archiviati):" : "Nessun dato di benchmark trovato ancora.", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Ricarica Dati", GUILayout.Width(100)))
            {
                cachedBenchmarkResults = MedicalXR.RayMarching.MedicalXRBenchmarkRunner.LoadSavedResults();
            }

            string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
            string parentDir = System.IO.Directory.GetParent(projectRoot).FullName;
            string optFolder = System.IO.Path.Combine(parentDir, "Tesi_SDF_MedicalXR", "Docs", "Optimization");
            string mdPath = System.IO.Path.Combine(optFolder, "Benchmark_Results.md");
            string csvPath = System.IO.Path.Combine(optFolder, "benchmark_data.csv");

            if (System.IO.File.Exists(mdPath))
            {
                GUI.backgroundColor = Color.cyan;
                if (GUILayout.Button("📄 Apri Benchmark_Results.md", GUILayout.Width(200)))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(mdPath) { UseShellExecute = true });
                }
                GUI.backgroundColor = Color.white;
            }

            if (System.IO.File.Exists(csvPath))
            {
                if (GUILayout.Button("📊 Apri benchmark_data.csv", GUILayout.Width(180)))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(csvPath) { UseShellExecute = true });
                }
            }
            EditorGUILayout.EndHorizontal();

            // Preview Table
            if (results != null && results.Count > 0)
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.BeginVertical(GUI.skin.box);
                
                // Header
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Test", EditorStyles.boldLabel, GUILayout.Width(220));
                GUILayout.Label("Risoluzione", EditorStyles.boldLabel, GUILayout.Width(90));
                GUILayout.Label("VRAM", EditorStyles.boldLabel, GUILayout.Width(80));
                GUILayout.Label("Solid Mesh", EditorStyles.boldLabel, GUILayout.Width(100));
                GUILayout.Label("Avg FPS", EditorStyles.boldLabel, GUILayout.Width(80));
                GUILayout.Label("1% Low", EditorStyles.boldLabel, GUILayout.Width(70));
                GUILayout.Label("Frame Time", EditorStyles.boldLabel, GUILayout.Width(90));
                GUILayout.Label("Target XR 72Hz", EditorStyles.boldLabel, GUILayout.Width(110));
                EditorGUILayout.EndHorizontal();

                foreach (var r in results)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(r.testName, GUILayout.Width(220));
                    GUILayout.Label($"{r.resolution}³", GUILayout.Width(90));
                    GUILayout.Label($"{r.vramMb:F2} MB", GUILayout.Width(80));
                    GUILayout.Label(r.solidMeshEnabled ? "ON (Raymarch)" : "OFF (Physics)", GUILayout.Width(100));

                    Color fpsCol = r.avgFps >= 71.5f ? Color.green : (r.avgFps >= 45f ? Color.yellow : Color.red);
                    var prevCol = GUI.contentColor;
                    GUI.contentColor = fpsCol;
                    GUILayout.Label($"{r.avgFps:F1} FPS", EditorStyles.boldLabel, GUILayout.Width(80));
                    GUI.contentColor = prevCol;

                    GUILayout.Label($"{r.onePercentLowFps:F1}", GUILayout.Width(70));
                    GUILayout.Label($"{r.avgFrameTimeMs:F2} ms", GUILayout.Width(90));

                    GUI.contentColor = r.avgFps >= 71.5f ? Color.green : Color.yellow;
                    GUILayout.Label(r.avgFps >= 71.5f ? "✅ Soddisfatto" : "⚠️ Sotto soglia", GUILayout.Width(110));
                    GUI.contentColor = prevCol;

                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
        }

        private void EnsureBenchmarkComponents()
        {
            var voxelizer = Object.FindFirstObjectByType<MedicalXR.RayMarching.SdfVolumeVoxelizer>();
            if (voxelizer != null)
            {
                var go = voxelizer.gameObject;
                if (go.GetComponent<MedicalXR.RayMarching.MedicalXRBenchmarkRunner>() == null)
                {
                    Undo.AddComponent<MedicalXR.RayMarching.MedicalXRBenchmarkRunner>(go);
                    Debug.Log("[DAW Mixer] MedicalXRBenchmarkRunner aggiunto a SDF_Volume.");
                }
                if (go.GetComponent<MedicalXR.RayMarching.MedicalXRPerformanceHUD>() == null)
                {
                    Undo.AddComponent<MedicalXR.RayMarching.MedicalXRPerformanceHUD>(go);
                    Debug.Log("[DAW Mixer] MedicalXRPerformanceHUD aggiunto a SDF_Volume.");
                }
                EditorUtility.SetDirty(go);
            }
            else
            {
                Debug.LogWarning("[DAW Mixer] Impossibile trovare SdfVolumeVoxelizer in scena per associare Benchmark & HUD.");
            }
        }

        private void SaveCurrentSceneAsTemplate()
        {
            string path = EditorUtility.SaveFilePanelInProject("Salva Template di Scena", "NuovoScenario", "asset", "Salva lo stato completo della scena come Template");
            if (string.IsNullOrEmpty(path)) return;

            string templateName = System.IO.Path.GetFileNameWithoutExtension(path);
            var newTemplate = MedicalXR.RayMarching.SdfSceneTemplate.CaptureCurrentScene(templateName);
            newTemplate.name = templateName;

            AssetDatabase.CreateAsset(newTemplate, path);
            EditorUtility.SetDirty(newTemplate);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            RefreshTemplates();

            activePresetName = templateName;
            EditorPrefs.SetString("MedicalXR_ActivePreset", activePresetName);
            if (cachedSceneTemplates != null)
            {
                for (int i = 0; i < cachedSceneTemplates.Count; i++)
                {
                    if (cachedSceneTemplates[i] != null && cachedSceneTemplates[i].name == activePresetName)
                    {
                        selectedTemplateIndex = i;
                        break;
                    }
                }
            }
            Debug.Log($"[DAW Mixer] Template completo salvato e attivato: {path}");
        }

        private void DrawMasterPresetBar()
        {
            if (cachedSceneTemplates == null || cachedSceneTemplates.Count == 0)
            {
                RefreshTemplates();
            }

            int activeIdx = -1;
            if (cachedSceneTemplates != null)
            {
                for (int i = 0; i < cachedSceneTemplates.Count; i++)
                {
                    if (cachedSceneTemplates[i] != null && cachedSceneTemplates[i].name == activePresetName)
                    {
                        activeIdx = i;
                        break;
                    }
                }
            }

            if (cachedSceneTemplates != null && cachedSceneTemplates.Count > 0)
            {
                if (selectedTemplateIndex < 0 || selectedTemplateIndex >= cachedSceneTemplates.Count)
                {
                    selectedTemplateIndex = activeIdx >= 0 ? activeIdx : 0;
                }
            }

            var selectedTemplate = (cachedSceneTemplates != null && cachedSceneTemplates.Count > selectedTemplateIndex)
                ? cachedSceneTemplates[selectedTemplateIndex]
                : null;

            bool isSceneDirty = CheckIfSceneIsDirty(activePresetName);

            // ── MASTER PRESET BAR (Hardware OLED Rack Console) ──────────────
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            // 1. OLED Status Badge
            string statusText;
            Color badgeGlow;
            if (string.IsNullOrEmpty(activePresetName))
            {
                statusText = "IN SCENA: CUSTOM";
                badgeGlow = new Color(0.6f, 0.6f, 0.6f);
            }
            else if (isSceneDirty)
            {
                statusText = $"● IN SCENA: {activePresetName.ToUpper()} * [MODIFICATO]";
                badgeGlow = DAWTheme.AmberGold;
            }
            else
            {
                statusText = $"● IN SCENA: {activePresetName.ToUpper()}";
                badgeGlow = DAWTheme.EmeraldZen;
            }

            Rect badgeRect = GUILayoutUtility.GetRect(new GUIContent($" {statusText} "), EditorStyles.miniBoldLabel, GUILayout.Height(24));
            EditorGUI.DrawRect(badgeRect, DAWTheme.InsetDark);
            EditorGUI.DrawRect(new Rect(badgeRect.x, badgeRect.y, badgeRect.width, 1), badgeGlow);
            EditorGUI.DrawRect(new Rect(badgeRect.x, badgeRect.y + badgeRect.height - 1, badgeRect.width, 1), badgeGlow);
            EditorGUI.DrawRect(new Rect(badgeRect.x, badgeRect.y, 1, badgeRect.height), badgeGlow);
            EditorGUI.DrawRect(new Rect(badgeRect.x + badgeRect.width - 1, badgeRect.y, 1, badgeRect.height), badgeGlow);

            var prevContent = GUI.contentColor;
            GUI.contentColor = badgeGlow;
            GUI.Label(badgeRect, $" {statusText}", EditorStyles.miniBoldLabel);
            GUI.contentColor = prevContent;

            GUILayout.Space(8);

            // 2. Navigation [◀]
            if (GUILayout.Button("◀", EditorStyles.miniButtonLeft, GUILayout.Width(26), GUILayout.Height(24)))
            {
                if (cachedSceneTemplates != null && cachedSceneTemplates.Count > 0)
                {
                    selectedTemplateIndex = (selectedTemplateIndex - 1 + cachedSceneTemplates.Count) % cachedSceneTemplates.Count;
                    isRenamingActive = false;
                }
            }

            // 3. Template Selection & Inline Renaming
            if (selectedTemplate != null)
            {
                if (isRenamingActive)
                {
                    GUI.SetNextControlName("TemplateRenameField");
                    renameBuffer = EditorGUILayout.TextField(renameBuffer, GUILayout.Width(170), GUILayout.Height(24));

                    if (focusRenameField)
                    {
                        EditorGUI.FocusTextInControl("TemplateRenameField");
                        focusRenameField = false;
                    }

                    Event e = Event.current;
                    if (e.isKey && e.type == EventType.KeyDown)
                    {
                        if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                        {
                            ConfirmInlineRename(selectedTemplate, renameBuffer);
                            e.Use();
                        }
                        else if (e.keyCode == KeyCode.Escape)
                        {
                            isRenamingActive = false;
                            e.Use();
                        }
                    }

                    GUI.backgroundColor = DAWTheme.EmeraldZen;
                    if (GUILayout.Button("✓", EditorStyles.miniButtonMid, GUILayout.Width(24), GUILayout.Height(24)))
                    {
                        ConfirmInlineRename(selectedTemplate, renameBuffer);
                    }
                    GUI.backgroundColor = DAWTheme.CrimsonRed;
                    if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(24), GUILayout.Height(24)))
                    {
                        isRenamingActive = false;
                    }
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    string[] templateNames = cachedSceneTemplates.Select(t => {
                        if (t == null) return "<null>";
                        if (!string.IsNullOrEmpty(t.name)) return t.name;
                        string assetP = AssetDatabase.GetAssetPath(t);
                        return !string.IsNullOrEmpty(assetP) ? System.IO.Path.GetFileNameWithoutExtension(assetP) : "Senza Nome";
                    }).ToArray();
                    int newIdx = EditorGUILayout.Popup(selectedTemplateIndex, templateNames, GUILayout.Width(160), GUILayout.Height(24));
                    if (newIdx != selectedTemplateIndex)
                    {
                        selectedTemplateIndex = newIdx;
                        isRenamingActive = false;
                    }

                    GUI.backgroundColor = new Color(0.25f, 0.28f, 0.32f);
                    if (GUILayout.Button("✏️ Rinomina", EditorStyles.miniButtonMid, GUILayout.Width(80), GUILayout.Height(24)))
                    {
                        isRenamingActive = true;
                        renameBuffer = selectedTemplate.name;
                        focusRenameField = true;
                    }
                    GUI.backgroundColor = Color.white;
                }
            }
            else
            {
                GUILayout.Label("(Nessun Template)", EditorStyles.miniLabel, GUILayout.Width(160), GUILayout.Height(24));
            }

            // Navigation [▶]
            if (GUILayout.Button("▶", EditorStyles.miniButtonRight, GUILayout.Width(26), GUILayout.Height(24)))
            {
                if (cachedSceneTemplates != null && cachedSceneTemplates.Count > 0)
                {
                    selectedTemplateIndex = (selectedTemplateIndex + 1) % cachedSceneTemplates.Count;
                    isRenamingActive = false;
                }
            }

            GUILayout.Space(10);

            // 4. Action Buttons
            if (selectedTemplate != null)
            {
                bool isAlreadyActive = (!string.IsNullOrEmpty(activePresetName) && selectedTemplate.name == activePresetName);

                // APPLICA ALLA SCENA
                GUI.backgroundColor = isAlreadyActive ? new Color(0.2f, 0.5f, 0.3f) : DAWTheme.EmeraldZen;
                string applyLabel = isAlreadyActive ? "✓ RICARICA" : "🚀 APPLICA ALLA SCENA";
                if (GUILayout.Button(applyLabel, GUILayout.Height(24), GUILayout.Width(150)))
                {
                    selectedTemplate.ApplyToScene();
                    activePresetName = selectedTemplate.name;
                    EditorPrefs.SetString("MedicalXR_ActivePreset", activePresetName);
                    Debug.Log($"[DAW Mixer] Template '{selectedTemplate.name}' applicato alla scena!");
                }

                // SOVRASCRIVI
                GUI.backgroundColor = isSceneDirty ? DAWTheme.AmberGold : new Color(0.3f, 0.35f, 0.4f);
                if (GUILayout.Button("💾 SOVRASCRIVI", GUILayout.Height(24), GUILayout.Width(110)))
                {
                    if (EditorUtility.DisplayDialog("Sovrascrivi Template", $"Vuoi davvero sovrascrivere il template '{selectedTemplate.name}' con lo stato attuale della scena?", "Sì, Sovrascrivi", "Annulla"))
                    {
                        string preservedName = selectedTemplate.name;
                        var snapshot = MedicalXR.RayMarching.SdfSceneTemplate.CaptureCurrentScene(preservedName);
                        EditorUtility.CopySerialized(snapshot, selectedTemplate);
                        selectedTemplate.name = preservedName;
                        EditorUtility.SetDirty(selectedTemplate);
                        AssetDatabase.SaveAssets();
                        activePresetName = preservedName;
                        EditorPrefs.SetString("MedicalXR_ActivePreset", activePresetName);
                        RefreshTemplates();
                        Debug.Log($"[DAW Mixer] Template '{preservedName}' sovrascritto con successo!");
                    }
                }
                // ELIMINA TEMPLATE CORRENTE
                GUI.backgroundColor = new Color(0.75f, 0.2f, 0.2f);
                if (GUILayout.Button("🗑️ ELIMINA", GUILayout.Height(24), GUILayout.Width(95)))
                {
                    DeleteTemplate(selectedTemplate);
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = Color.white;
            }

            // SALVA COME NUOVO
            GUI.backgroundColor = DAWTheme.ElectricCyan;
            if (GUILayout.Button("➕ SALVA NUOVO", GUILayout.Height(24), GUILayout.Width(120)))
            {
                SaveCurrentSceneAsTemplate();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DeleteTemplate(MedicalXR.RayMarching.SdfSceneTemplate templateToDelete)
        {
            if (templateToDelete == null) return;

            string templateName = templateToDelete.name;
            string assetPath = AssetDatabase.GetAssetPath(templateToDelete);

            if (EditorUtility.DisplayDialog(
                "Elimina Template Definitivamente",
                $"Sei sicuro di voler eliminare definitivamente il template '{templateName}'?\n\nFile: {assetPath}\n\nQuesta operazione non può essere annullata.",
                "Sì, Elimina",
                "Annulla"))
            {
                if (!string.IsNullOrEmpty(assetPath))
                {
                    AssetDatabase.DeleteAsset(assetPath);
                    AssetDatabase.SaveAssets();
                }

                if (activePresetName == templateName)
                {
                    activePresetName = "";
                    EditorPrefs.DeleteKey("MedicalXR_ActivePreset");
                }

                isRenamingActive = false;
                RefreshTemplates();

                if (cachedSceneTemplates != null && cachedSceneTemplates.Count > 0)
                {
                    selectedTemplateIndex = Mathf.Clamp(selectedTemplateIndex, 0, cachedSceneTemplates.Count - 1);
                }
                else
                {
                    selectedTemplateIndex = 0;
                }

                Debug.Log($"[DAW Mixer] Template '{templateName}' eliminato con successo dal disco.");
            }
        }

        private void ConfirmInlineRename(MedicalXR.RayMarching.SdfSceneTemplate template, string newName)
        {
            if (template == null || string.IsNullOrWhiteSpace(newName))
            {
                isRenamingActive = false;
                return;
            }

            string cleanNewName = newName.Trim();
            if (cleanNewName == template.name)
            {
                isRenamingActive = false;
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(template);
            if (!string.IsNullOrEmpty(assetPath))
            {
                string result = AssetDatabase.RenameAsset(assetPath, cleanNewName);
                if (string.IsNullOrEmpty(result))
                {
                    bool wasActive = (activePresetName == template.name);
                    AssetDatabase.SaveAssets();
                    RefreshTemplates();
                    if (wasActive)
                    {
                        activePresetName = cleanNewName;
                        EditorPrefs.SetString("MedicalXR_ActivePreset", activePresetName);
                    }
                    if (cachedSceneTemplates != null)
                    {
                        for (int i = 0; i < cachedSceneTemplates.Count; i++)
                        {
                            if (cachedSceneTemplates[i] != null && cachedSceneTemplates[i].name == cleanNewName)
                            {
                                selectedTemplateIndex = i;
                                break;
                            }
                        }
                    }
                    Debug.Log($"[DAW Mixer] Template rinominato in: {cleanNewName}");
                }
                else
                {
                    Debug.LogWarning($"[DAW Mixer] Errore durante la rinomina: {result}");
                }
            }
            isRenamingActive = false;
        }

        private bool CheckIfSceneIsDirty(string presetName)
        {
            if (string.IsNullOrEmpty(presetName) || cachedSceneTemplates == null) return false;
            var t = cachedSceneTemplates.Find(x => x != null && x.name == presetName);
            if (t == null) return false;

            var controller = Object.FindFirstObjectByType<MedicalXR.RayMarching.SdfGroupController>();
            var voxelizer = Object.FindFirstObjectByType<MedicalXR.RayMarching.SdfVolumeVoxelizer>();
            if (voxelizer != null && voxelizer.textureResolution != t.textureResolution) return true;

            int sceneNodesCount = 0;
            if (controller != null)
            {
                var nodes = controller.GetComponentsInChildren<MedicalXR.RayMarching.SdfNode>();
                sceneNodesCount = nodes != null ? nodes.Length : 0;
            }
            if (sceneNodesCount != t.nodes.Count) return true;

            if (registry != null && registry.channels != null)
            {
                if (registry.channels.Count != t.channels.Count) return true;
            }

            return false;
        }

        private void DrawChannelStrip(DataStreamChannel channel)
        {
            Color accentColor = DAWTheme.GetChannelColor(channel.id);
            bool isMuted = channel.isMuted;
            bool isSolo = channel.isSolo;
            bool anySoloActive = registry != null && registry.channels != null && registry.channels.Any(c => c != null && c.isSolo);
            bool isSilenced = isMuted || (anySoloActive && !isSolo);

            // Channel Strip Outer Container (Ableton/SSL style)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(290), GUILayout.ExpandHeight(true));
            
            // ── 1. Color Ribbon Strip on Top ────────────────────────────────
            Rect ribbonRect = GUILayoutUtility.GetRect(280, 4);
            EditorGUI.DrawRect(ribbonRect, accentColor);
            EditorGUILayout.Space(2);

            // ── 2. Strip Header (Title, Mute, Solo, Remove) ─────────────────
            EditorGUILayout.BeginHorizontal();
            
            // Channel Name with quick rename
            var prevC = GUI.contentColor;
            GUI.contentColor = accentColor;
            string oldId = channel.id;
            string newId = EditorGUILayout.DelayedTextField(oldId.ToUpper(), EditorStyles.boldLabel, GUILayout.Width(140));
            GUI.contentColor = prevC;
            
            if (newId.ToLower() != oldId)
            {
                string cleanNew = newId.ToLower().Trim();
                if (registry.channels.Any(c => c.id == cleanNew && c != channel)) {
                    Debug.LogWarning("Channel ID already exists!");
                } else {
                    Undo.RecordObject(registry, "Rename Channel");
                    channel.id = cleanNew;
                    RenameChannelAcrossSystem(oldId, cleanNew);
                    EditorUtility.SetDirty(registry);
                }
            }

            GUILayout.FlexibleSpace();

            // MUTE Button (M)
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = isMuted ? DAWTheme.CrimsonRed : new Color(0.22f, 0.24f, 0.27f);
            GUI.contentColor = isMuted ? Color.white : Color.gray;
            if (GUILayout.Button("M", EditorStyles.miniButtonLeft, GUILayout.Width(24), GUILayout.Height(18)))
            {
                Undo.RecordObject(registry, "Toggle Mute");
                channel.isMuted = !channel.isMuted;
                EditorUtility.SetDirty(registry);
            }

            // SOLO Button (S)
            GUI.backgroundColor = isSolo ? DAWTheme.CyberYellow : new Color(0.22f, 0.24f, 0.27f);
            GUI.contentColor = isSolo ? Color.black : Color.gray;
            if (GUILayout.Button("S", EditorStyles.miniButtonMid, GUILayout.Width(24), GUILayout.Height(18)))
            {
                Undo.RecordObject(registry, "Toggle Solo");
                channel.isSolo = !channel.isSolo;
                EditorUtility.SetDirty(registry);
            }

            // Remove Channel Button (✕)
            GUI.backgroundColor = new Color(0.22f, 0.24f, 0.27f);
            GUI.contentColor = Color.gray;
            if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(22), GUILayout.Height(18)))
            {
                if (EditorUtility.DisplayDialog("Elimina Canale", $"Vuoi davvero eliminare il canale '{channel.id}'?", "Elimina", "Annulla"))
                {
                    Undo.RecordObject(registry, "Remove Channel");
                    registry.channels.Remove(channel);
                    EditorUtility.SetDirty(registry);
                    GUIUtility.ExitGUI();
                }
            }
            GUI.backgroundColor = prevBg;
            GUI.contentColor = prevC;

            EditorGUILayout.EndHorizontal();

            if (isSilenced)
            {
                EditorGUILayout.HelpBox(isMuted ? "🔇 CANALE IN MUTE" : "🔇 MUTED DA ALTRO SOLO", MessageType.None);
            }

            EditorGUILayout.Space(4);

            // ── 3. Live Waveform Mini-Oscilloscope ──────────────────────────
            float normVal = isSilenced ? 0f : Mathf.InverseLerp(channel.outputRange.x, channel.outputRange.y, channel.Value);
            Rect oscRect = GUILayoutUtility.GetRect(280, 42);
            DrawMiniOscilloscope(oscRect, channel.id, isSilenced ? new Color(0.35f, 0.35f, 0.35f) : accentColor, normVal);

            EditorGUILayout.Space(4);

            // ── 4. Digital OLED Readout & Multi-Segment LED VU Meter ────────
            EditorGUILayout.BeginHorizontal();
            
            // OLED Digital Readout
            Rect oledRect = GUILayoutUtility.GetRect(130, 18);
            EditorGUI.DrawRect(oledRect, DAWTheme.InsetDark);
            EditorGUI.DrawRect(new Rect(oledRect.x, oledRect.y, oledRect.width, 1), DAWTheme.InsetBorder);
            EditorGUI.DrawRect(new Rect(oledRect.x, oledRect.y + oledRect.height - 1, oledRect.width, 1), DAWTheme.InsetBorder);
            EditorGUI.DrawRect(new Rect(oledRect.x, oledRect.y, 1, oledRect.height), DAWTheme.InsetBorder);
            EditorGUI.DrawRect(new Rect(oledRect.x + oledRect.width - 1, oledRect.y, 1, oledRect.height), DAWTheme.InsetBorder);
            
            GUI.contentColor = new Color(0.7f, 0.75f, 0.8f);
            GUI.Label(new Rect(oledRect.x + 4, oledRect.y + 1, oledRect.width - 8, 16), $"RAW: {channel.RawValue:F2}", EditorStyles.miniLabel);
            GUI.contentColor = prevC;

            // Normalized Badge / Muted Badge
            Rect normBadgeRect = GUILayoutUtility.GetRect(130, 18);
            EditorGUI.DrawRect(normBadgeRect, DAWTheme.InsetDark);
            Color normBorder = isSilenced ? DAWTheme.CrimsonRed : accentColor;
            EditorGUI.DrawRect(new Rect(normBadgeRect.x, normBadgeRect.y, normBadgeRect.width, 1), normBorder);
            EditorGUI.DrawRect(new Rect(normBadgeRect.x, normBadgeRect.y + normBadgeRect.height - 1, normBadgeRect.width, 1), normBorder);
            EditorGUI.DrawRect(new Rect(normBadgeRect.x, normBadgeRect.y, 1, normBadgeRect.height), normBorder);
            EditorGUI.DrawRect(new Rect(normBadgeRect.x + normBadgeRect.width - 1, normBadgeRect.y, 1, normBadgeRect.height), normBorder);
            
            if (isSilenced)
            {
                GUI.contentColor = DAWTheme.CrimsonRed;
                GUI.Label(new Rect(normBadgeRect.x + 4, normBadgeRect.y + 1, normBadgeRect.width - 8, 16), "MUTED", EditorStyles.miniBoldLabel);
            }
            else
            {
                GUI.contentColor = accentColor;
                GUI.Label(new Rect(normBadgeRect.x + 4, normBadgeRect.y + 1, normBadgeRect.width - 8, 16), $"NORM: {normVal:F2}", EditorStyles.miniBoldLabel);
            }
            GUI.contentColor = prevC;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);
            Rect vuRect = GUILayoutUtility.GetRect(280, 14);
            DrawLEDVUMeter(vuRect, isSilenced ? 0f : normVal, channel.RawValue, accentColor, channel.id);

            EditorGUILayout.Space(6);

            // ── 5. Ingestion Routing & Source Type ──────────────────────────
            Undo.RecordObject(registry, "Modify Channel");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Source:", EditorStyles.miniBoldLabel, GUILayout.Width(55));
            channel.sourceType = (ChannelSourceType)EditorGUILayout.EnumPopup(channel.sourceType);
            EditorGUILayout.EndHorizontal();

            if (channel.sourceType == ChannelSourceType.Virtual)
            {
                var otherChannels = registry.channels.Where(c => c != channel).Select(c => c.id).ToArray();
                if (otherChannels.Length > 0)
                {
                    int idx = Mathf.Max(0, System.Array.IndexOf(otherChannels, channel.sourceChannelId));
                    idx = EditorGUILayout.Popup("Source Ch", idx, otherChannels);
                    channel.sourceChannelId = otherChannels[idx];
                }
                channel.filterType = (ChannelFilterType)EditorGUILayout.EnumPopup("Filter FX", channel.filterType);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // ── 6. Normalization Trim (Input & Output Range) ────────────────
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("INPUT TRIM & NORMALIZATION", EditorStyles.miniBoldLabel);
            
            bool disableInput = (channel.sourceType == ChannelSourceType.Virtual && channel.filterType == ChannelFilterType.SineWavePulse);
            if (disableInput) {
                EditorGUILayout.HelpBox("Input bloccato a 0-1 (Onda Normalizzata).", MessageType.None);
            }
            
            EditorGUI.BeginDisabledGroup(disableInput);
            channel.inputRange = EditorGUILayout.Vector2Field("Input Range", channel.inputRange);
            EditorGUI.EndDisabledGroup();
            
            channel.outputRange = EditorGUILayout.Vector2Field("Output Range", channel.outputRange);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Smoothing", EditorStyles.miniLabel, GUILayout.Width(70));
            channel.smoothing = EditorGUILayout.Slider(channel.smoothing, 0f, 0.999f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── 7. FX Inserts (Binders Routing Rack) ────────────────────────
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("FX INSERTS (ROUTING)", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent(wizardOpen.ContainsKey(channel.id) && wizardOpen[channel.id] ? "- Chiudi" : "+ Add Insert", "Aggiungi un nuovo Binder in uscita."), EditorStyles.miniButton))
            {
                if (!wizardOpen.ContainsKey(channel.id)) wizardOpen[channel.id] = false;
                wizardOpen[channel.id] = !wizardOpen[channel.id];
            }
            if (clipboardBinder != null)
            {
                if (GUILayout.Button(new GUIContent("📋 Paste", "Incolla il Binder copiato."), EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    PasteBinder(channel.id);
                }
            }
            EditorGUILayout.EndHorizontal();

            var allProps = Object.FindObjectsByType<DataStreamPropertyBinder>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(b => b.channelId == channel.id).ToList();
            var allColors = Object.FindObjectsByType<DataStreamColorBinder>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(b => b.channelId == channel.id).ToList();

            if (allProps.Count == 0 && allColors.Count == 0)
            {
                EditorGUILayout.HelpBox("Nessun Insert collegato.", MessageType.None);
            }
            else
            {
                // Property Binders
                foreach (var p in allProps)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();
                    
                    // LED Bypass Toggle
                    var prevLED = GUI.backgroundColor;
                    GUI.backgroundColor = p.isEnabled ? DAWTheme.EmeraldZen : new Color(0.28f, 0.30f, 0.34f);
                    if (GUILayout.Button(p.isEnabled ? "●" : "○", EditorStyles.miniButton, GUILayout.Width(22), GUILayout.Height(18)))
                    {
                        Undo.RecordObject(p, "Toggle Binder");
                        p.isEnabled = !p.isEnabled;
                        EditorUtility.SetDirty(p);
                    }
                    GUI.backgroundColor = prevLED;

                    // Binder Label
                    GUI.contentColor = DAWTheme.ElectricCyan;
                    GUILayout.Label($"[P] {p.targetPropertyName}", EditorStyles.boldLabel, GUILayout.Width(110));
                    GUI.contentColor = prevC;

                    int id = p.GetHashCode();
                    if (!editingBinder.ContainsKey(id)) editingBinder[id] = false;
                    if (GUILayout.Button(new GUIContent(editingBinder[id] ? "▼" : "⚙", "Impostazioni avanzate"), EditorStyles.miniButton, GUILayout.Width(24))) editingBinder[id] = !editingBinder[id];
                    if (GUILayout.Button(new GUIContent("Ping", "Seleziona l'oggetto target"), EditorStyles.miniButton, GUILayout.Width(35))) EditorGUIUtility.PingObject(p.gameObject);
                    if (GUILayout.Button(new GUIContent("⧉", "Copia impostazioni"), EditorStyles.miniButton, GUILayout.Width(20))) { CopyPropertyBinder(p); }
                    if (GUILayout.Button(new GUIContent("✕", "Elimina Binder"), EditorStyles.miniButton, GUILayout.Width(20))) { Undo.DestroyObjectImmediate(p); GUIUtility.ExitGUI(); }
                    EditorGUILayout.EndHorizontal();

                    Undo.RecordObject(p, "Change Binder");
                    if (editingBinder[id])
                    {
                        GameObject go = p.targetComponent != null ? p.targetComponent.gameObject : null;
                        go = (GameObject)EditorGUILayout.ObjectField("Target", go, typeof(GameObject), true);
                        if (go != null)
                        {
                            var comps = go.GetComponents<Component>().Where(x => x != null && !(x is DataStreamPropertyBinder) && !(x is DataStreamColorBinder)).ToArray();
                            string[] cNames = comps.Select(x => x.GetType().Name).ToArray();
                            int cIdx = Mathf.Max(0, System.Array.IndexOf(comps, p.targetComponent));
                            if (comps.Length > 0)
                            {
                                cIdx = EditorGUILayout.Popup("Component", cIdx, cNames);
                                p.targetComponent = comps[cIdx];
                                string[] props = DataStreamEditorUtils.GetPropertiesForComponent(p.targetComponent, false);
                                int pIdx = Mathf.Max(0, System.Array.IndexOf(props, p.targetPropertyName));
                                if (props.Length > 0)
                                {
                                    pIdx = EditorGUILayout.Popup("Prop", pIdx, props);
                                    p.targetPropertyName = props[pIdx];
                                }
                            }
                        }
                        
                        EditorGUILayout.BeginHorizontal();
                        p.useLocalRemap = EditorGUILayout.ToggleLeft("Remap", p.useLocalRemap, GUILayout.Width(65));
                        if (p.useLocalRemap) {
                            p.localRemap = EditorGUILayout.Vector2Field("", p.localRemap);
                        } else {
                            p.weightMultiplier = EditorGUILayout.FloatField("Weight", p.weightMultiplier);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    if (GUI.changed) { EditorUtility.SetDirty(p); p.InitializeBindings(); }
                    EditorGUILayout.EndVertical();
                }

                // Color Binders
                foreach (var c in allColors)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();
                    
                    // LED Bypass Toggle
                    var prevLED = GUI.backgroundColor;
                    GUI.backgroundColor = c.isEnabled ? DAWTheme.ElectricViolet : new Color(0.28f, 0.30f, 0.34f);
                    if (GUILayout.Button(c.isEnabled ? "●" : "○", EditorStyles.miniButton, GUILayout.Width(22), GUILayout.Height(18)))
                    {
                        Undo.RecordObject(c, "Toggle Color Binder");
                        c.isEnabled = !c.isEnabled;
                        EditorUtility.SetDirty(c);
                    }
                    GUI.backgroundColor = prevLED;

                    // Binder Label
                    GUI.contentColor = DAWTheme.ElectricViolet;
                    GUILayout.Label($"[C] {c.targetPropertyName}", EditorStyles.boldLabel, GUILayout.Width(110));
                    GUI.contentColor = prevC;

                    int id = c.GetHashCode();
                    if (!editingBinder.ContainsKey(id)) editingBinder[id] = false;
                    if (GUILayout.Button(new GUIContent(editingBinder[id] ? "▼" : "⚙", "Impostazioni avanzate"), EditorStyles.miniButton, GUILayout.Width(24))) editingBinder[id] = !editingBinder[id];
                    if (GUILayout.Button(new GUIContent("Ping", "Seleziona l'oggetto target"), EditorStyles.miniButton, GUILayout.Width(35))) EditorGUIUtility.PingObject(c.gameObject);
                    if (GUILayout.Button(new GUIContent("⧉", "Copia impostazioni"), EditorStyles.miniButton, GUILayout.Width(20))) { CopyColorBinder(c); }
                    if (GUILayout.Button(new GUIContent("✕", "Elimina Binder"), EditorStyles.miniButton, GUILayout.Width(20))) { Undo.DestroyObjectImmediate(c); GUIUtility.ExitGUI(); }
                    EditorGUILayout.EndHorizontal();

                    Undo.RecordObject(c, "Change Binder");
                    if (editingBinder[id])
                    {
                        GameObject go = c.targetComponent != null ? c.targetComponent.gameObject : null;
                        go = (GameObject)EditorGUILayout.ObjectField("Target", go, typeof(GameObject), true);
                        if (go != null)
                        {
                            var comps = go.GetComponents<Component>().Where(x => x != null && !(x is DataStreamPropertyBinder) && !(x is DataStreamColorBinder)).ToArray();
                            string[] cNames = comps.Select(x => x.GetType().Name).ToArray();
                            int cIdx = Mathf.Max(0, System.Array.IndexOf(comps, c.targetComponent));
                            if (comps.Length > 0)
                            {
                                cIdx = EditorGUILayout.Popup("Component", cIdx, cNames);
                                c.targetComponent = comps[cIdx];
                                string[] props = DataStreamEditorUtils.GetPropertiesForComponent(c.targetComponent, true);
                                int pIdx = Mathf.Max(0, System.Array.IndexOf(props, c.targetPropertyName));
                                if (props.Length > 0)
                                {
                                    pIdx = EditorGUILayout.Popup("Prop", pIdx, props);
                                    c.targetPropertyName = props[pIdx];
                                }
                            }
                        }
                        if (GUI.changed) c.InitializeBindings();
                    }
                    
                    var so = new SerializedObject(c);
                    var sp = so.FindProperty("colorGradient");
                    if (sp != null) {
                        EditorGUILayout.PropertyField(sp, GUIContent.none);
                        if (so.ApplyModifiedProperties()) {
                            c.InitializeBindings();
                        }
                    }
                    EditorGUILayout.EndVertical();
                }
            }

            if (wizardOpen.ContainsKey(channel.id) && wizardOpen[channel.id])
            {
                DrawWizardForChannel(channel.id);
            }

            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        private void RecordWaveform(string channelId, float val)
        {
            if (!channelWaveHistory.ContainsKey(channelId))
            {
                channelWaveHistory[channelId] = new float[OSCILLOSCOPE_SAMPLES];
            }
            float[] hist = channelWaveHistory[channelId];
            System.Array.Copy(hist, 1, hist, 0, OSCILLOSCOPE_SAMPLES - 1);
            hist[OSCILLOSCOPE_SAMPLES - 1] = Mathf.Clamp01(val);
        }

        private void DrawMiniOscilloscope(Rect rect, string channelId, Color accentColor, float currentNormVal)
        {
            RecordWaveform(channelId, currentNormVal);
            
            // Inset Background & Borders
            EditorGUI.DrawRect(rect, DAWTheme.InsetDark);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), DAWTheme.InsetBorder);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height - 1, rect.width, 1), DAWTheme.InsetBorder);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), DAWTheme.InsetBorder);
            EditorGUI.DrawRect(new Rect(rect.x + rect.width - 1, rect.y, 1, rect.height), DAWTheme.InsetBorder);

            // Center subtle grid reference line
            EditorGUI.DrawRect(new Rect(rect.x + 2, rect.y + rect.height * 0.5f, rect.width - 4, 1), new Color(0.18f, 0.20f, 0.24f, 0.5f));

            if (!channelWaveHistory.ContainsKey(channelId)) return;
            float[] hist = channelWaveHistory[channelId];

            Vector3[] points = new Vector3[OSCILLOSCOPE_SAMPLES];
            for (int i = 0; i < OSCILLOSCOPE_SAMPLES; i++)
            {
                float px = rect.x + 2f + (i / (float)(OSCILLOSCOPE_SAMPLES - 1)) * (rect.width - 4f);
                float py = rect.y + rect.height - 3f - hist[i] * (rect.height - 6f);
                points[i] = new Vector3(px, py, 0f);
            }

            Handles.color = accentColor;
            Handles.DrawAAPolyLine(2.0f, points);

            // Glowing sample head dot
            Vector3 lastPt = points[OSCILLOSCOPE_SAMPLES - 1];
            EditorGUI.DrawRect(new Rect(lastPt.x - 2, lastPt.y - 2, 4, 4), Color.white);
            
            var prevC = GUI.contentColor;
            GUI.contentColor = new Color(0.5f, 0.55f, 0.6f);
            GUI.Label(new Rect(rect.x + 4, rect.y + 2, 80, 14), "WAVEFORM", EditorStyles.miniLabel);
            GUI.contentColor = prevC;
        }

        private void DrawLEDVUMeter(Rect rect, float normVal, float rawVal, Color accentColor, string channelId)
        {
            normVal = Mathf.Clamp01(normVal);
            double now = EditorApplication.timeSinceStartup;
            if (!channelPeakHold.ContainsKey(channelId)) channelPeakHold[channelId] = 0f;
            if (!channelPeakHoldTime.ContainsKey(channelId)) channelPeakHoldTime[channelId] = now;

            if (normVal >= channelPeakHold[channelId])
            {
                channelPeakHold[channelId] = normVal;
                channelPeakHoldTime[channelId] = now;
            }
            else if (now - channelPeakHoldTime[channelId] > 0.8) // Decay after 0.8s
            {
                channelPeakHold[channelId] = Mathf.MoveTowards(channelPeakHold[channelId], normVal, 0.03f);
            }

            // Trench Background
            EditorGUI.DrawRect(rect, DAWTheme.InsetDark);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), DAWTheme.InsetBorder);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height - 1, rect.width, 1), DAWTheme.InsetBorder);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), DAWTheme.InsetBorder);
            EditorGUI.DrawRect(new Rect(rect.x + rect.width - 1, rect.y, 1, rect.height), DAWTheme.InsetBorder);

            // Segmented LEDs (22 segments)
            int totalSegments = 22;
            float pad = 1.5f;
            float segWidth = (rect.width - (totalSegments + 1) * pad) / totalSegments;
            float segHeight = rect.height - 4f;

            for (int i = 0; i < totalSegments; i++)
            {
                float segThreshold = (i + 1) / (float)totalSegments;
                float segX = rect.x + pad + i * (segWidth + pad);
                float segY = rect.y + 2f;
                Rect segRect = new Rect(segX, segY, segWidth, segHeight);

                Color segCol;
                if (segThreshold <= 0.65f)
                    segCol = new Color(0.12f, 0.85f, 0.38f); // Green
                else if (segThreshold <= 0.85f)
                    segCol = new Color(0.98f, 0.78f, 0.15f); // Yellow
                else
                    segCol = new Color(0.95f, 0.22f, 0.22f); // Red (Peak)

                bool isLit = normVal >= segThreshold;
                Color drawCol = isLit ? segCol : new Color(segCol.r * 0.18f, segCol.g * 0.18f, segCol.b * 0.18f, 0.35f);
                EditorGUI.DrawRect(segRect, drawCol);
            }

            // Peak Hold Marker
            float peakFrac = Mathf.Clamp01(channelPeakHold[channelId]);
            float peakX = rect.x + pad + peakFrac * (rect.width - 2 * pad);
            EditorGUI.DrawRect(new Rect(peakX - 1f, rect.y + 1f, 2f, rect.height - 2f), Color.white);
        }

        private void RenameChannelAcrossSystem(string oldId, string newId)
        {
            var props = Object.FindObjectsByType<DataStreamPropertyBinder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var p in props) if (p.channelId == oldId) { Undo.RecordObject(p, "Rename"); p.channelId = newId; EditorUtility.SetDirty(p); }
            
            var colors = Object.FindObjectsByType<DataStreamColorBinder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in colors) if (c.channelId == oldId) { Undo.RecordObject(c, "Rename"); c.channelId = newId; EditorUtility.SetDirty(c); }

            var maths = Object.FindObjectsByType<GenericDataStreaming.Modifiers.DataStreamMathModifier>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var m in maths) {
                if (m.sourceChannelId == oldId) { Undo.RecordObject(m, "Rename"); m.sourceChannelId = newId; EditorUtility.SetDirty(m); }
                if (m.targetChannelId == oldId) { Undo.RecordObject(m, "Rename"); m.targetChannelId = newId; EditorUtility.SetDirty(m); }
            }

            var decays = Object.FindObjectsByType<GenericDataStreaming.Modifiers.DataStreamDecayTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var d in decays) if (d.outputChannelId == oldId) { Undo.RecordObject(d, "Rename"); d.outputChannelId = newId; EditorUtility.SetDirty(d); }

            var metros = Object.FindObjectsByType<GenericDataStreaming.Modifiers.DataStreamMetronome>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var m in metros) if (m.bpmChannelId == oldId) { Undo.RecordObject(m, "Rename"); m.bpmChannelId = newId; EditorUtility.SetDirty(m); }
        }

        private void DrawWizardForChannel(string chId)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            if (!wizardTarget.ContainsKey(chId)) wizardTarget[chId] = null;
            if (!wizardCompIdx.ContainsKey(chId)) wizardCompIdx[chId] = 0;
            if (!wizardProp.ContainsKey(chId)) wizardProp[chId] = "";
            if (!wizardIsColor.ContainsKey(chId)) wizardIsColor[chId] = false;

            GameObject target = (GameObject)EditorGUILayout.ObjectField("Target Object", wizardTarget[chId], typeof(GameObject), true);
            wizardTarget[chId] = target;

            if (target != null)
            {
                var comps = target.GetComponents<Component>().Where(c => c != null && !(c is DataStreamPropertyBinder) && !(c is DataStreamColorBinder)).ToArray();
                string[] compNames = comps.Select(c => c.GetType().Name).ToArray();
                
                if (comps.Length > 0)
                {
                    wizardCompIdx[chId] = EditorGUILayout.Popup("Component", Mathf.Clamp(wizardCompIdx[chId], 0, comps.Length - 1), compNames);
                    Component selectedComp = comps[wizardCompIdx[chId]];
                    
                    wizardIsColor[chId] = EditorGUILayout.Toggle("Is Color Property?", wizardIsColor[chId]);
                    
                    string[] props = DataStreamEditorUtils.GetPropertiesForComponent(selectedComp, wizardIsColor[chId]);
                    if (props.Length > 0)
                    {
                        int propIdx = Mathf.Max(0, System.Array.IndexOf(props, wizardProp[chId]));
                        propIdx = EditorGUILayout.Popup("Property", propIdx, props);
                        wizardProp[chId] = props[propIdx];
                        
                        if (GUILayout.Button("=> CREATE BINDER <=", GUILayout.Height(30)))
                        {
                            if (wizardIsColor[chId])
                            {
                                var b = target.AddComponent<DataStreamColorBinder>();
                                b.channelId = chId;
                                b.targetComponent = selectedComp;
                                b.targetPropertyName = wizardProp[chId];
                                b.InitializeBindings();
                            }
                            else
                            {
                                var b = target.AddComponent<DataStreamPropertyBinder>();
                                b.channelId = chId;
                                b.targetComponent = selectedComp;
                                b.targetPropertyName = wizardProp[chId];
                                b.InitializeBindings();
                            }
                            wizardOpen[chId] = false;
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("No valid properties found.", MessageType.Info);
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAddChannelStrip()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Width(200));
            GUILayout.Label("ADD CHANNEL", EditorStyles.boldLabel);
            if (showHelp) EditorGUILayout.HelpBox("Digita il nome del nuovo canale (es. 'pressione') e crealo come Canale Esterno (da OSC) o Virtuale (generatore).", MessageType.None);
            newChannelName = EditorGUILayout.TextField(newChannelName);
            EditorGUILayout.Space(10);
            if (GUILayout.Button("Load Base Channels", GUILayout.Height(30)))
            {
                Undo.RecordObject(registry, "Load Base Channels");
                if (registry.channels == null) registry.channels = new List<DataStreamChannel>();
                if (!registry.channels.Any(c => c.id == "heart_rate")) registry.channels.Add(new DataStreamChannel { id = "heart_rate", inputRange = new Vector2(40, 180), outputRange = new Vector2(0.5f, 2f) });
                if (!registry.channels.Any(c => c.id == "spo2")) registry.channels.Add(new DataStreamChannel { id = "spo2", inputRange = new Vector2(85, 100), outputRange = new Vector2(0, 1) });
                if (!registry.channels.Any(c => c.id == "perfusion_index")) registry.channels.Add(new DataStreamChannel { id = "perfusion_index", inputRange = new Vector2(0, 10), outputRange = new Vector2(0, 1) });
                if (!registry.channels.Any(c => c.id == "heart_pulse")) registry.channels.Add(new DataStreamChannel { id = "heart_pulse", inputRange = new Vector2(0, 1), outputRange = new Vector2(0, 1) });
                EditorUtility.SetDirty(registry);
            }
            EditorGUILayout.Space(10);

            if (GUILayout.Button("Create", GUILayout.Height(30)))
            {
                if (registry.channels != null && registry.channels.Any(c => c.id == newChannelName)) {
                    Debug.LogWarning("Channel already exists!");
                } else {
                    Undo.RecordObject(registry, "Add Channel");
                    if (registry.channels == null) registry.channels = new List<DataStreamChannel>();
                    registry.channels.Add(new DataStreamChannel { id = newChannelName });
                    EditorUtility.SetDirty(registry);
                    newChannelName = "new_channel_" + UnityEngine.Random.Range(100,999);
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawFXRack()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Width(250), GUILayout.ExpandHeight(true));
            GUILayout.Label("FX RACK & TRIGGERS", EditorStyles.boldLabel);
            if (showHelp) EditorGUILayout.HelpBox("RACK: Modificatori avanzati che creano ponti tra canali o gestiscono trigger (es. DecayTrigger per i picchi).", MessageType.Info);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Math")) Undo.AddComponent<GenericDataStreaming.Modifiers.DataStreamMathModifier>(GetOrCreateModifiersContainer());
            if (GUILayout.Button("+ Decay")) Undo.AddComponent<GenericDataStreaming.Modifiers.DataStreamDecayTrigger>(GetOrCreateModifiersContainer());
            if (GUILayout.Button("+ Metronome")) Undo.AddComponent<GenericDataStreaming.Modifiers.DataStreamMetronome>(GetOrCreateModifiersContainer());
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            
            string[] channelOptions = registry.channels.Select(c => c.id).ToArray();
            var triggerNames = registry.registeredTriggers != null ? registry.registeredTriggers.ToList() : new List<string>();
            triggerNames.Add("new_trigger"); // fallback
            string[] triggerOptions = triggerNames.ToArray();

            var mathMods = Object.FindObjectsByType<GenericDataStreaming.Modifiers.DataStreamMathModifier>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var m in mathMods)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Math Mod", EditorStyles.boldLabel);
                if (GUILayout.Button("X", GUILayout.Width(20))) { Undo.DestroyObjectImmediate(m); GUIUtility.ExitGUI(); }
                EditorGUILayout.EndHorizontal();
                
                Undo.RecordObject(m, "Change Math Mod");
                
                int srcIdx = Mathf.Max(0, System.Array.IndexOf(channelOptions, m.sourceChannelId));
                srcIdx = EditorGUILayout.Popup("Source", srcIdx, channelOptions);
                if (channelOptions.Length > 0) m.sourceChannelId = channelOptions[srcIdx];

                m.operation = (GenericDataStreaming.Modifiers.MathOperation)EditorGUILayout.EnumPopup("Op", m.operation);
                m.operand = EditorGUILayout.FloatField("Value", m.operand);

                int tgtIdx = Mathf.Max(0, System.Array.IndexOf(channelOptions, m.targetChannelId));
                tgtIdx = EditorGUILayout.Popup("Target", tgtIdx, channelOptions);
                if (channelOptions.Length > 0) m.targetChannelId = channelOptions[tgtIdx];
                
                if (GUI.changed) EditorUtility.SetDirty(m);
                EditorGUILayout.EndVertical();
            }

            var decayMods = Object.FindObjectsByType<GenericDataStreaming.Modifiers.DataStreamDecayTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var d in decayMods)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Decay Trigger", EditorStyles.boldLabel);
                if (GUILayout.Button("X", GUILayout.Width(20))) { Undo.DestroyObjectImmediate(d); GUIUtility.ExitGUI(); }
                EditorGUILayout.EndHorizontal();
                
                Undo.RecordObject(d, "Change Decay");
                
                int trgIdx = Mathf.Max(0, System.Array.IndexOf(triggerOptions, d.triggerId));
                trgIdx = EditorGUILayout.Popup("Trigger", trgIdx, triggerOptions);
                d.triggerId = triggerOptions[trgIdx];

                d.decaySpeed = EditorGUILayout.FloatField("Decay Speed", d.decaySpeed);

                int outIdx = Mathf.Max(0, System.Array.IndexOf(channelOptions, d.outputChannelId));
                outIdx = EditorGUILayout.Popup("Output", outIdx, channelOptions);
                if (channelOptions.Length > 0) d.outputChannelId = channelOptions[outIdx];
                
                if (GUI.changed) EditorUtility.SetDirty(d);
                EditorGUILayout.EndVertical();
            }

            var metroMods = Object.FindObjectsByType<GenericDataStreaming.Modifiers.DataStreamMetronome>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var m in metroMods)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Metronome", EditorStyles.boldLabel);
                if (GUILayout.Button("X", GUILayout.Width(20))) { Undo.DestroyObjectImmediate(m); GUIUtility.ExitGUI(); }
                EditorGUILayout.EndHorizontal();
                
                Undo.RecordObject(m, "Change Metronome");
                
                int bpmIdx = Mathf.Max(0, System.Array.IndexOf(channelOptions, m.bpmChannelId));
                bpmIdx = EditorGUILayout.Popup("BPM Ch", bpmIdx, channelOptions);
                if (channelOptions.Length > 0) m.bpmChannelId = channelOptions[bpmIdx];

                m.fallbackBPM = EditorGUILayout.FloatField("Fallback BPM", m.fallbackBPM);

                int outIdx = Mathf.Max(0, System.Array.IndexOf(triggerOptions, m.outputTriggerId));
                outIdx = EditorGUILayout.Popup("Trigger", outIdx, triggerOptions);
                m.outputTriggerId = triggerOptions[outIdx];
                
                if (GUI.changed) EditorUtility.SetDirty(m);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
        }

        private GameObject GetOrCreateModifiersContainer()
        {
            GameObject container = GameObject.Find("DataStream_Modifiers");
            if (container == null)
            {
                container = new GameObject("DataStream_Modifiers");
                Undo.RegisterCreatedObjectUndo(container, "Create Modifiers Container");
            }
            return container;
        }

        private void CopyPropertyBinder(DataStreamPropertyBinder p)
        {
            clipboardBinder = new CopiedBinderData
            {
                isColor = false,
                targetGameObject = p.gameObject,
                targetComponentType = p.targetComponent != null ? p.targetComponent.GetType().FullName : "",
                targetPropertyName = p.targetPropertyName,
                weightMultiplier = p.weightMultiplier,
                useLocalRemap = p.useLocalRemap,
                localRemap = p.localRemap
            };
        }

        private void CopyColorBinder(DataStreamColorBinder c)
        {
            clipboardBinder = new CopiedBinderData
            {
                isColor = true,
                targetGameObject = c.gameObject,
                targetComponentType = c.targetComponent != null ? c.targetComponent.GetType().FullName : "",
                targetPropertyName = c.targetPropertyName,
                colorGradient = c.colorGradient
            };
        }

        private void PasteBinder(string channelId)
        {
            if (clipboardBinder == null || clipboardBinder.targetGameObject == null) return;
            GameObject targetGo = clipboardBinder.targetGameObject;
            
            Component targetComp = null;
            if (!string.IsNullOrEmpty(clipboardBinder.targetComponentType))
                targetComp = targetGo.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType().FullName == clipboardBinder.targetComponentType);

            if (clipboardBinder.isColor)
            {
                var newBinder = Undo.AddComponent<DataStreamColorBinder>(targetGo);
                newBinder.channelId = channelId;
                newBinder.targetComponent = targetComp;
                newBinder.targetPropertyName = clipboardBinder.targetPropertyName;
                if (clipboardBinder.colorGradient != null)
                {
                    newBinder.colorGradient = new Gradient();
                    newBinder.colorGradient.SetKeys(clipboardBinder.colorGradient.colorKeys, clipboardBinder.colorGradient.alphaKeys);
                    newBinder.colorGradient.mode = clipboardBinder.colorGradient.mode;
                }
                newBinder.InitializeBindings();
            }
            else
            {
                var newBinder = Undo.AddComponent<DataStreamPropertyBinder>(targetGo);
                newBinder.channelId = channelId;
                newBinder.targetComponent = targetComp;
                newBinder.targetPropertyName = clipboardBinder.targetPropertyName;
                newBinder.weightMultiplier = clipboardBinder.weightMultiplier;
                newBinder.useLocalRemap = clipboardBinder.useLocalRemap;
                newBinder.localRemap = clipboardBinder.localRemap;
                newBinder.InitializeBindings();
            }
        }
    }
}
