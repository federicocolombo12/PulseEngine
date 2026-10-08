using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.Events;

namespace GenericDataStreaming
{
    /// <summary>
    /// Componente generico per catturare eventi trigger discreti dal DataStreamRegistry (es. 'heartbeat')
    /// e scatenare eventi in VFX Graph o invocare UnityEvent in tempo reale.
    /// </summary>
    [AddComponentMenu("Generic Data Streaming/Data Stream Trigger Binder")]
    public class DataStreamTriggerBinder : MonoBehaviour
    {
        public enum TriggerActionType
        {
            VfxSendEvent,
            UnityEvent
        }

        [Tooltip("L'ID del trigger da ascoltare dal DataStreamRegistry (es. 'heartbeat').")]
        public string triggerId;

        [Tooltip("L'azione da compiere quando si riceve l'evento.")]
        public TriggerActionType actionType = TriggerActionType.UnityEvent;

        [Header("VFX Action Settings")]
        [Tooltip("Il Visual Effect Graph di destinazione.")]
        public VisualEffect targetVfx;
        [Tooltip("Nome esatto dell'evento da sollevare nel VFX Graph (es. 'OnPlay').")]
        public string vfxEventName;

        [Header("Event Action Settings")]
        [Tooltip("L'evento C# o UnityEvent da invocare.")]
        public UnityEvent onTriggered;

        void OnEnable()
        {
            if (DataStreamRegistry.Instance != null && !string.IsNullOrEmpty(triggerId))
            {
                DataStreamRegistry.Instance.RegisterTriggerListener(triggerId, OnTriggerReceived);
            }
        }

        void OnDisable()
        {
            if (DataStreamRegistry.Instance != null && !string.IsNullOrEmpty(triggerId))
            {
                DataStreamRegistry.Instance.DeregisterTriggerListener(triggerId, OnTriggerReceived);
            }
        }

        private void OnTriggerReceived()
        {
            switch (actionType)
            {
                case TriggerActionType.VfxSendEvent:
                    if (targetVfx != null && !string.IsNullOrEmpty(vfxEventName))
                    {
                        targetVfx.SendEvent(vfxEventName);
                    }
                    break;

                case TriggerActionType.UnityEvent:
                    onTriggered?.Invoke();
                    break;
            }
        }
    }
}
