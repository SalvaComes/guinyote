using System;
using UnityEngine;

namespace GuinoteApp.Network
{
    /// <summary>
    /// Punto unico de acceso a la red. Elige el transporte (Bluetooth local o Relay por Internet),
    /// reenvia sus eventos y entrega los mensajes en el hilo principal.
    /// Singleton que sobrevive a los cambios de escena.
    /// </summary>
    public class RedManager : MonoBehaviour
    {
        public static RedManager Instancia { get; private set; }

        [SerializeField] private ModoConexion modo = ModoConexion.InternetRelay;
        [SerializeField] private string nombreJugador = "Jugador";

        /// <summary>Indice de jugador (0-3) que se pone como emisor en los mensajes de este dispositivo.</summary>
        public int JugadorLocalId { get; set; }

        public ModoConexion Modo => modo;
        public bool EstaConectado => Servicio != null && Servicio.EstaConectado;
        public IRedGuiñoteServicio Servicio { get; private set; }

        public event Action<MensajeRedGuiñote> OnMensajeRecibido;
        public event Action<bool> OnConexionCambiada;
        public event Action<string> OnError;

        private void Awake()
        {
            if (Instancia != null && Instancia != this)
            {
                Destroy(gameObject);
                return;
            }

            Instancia = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            CambiarModo(modo);
        }

        private void Update()
        {
            // Los hilos de red solo encolan; aqui se entregan ya en el hilo principal
            Servicio?.Despachar();
        }

        private void OnDestroy()
        {
            if (Instancia != this) return;
            Desconectar();
            Instancia = null;
        }

        /// <summary>Cambia de transporte. Cierra la conexion actual si la hay.</summary>
        public void CambiarModo(ModoConexion nuevoModo)
        {
            if (Servicio != null)
            {
                Servicio.OnMensajeRecibido -= ReenviarMensaje;
                Servicio.OnConexionCambiada -= ReenviarConexion;
                Servicio.OnError -= ReenviarError;
                Servicio.Desconectar();
            }

            modo = nuevoModo;
            switch (nuevoModo)
            {
                case ModoConexion.BluetoothLocal: Servicio = new BluetoothAndroidServicio(); break;
                default:                          Servicio = new InternetRelayServicio(); break;
            }

            Servicio.OnMensajeRecibido += ReenviarMensaje;
            Servicio.OnConexionCambiada += ReenviarConexion;
            Servicio.OnError += ReenviarError;
            Servicio.Inicializar(nombreJugador);
            Debug.Log($"[RedManager] Modo de conexion: {nuevoModo}");
        }

        /// <summary>
        /// Abre la conexion. Internet: <paramref name="destino"/> es la URL de la sala
        /// (<see cref="InternetRelayServicio.ConstruirUrlSala"/>). Bluetooth: nombre o MAC del movil emparejado.
        /// </summary>
        /// <param name="maxClientes">Solo Bluetooth anfitrion: cuantos dispositivos aceptar (1 a 3).</param>
        public void Conectar(string destino, bool esAnfitrion, int maxClientes = 1)
        {
            if (Servicio is BluetoothAndroidServicio bluetooth)
                bluetooth.MaxClientes = maxClientes;
            Servicio.Conectar(destino, esAnfitrion);
        }

        public void Desconectar() => Servicio?.Desconectar();

        /// <summary>Crea el RedManager en la escena si todavia no hay uno.</summary>
        public static RedManager ObtenerOCrear()
        {
            if (Instancia != null) return Instancia;
            return new GameObject("RedManager").AddComponent<RedManager>();
        }

        /// <summary>Envia una accion con su DTO serializado como carga util.</summary>
        public void Enviar<T>(TipoMensajeRed tipo, T payload) =>
            Servicio.EnviarMensaje(MensajeRedGuiñote.Crear(tipo, JugadorLocalId, payload));

        public void Enviar(MensajeRedGuiñote mensaje) => Servicio.EnviarMensaje(mensaje);

        private void ReenviarMensaje(MensajeRedGuiñote m) => OnMensajeRecibido?.Invoke(m);
        private void ReenviarConexion(bool c) => OnConexionCambiada?.Invoke(c);

        private void ReenviarError(string e)
        {
            Debug.LogWarning($"[RedManager] {e}");
            OnError?.Invoke(e);
        }
    }
}
