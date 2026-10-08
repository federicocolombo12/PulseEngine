namespace GenericDataStreaming
{
    /// <summary>
    /// Interfaccia che definisce il comportamento di una sorgente dati generica (es: BLE, OSC, Serial, Mock).
    /// </summary>
    public interface IDataStreamSource
    {
        /// <summary>
        /// Nome descrittivo del protocollo o dispositivo (es. 'Checkme Pro BLE', 'Arduino Serial Driver').
        /// </summary>
        string ProtocolName { get; }

        /// <summary>
        /// Indica se la connessione fisica o di rete con la sorgente è attiva.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Avvia l'ascolto o la ricezione dei dati.
        /// </summary>
        void StartStreaming();

        /// <summary>
        /// Interrompe l'ascolto o la ricezione dei dati.
        /// </summary>
        void StopStreaming();
    }
}
