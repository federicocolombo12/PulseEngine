# 01 Signal Routing Demo

## Panoramica
Questo sample illustra il funzionamento nativo e plug-and-play di **PulseEngine**:
- Ingestione telemetrica multi-protocollo tramite `DataStreamRegistry` (simulatore organico interno, OSC/UDP, WebSocket, Seriale).
- Normalizzazione e filtri temporali sui canali biologici (`heart_rate`, `spo2`, `heart_pulse`, `is_hypoxic`).
- Modificatori di segnale in tempo reale (`DataStreamDecayTrigger` per convertire battiti discreti in onde di decadimento e `DataStreamMathModifier` per soglie logiche di ipossia).
- Visual Property Binders su componenti standard Unity (MeshRenderer, Transform scale, Light intensity) con remap locale custom e gradienti cromatici.

## Come Usare
1. Apri la scena `SignalRoutingDemo.unity`.
2. Premi **Play** in Unity.
3. Apri **MedicalXR -> Pulse Console** per osservare l'oscilloscopio a 48 campioni, i picchi VU Meter e sperimentare cambiando in tempo reale i parametri dei canali o passando tra gli scenari clinici (Calma, Stress, Ipossia).
