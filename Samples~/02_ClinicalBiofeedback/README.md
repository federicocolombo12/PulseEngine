# 02 Clinical Biofeedback Suite

## Panoramica
Questo sample è il dimostratore di punta che abbina **PulseEngine** al pacchetto companion raccomandato **VolumetricSDF**:
- Modulazione di un volume implicito Signed Distance Field (SDF) tramite parametri biologici.
- Voxelizzazione continua $128^3$ eseguita su Compute Shader GPU per pilotare particelle organiche in Unity VFX Graph (`VfxCheckme`).
- Ingestione da sensore clinico Checkme via OSC o generazione autonoma di scenari di stress fisiologico e ipossia.

## Requisito Companion
Questo sample raccomanda l'installazione del modulo companion:
- **VolumetricSDF Engine**: `https://github.com/federicocolombo12/VolumetricSDF.git`

## Come Usare
1. Assicurati che `com.federicocolombo.volumetricsdf` sia presente nel progetto.
2. Apri la scena `ClinicalBiofeedbackDemo.unity`.
3. Apri **MedicalXR -> Pulse Console** per alternare i 9 scenari clinici (Calma, Stress, Ipossia, BoubaKiki, ecc.) o eseguire benchmark prestazionali XR a 72Hz.
