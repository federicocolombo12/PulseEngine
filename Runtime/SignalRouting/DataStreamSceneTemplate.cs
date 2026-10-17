using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GenericDataStreaming.Modifiers;

namespace GenericDataStreaming
{
    [System.Serializable]
    public class DataStreamPropertyBinderSnapshot
    {
        public string channelId;
        public string targetGameObjectName;
        public string targetComponentType;
        public string targetPropertyName;
        public float weightMultiplier = 1.0f;
        public bool useLocalRemap = false;
        public Vector2 localRemap = new Vector2(0f, 1f);
    }

    [System.Serializable]
    public class DataStreamColorBinderSnapshot
    {
        public string channelId;
        public string targetGameObjectName;
        public string targetComponentType;
        public string targetPropertyName;
        public Gradient colorGradient;
    }

    [System.Serializable]
    public class DataStreamTriggerBinderSnapshot
    {
        public string triggerId;
        public string targetGameObjectName;
        public DataStreamTriggerBinder.TriggerActionType actionType;
        public string vfxEventName;
    }

    [System.Serializable]
    public class DataStreamDecayTriggerSnapshot
    {
        public string targetGameObjectName;
        public string triggerId;
        public string outputChannelId;
        public float maxIntensity = 1.0f;
        public float decaySpeed = 5.0f;
    }

    [System.Serializable]
    public class DataStreamMathModifierSnapshot
    {
        public string targetGameObjectName;
        public string sourceChannelId;
        public string targetChannelId;
        public MathOperation operation;
        public float operand = 90f;
        public Vector2 clampRange = new Vector2(0f, 1f);
    }

    [System.Serializable]
    public class DataStreamMetronomeSnapshot
    {
        public string targetGameObjectName;
        public string bpmChannelId;
        public string outputTriggerId;
        public float fallbackBPM = 60f;
    }

    /// <summary>
    /// Snapshot completo della configurazione di telemetria e routing per PulseEngine:
    /// Canali del DataStreamRegistry, trigger registrati, modificatori (Decay, Math, Metronome)
    /// e tutti i binder (PropertyBinder, ColorBinder, TriggerBinder).
    /// </summary>
    [CreateAssetMenu(fileName = "New DataStream Scene Template", menuName = "MedicalXR/DataStream Scene Template")]
    public class DataStreamSceneTemplate : ScriptableObject
    {
        [Header("DataStream Channels")]
        public List<DataStreamChannel> channels = new List<DataStreamChannel>();

        [Header("Registered Triggers")]
        public List<string> registeredTriggers = new List<string>();

        [Header("Signal Modifiers")]
        public List<DataStreamDecayTriggerSnapshot> decayTriggers = new List<DataStreamDecayTriggerSnapshot>();
        public List<DataStreamMathModifierSnapshot> mathModifiers = new List<DataStreamMathModifierSnapshot>();
        public List<DataStreamMetronomeSnapshot> metronomes = new List<DataStreamMetronomeSnapshot>();

        [Header("DataStream Binders")]
        public List<DataStreamPropertyBinderSnapshot> propertyBinders = new List<DataStreamPropertyBinderSnapshot>();
        public List<DataStreamColorBinderSnapshot> colorBinders = new List<DataStreamColorBinderSnapshot>();
        public List<DataStreamTriggerBinderSnapshot> triggerBinders = new List<DataStreamTriggerBinderSnapshot>();

        /// <summary>
        /// Crea uno snapshot istantaneo della configurazione di routing della scena attiva.
        /// </summary>
        public static DataStreamSceneTemplate CaptureCurrentScene(string templateName = "")
        {
            var template = ScriptableObject.CreateInstance<DataStreamSceneTemplate>();
            if (!string.IsNullOrEmpty(templateName))
            {
                template.name = templateName;
            }

            // 1. DataStreamRegistry Channels & Triggers
            if (DataStreamRegistry.Instance != null)
            {
                if (DataStreamRegistry.Instance.channels != null)
                {
                    foreach (var ch in DataStreamRegistry.Instance.channels)
                    {
                        template.channels.Add(new DataStreamChannel
                        {
                            id = ch.id,
                            sourceType = ch.sourceType,
                            filterType = ch.filterType,
                            sourceChannelId = ch.sourceChannelId,
                            inputRange = ch.inputRange,
                            outputRange = ch.outputRange,
                            smoothing = ch.smoothing,
                            isMuted = ch.isMuted,
                            isSolo = ch.isSolo
                        });
                    }
                }

                if (DataStreamRegistry.Instance.registeredTriggers != null)
                {
                    template.registeredTriggers = new List<string>(DataStreamRegistry.Instance.registeredTriggers);
                }
            }

            // 2. Modifiers
            var decayList = UnityEngine.Object.FindObjectsByType<DataStreamDecayTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var dt in decayList)
            {
                if (dt == null) continue;
                template.decayTriggers.Add(new DataStreamDecayTriggerSnapshot
                {
                    targetGameObjectName = dt.gameObject.name,
                    triggerId = dt.triggerId,
                    outputChannelId = dt.outputChannelId,
                    maxIntensity = dt.maxIntensity,
                    decaySpeed = dt.decaySpeed
                });
            }

            var mathList = UnityEngine.Object.FindObjectsByType<DataStreamMathModifier>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var mm in mathList)
            {
                if (mm == null) continue;
                template.mathModifiers.Add(new DataStreamMathModifierSnapshot
                {
                    targetGameObjectName = mm.gameObject.name,
                    sourceChannelId = mm.sourceChannelId,
                    targetChannelId = mm.targetChannelId,
                    operation = mm.operation,
                    operand = mm.operand,
                    clampRange = mm.clampRange
                });
            }

            var metroList = UnityEngine.Object.FindObjectsByType<DataStreamMetronome>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var m in metroList)
            {
                if (m == null) continue;
                template.metronomes.Add(new DataStreamMetronomeSnapshot
                {
                    targetGameObjectName = m.gameObject.name,
                    bpmChannelId = m.bpmChannelId,
                    outputTriggerId = m.outputTriggerId,
                    fallbackBPM = m.fallbackBPM
                });
            }

            // 3. Property Binders
            var pBinders = UnityEngine.Object.FindObjectsByType<DataStreamPropertyBinder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var pb in pBinders)
            {
                if (pb == null || pb.targetComponent == null) continue;
                template.propertyBinders.Add(new DataStreamPropertyBinderSnapshot
                {
                    channelId = pb.channelId,
                    targetGameObjectName = pb.targetComponent.gameObject.name,
                    targetComponentType = pb.targetComponent.GetType().FullName,
                    targetPropertyName = pb.targetPropertyName,
                    weightMultiplier = pb.weightMultiplier,
                    useLocalRemap = pb.useLocalRemap,
                    localRemap = pb.localRemap
                });
            }

            // 4. Color Binders
            var cBinders = UnityEngine.Object.FindObjectsByType<DataStreamColorBinder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cb in cBinders)
            {
                if (cb == null || cb.targetComponent == null) continue;
                template.colorBinders.Add(new DataStreamColorBinderSnapshot
                {
                    channelId = cb.channelId,
                    targetGameObjectName = cb.targetComponent.gameObject.name,
                    targetComponentType = cb.targetComponent.GetType().FullName,
                    targetPropertyName = cb.targetPropertyName,
                    colorGradient = cb.colorGradient
                });
            }

            // 5. Trigger Binders
            var tBinders = UnityEngine.Object.FindObjectsByType<DataStreamTriggerBinder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var tb in tBinders)
            {
                if (tb == null) continue;
                template.triggerBinders.Add(new DataStreamTriggerBinderSnapshot
                {
                    triggerId = tb.triggerId,
                    targetGameObjectName = tb.gameObject.name,
                    actionType = tb.actionType,
                    vfxEventName = tb.vfxEventName
                });
            }

            return template;
        }

        /// <summary>
        /// Applica questo template alla scena corrente aggiornando DataStreamRegistry, Modifiers e Binders.
        /// </summary>
        public void ApplyToScene()
        {
            // 1. DataStreamRegistry
            if (DataStreamRegistry.Instance != null)
            {
#if UNITY_EDITOR
                UnityEditor.Undo.RecordObject(DataStreamRegistry.Instance, "Apply DataStream Template Channels");
#endif
                if (channels != null && channels.Count > 0)
                {
                    DataStreamRegistry.Instance.channels = channels.Select(ch => new DataStreamChannel
                    {
                        id = ch.id,
                        sourceType = ch.sourceType,
                        filterType = ch.filterType,
                        sourceChannelId = ch.sourceChannelId,
                        inputRange = ch.inputRange,
                        outputRange = ch.outputRange,
                        smoothing = ch.smoothing,
                        isMuted = ch.isMuted,
                        isSolo = ch.isSolo
                    }).ToList();
                }

                if (registeredTriggers != null)
                {
                    DataStreamRegistry.Instance.registeredTriggers = new List<string>(registeredTriggers);
                }

                DataStreamRegistry.Instance.RefreshRegistry();
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(DataStreamRegistry.Instance);
#endif
            }

            // 2. Modifiers
            if (decayTriggers != null && decayTriggers.Count > 0)
            {
                foreach (var dtSnap in decayTriggers)
                {
                    var targetGo = GameObject.Find(dtSnap.targetGameObjectName);
                    if (targetGo == null)
                    {
                        targetGo = GameObject.Find("DataStream_Modifiers") ?? (DataStreamRegistry.Instance != null ? DataStreamRegistry.Instance.gameObject : null);
                    }
                    if (targetGo != null)
                    {
                        var comp = targetGo.GetComponent<DataStreamDecayTrigger>();
                        if (comp == null)
                        {
#if UNITY_EDITOR
                            comp = UnityEditor.Undo.AddComponent<DataStreamDecayTrigger>(targetGo);
#else
                            comp = targetGo.AddComponent<DataStreamDecayTrigger>();
#endif
                        }
#if UNITY_EDITOR
                        UnityEditor.Undo.RecordObject(comp, "Apply Decay Trigger Settings");
#endif
                        comp.triggerId = dtSnap.triggerId;
                        comp.outputChannelId = dtSnap.outputChannelId;
                        comp.maxIntensity = dtSnap.maxIntensity;
                        comp.decaySpeed = dtSnap.decaySpeed;
                    }
                }
            }

            if (mathModifiers != null && mathModifiers.Count > 0)
            {
                foreach (var mmSnap in mathModifiers)
                {
                    var targetGo = GameObject.Find(mmSnap.targetGameObjectName);
                    if (targetGo == null)
                    {
                        targetGo = GameObject.Find("DataStream_Modifiers") ?? (DataStreamRegistry.Instance != null ? DataStreamRegistry.Instance.gameObject : null);
                    }
                    if (targetGo != null)
                    {
                        var comp = targetGo.GetComponent<DataStreamMathModifier>();
                        if (comp == null)
                        {
#if UNITY_EDITOR
                            comp = UnityEditor.Undo.AddComponent<DataStreamMathModifier>(targetGo);
#else
                            comp = targetGo.AddComponent<DataStreamMathModifier>();
#endif
                        }
#if UNITY_EDITOR
                        UnityEditor.Undo.RecordObject(comp, "Apply Math Modifier Settings");
#endif
                        comp.sourceChannelId = mmSnap.sourceChannelId;
                        comp.targetChannelId = mmSnap.targetChannelId;
                        comp.operation = mmSnap.operation;
                        comp.operand = mmSnap.operand;
                        comp.clampRange = mmSnap.clampRange;
                    }
                }
            }

            if (metronomes != null && metronomes.Count > 0)
            {
                foreach (var mSnap in metronomes)
                {
                    var targetGo = GameObject.Find(mSnap.targetGameObjectName);
                    if (targetGo == null)
                    {
                        targetGo = GameObject.Find("DataStream_Modifiers") ?? (DataStreamRegistry.Instance != null ? DataStreamRegistry.Instance.gameObject : null);
                    }
                    if (targetGo != null)
                    {
                        var comp = targetGo.GetComponent<DataStreamMetronome>();
                        if (comp == null)
                        {
#if UNITY_EDITOR
                            comp = UnityEditor.Undo.AddComponent<DataStreamMetronome>(targetGo);
#else
                            comp = targetGo.AddComponent<DataStreamMetronome>();
#endif
                        }
#if UNITY_EDITOR
                        UnityEditor.Undo.RecordObject(comp, "Apply Metronome Settings");
#endif
                        comp.bpmChannelId = mSnap.bpmChannelId;
                        comp.outputTriggerId = mSnap.outputTriggerId;
                        comp.fallbackBPM = mSnap.fallbackBPM;
                    }
                }
            }

            // 3. Property Binders
            if (propertyBinders != null && propertyBinders.Count > 0)
            {
                var existingBinders = UnityEngine.Object.FindObjectsByType<DataStreamPropertyBinder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var b in existingBinders)
                {
#if UNITY_EDITOR
                    UnityEditor.Undo.DestroyObjectImmediate(b);
#else
                    UnityEngine.Object.Destroy(b);
#endif
                }

                foreach (var pbSnap in propertyBinders)
                {
                    var targetGo = GameObject.Find(pbSnap.targetGameObjectName);
                    if (targetGo != null)
                    {
                        Component targetComp = null;
                        if (!string.IsNullOrEmpty(pbSnap.targetComponentType))
                        {
                            targetComp = targetGo.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType().FullName == pbSnap.targetComponentType);
                        }
                        if (targetComp == null)
                        {
                            targetComp = targetGo.GetComponent<Renderer>() as Component ?? targetGo.transform;
                        }

#if UNITY_EDITOR
                        var newBinder = UnityEditor.Undo.AddComponent<DataStreamPropertyBinder>(targetGo);
#else
                        var newBinder = targetGo.AddComponent<DataStreamPropertyBinder>();
#endif
                        newBinder.channelId = pbSnap.channelId;
                        newBinder.targetComponent = targetComp;
                        newBinder.targetPropertyName = pbSnap.targetPropertyName;
                        newBinder.weightMultiplier = pbSnap.weightMultiplier;
                        newBinder.useLocalRemap = pbSnap.useLocalRemap;
                        newBinder.localRemap = pbSnap.localRemap;
                        newBinder.InitializeBindings();
                    }
                }
            }

            // 4. Color Binders
            if (colorBinders != null && colorBinders.Count > 0)
            {
                var existingColorBinders = UnityEngine.Object.FindObjectsByType<DataStreamColorBinder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var b in existingColorBinders)
                {
#if UNITY_EDITOR
                    UnityEditor.Undo.DestroyObjectImmediate(b);
#else
                    UnityEngine.Object.Destroy(b);
#endif
                }

                foreach (var cbSnap in colorBinders)
                {
                    var targetGo = GameObject.Find(cbSnap.targetGameObjectName);
                    if (targetGo != null)
                    {
                        Component targetComp = null;
                        if (!string.IsNullOrEmpty(cbSnap.targetComponentType))
                        {
                            targetComp = targetGo.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType().FullName == cbSnap.targetComponentType);
                        }
                        if (targetComp == null)
                        {
                            targetComp = targetGo.GetComponent<Renderer>() as Component ?? targetGo.GetComponent<Light>() as Component;
                        }

#if UNITY_EDITOR
                        var newColorBinder = UnityEditor.Undo.AddComponent<DataStreamColorBinder>(targetGo);
#else
                        var newColorBinder = targetGo.AddComponent<DataStreamColorBinder>();
#endif
                        newColorBinder.channelId = cbSnap.channelId;
                        newColorBinder.targetComponent = targetComp;
                        newColorBinder.targetPropertyName = cbSnap.targetPropertyName;
                        newColorBinder.colorGradient = cbSnap.colorGradient;
                        newColorBinder.InitializeBindings();
                    }
                }
            }

            // 5. Trigger Binders
            if (triggerBinders != null && triggerBinders.Count > 0)
            {
                var existingTriggerBinders = UnityEngine.Object.FindObjectsByType<DataStreamTriggerBinder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var tb in existingTriggerBinders)
                {
#if UNITY_EDITOR
                    UnityEditor.Undo.DestroyObjectImmediate(tb);
#else
                    UnityEngine.Object.Destroy(tb);
#endif
                }

                foreach (var tbSnap in triggerBinders)
                {
                    var targetGo = GameObject.Find(tbSnap.targetGameObjectName);
                    if (targetGo != null)
                    {
#if UNITY_EDITOR
                        var newTriggerBinder = UnityEditor.Undo.AddComponent<DataStreamTriggerBinder>(targetGo);
#else
                        var newTriggerBinder = targetGo.AddComponent<DataStreamTriggerBinder>();
#endif
                        newTriggerBinder.triggerId = tbSnap.triggerId;
                        newTriggerBinder.actionType = tbSnap.actionType;
                        newTriggerBinder.vfxEventName = tbSnap.vfxEventName;
                    }
                }
            }

            Debug.Log($"[DataStreamSceneTemplate] Template '{name}' applicato con successo alla scena!");
        }
    }
}
