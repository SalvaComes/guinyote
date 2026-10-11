using System;
using System.Collections.Concurrent;

namespace GuinoteApp.Network
{
    /// <summary>
    /// Base comun de los servicios de red: los hilos de red encolan sus avisos y el hilo
    /// principal los entrega en <see cref="Despachar"/>.
    /// </summary>
    public abstract class ServicioRedBase : IRedGuiñoteServicio
    {
        private readonly ConcurrentQueue<Action> pendientes = new ConcurrentQueue<Action>();

        public abstract bool EstaConectado { get; }

        public event Action<MensajeRedGuiñote> OnMensajeRecibido;
        public event Action<bool> OnConexionCambiada;
        public event Action<string> OnError;

        public abstract void Inicializar(string nombreJugador);
        public abstract void Conectar(string destino, bool esAnfitrion);
        public abstract void EnviarMensaje(MensajeRedGuiñote mensaje);
        public abstract void Desconectar();

        public void Despachar()
        {
            while (pendientes.TryDequeue(out Action accion))
                accion();
        }

        // Seguros desde cualquier hilo
        protected void NotificarMensaje(MensajeRedGuiñote m) => pendientes.Enqueue(() => OnMensajeRecibido?.Invoke(m));
        protected void NotificarConexion(bool conectado) => pendientes.Enqueue(() => OnConexionCambiada?.Invoke(conectado));
        protected void NotificarError(string error) => pendientes.Enqueue(() => OnError?.Invoke(error));
    }
}
