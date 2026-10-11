using System;

namespace GuinoteApp.Network
{
    /// <summary>
    /// Transporte de red intercambiable (Bluetooth local o Relay por Internet). Los eventos se
    /// disparan siempre en el hilo principal de Unity cuando el <see cref="RedManager"/> llama a
    /// <see cref="Despachar"/>, asi se pueden tocar objetos de Unity desde los manejadores.
    /// </summary>
    public interface IRedGuiñoteServicio
    {
        bool EstaConectado { get; }

        event Action<MensajeRedGuiñote> OnMensajeRecibido;

        /// <summary>true = conectado, false = desconectado.</summary>
        event Action<bool> OnConexionCambiada;

        event Action<string> OnError;

        /// <summary>Prepara el servicio (permisos, adaptadores...). Llamar antes de <see cref="Conectar"/>.</summary>
        void Inicializar(string nombreJugador);

        /// <summary>
        /// Abre la conexion. <paramref name="destino"/>: URL de la sala (Internet) o nombre/MAC del
        /// dispositivo emparejado (Bluetooth, ignorado si es anfitrion).
        /// </summary>
        void Conectar(string destino, bool esAnfitrion);

        void EnviarMensaje(MensajeRedGuiñote mensaje);

        void Desconectar();

        /// <summary>Entrega en el hilo principal lo recibido desde hilos de red.</summary>
        void Despachar();
    }
}
