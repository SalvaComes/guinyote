using System;
using UnityEngine;

namespace GuinoteApp.Network
{
    /// <summary>Como se conectan los dispositivos entre si.</summary>
    public enum ModoConexion
    {
        BluetoothLocal,
        InternetRelay
    }

    /// <summary>
    /// Tipo de accion que transporta un <see cref="MensajeRedGuiñote"/>. Los cuatro primeros son los
    /// originales; los demas completan el protocolo anfitrion-autoridad (ver MensajesPartida.cs para
    /// el DTO de cada uno). Anadir siempre al final: el valor numerico viaja por la red.
    /// </summary>
    public enum TipoMensajeRed
    {
        // Anfitrion -> invitados: nueva mano repartida (cada invitado recibe solo sus cartas)
        RepartirCartas,
        // Invitado -> anfitrion: quiere jugar esta carta
        LanzarCarta,
        // Invitado -> anfitrion: respuesta a PedirCante (canta un palo o no)
        HacerCante,
        // Anfitrion -> todos: se resolvio una baza
        FinalizarBaza,

        // Sala
        UnirseSala,          // invitado -> anfitrion
        EstadoSala,          // anfitrion -> todos: quien ocupa cada asiento
        Latido,              // todos: sigo conectado

        // Partida (anfitrion -> invitados)
        JugadaRechazada,
        CartaJugada,
        Robo,
        SincronizarManos,
        CambioPinta,
        CanteRealizado,
        CambioFase,
        PedirCante,
        PedirCambio,
        Marcador,
        FinDeMano,
        Aviso,
        PartidaTerminada,

        // Invitado -> anfitrion: respuesta a PedirCambio
        ResponderCambio
    }

    /// <summary>
    /// Mensaje que viaja entre dispositivos. La carga util (<see cref="payloadJson"/>) es el JSON de
    /// un DTO propio de cada accion, asi el formato de red no depende de las clases del Core.
    /// </summary>
    [Serializable]
    public class MensajeRedGuiñote
    {
        /// <summary>Destino = todos los dispositivos.</summary>
        public const int ATodos = -1;

        /// <summary>Asiento del anfitrion (jugador 0).</summary>
        public const int Anfitrion = 0;

        public TipoMensajeRed tipo;

        /// <summary>Asiento (0-3) del que envia el mensaje; -1 si aun no tiene asiento.</summary>
        public int emisorId;

        /// <summary>Asiento al que va dirigido, o <see cref="ATodos"/>. En el Relay todos reciben todo y cada
        /// dispositivo descarta lo que no es para el; en Bluetooth el anfitrion difunde igual.</summary>
        public int destino = ATodos;

        public string payloadJson;

        public MensajeRedGuiñote() { }

        public MensajeRedGuiñote(TipoMensajeRed tipo, int emisorId, string payloadJson, int destino = ATodos)
        {
            this.tipo = tipo;
            this.emisorId = emisorId;
            this.payloadJson = payloadJson;
            this.destino = destino;
        }

        /// <summary>Crea un mensaje serializando el DTO de la accion a JSON.</summary>
        public static MensajeRedGuiñote Crear<T>(TipoMensajeRed tipo, int emisorId, T payload, int destino = ATodos) =>
            new MensajeRedGuiñote(tipo, emisorId, JsonUtility.ToJson(payload), destino);

        /// <summary>Lee la carga util como el DTO indicado.</summary>
        public T LeerPayload<T>() => JsonUtility.FromJson<T>(payloadJson);

        public string ToJson() => JsonUtility.ToJson(this);

        /// <summary>Devuelve null si el texto no es un mensaje valido.</summary>
        public static MensajeRedGuiñote DesdeJson(string json)
        {
            try { return JsonUtility.FromJson<MensajeRedGuiñote>(json); }
            catch (ArgumentException) { return null; }
        }
    }
}
