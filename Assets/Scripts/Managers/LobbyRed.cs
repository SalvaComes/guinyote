using System;
using System.Collections.Generic;
using UnityEngine;
using GuinoteApp.Network;
using GuinoteApp.UI;

namespace GuinoteApp.Managers
{
    /// <summary>
    /// Lobby del multijugador: crear una sala (anfitrion) o unirse a una (invitado), por Internet
    /// (Relay) o por Bluetooth, para 2 o 4 jugadores humanos. Esta hecho con IMGUI para no depender
    /// de prefabs ni de la escena: se dibuja encima de la interfaz mientras esta abierto.
    /// Se abre desde el boton "Multijugador" de la pantalla de seleccion (GameManager.AbrirLobbyMultijugador).
    /// </summary>
    public class LobbyRed : MonoBehaviour
    {
        private enum Estado { Cerrado, Menu, ConfigAnfitrion, SalaAnfitrion, ConfigInvitado, SalaInvitado }

        // Servidor Relay del juego (Tools/RelayServer desplegado en Render). Fijo: el jugador no lo ve ni lo configura.
        private const string UrlRelay = "wss://guinyote.onrender.com";

        private const string PrefNombre = "Guinote.NombreJugador";
        private const string AlfabetoCodigo = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        private Estado estado = Estado.Cerrado;

        // Dependencias que pasa GameManager
        private CartaVisualManager cartaVisualManager;
        private HUDMesaDeJuego hudMesa;
        private UIManager uiManager;
        private Func<DificultadIA[]> obtenerNiveles;
        private Action<DificultadIA[], PartidaRedAnfitrion> alEmpezar;

        // Configuracion editable
        private ModoConexion modo = ModoConexion.InternetRelay;
        private int humanos = 2;
        private bool invitadoCompanero = true;
        private string codigoSala = "";
        private string nombre;
        private string dispositivo = "";
        private List<string> emparejados = new List<string>();
        private string mensaje = "";

        // Sesion en curso
        private RedManager red;
        private PartidaRedAnfitrion anfitrion;
        private PartidaRedCliente cliente;

        public void Abrir(CartaVisualManager cvm, HUDMesaDeJuego hud, UIManager ui,
            Func<DificultadIA[]> niveles, Action<DificultadIA[], PartidaRedAnfitrion> empezar)
        {
            cartaVisualManager = cvm;
            hudMesa = hud;
            uiManager = ui;
            obtenerNiveles = niveles;
            alEmpezar = empezar;

            nombre = PlayerPrefs.GetString(PrefNombre, "Jugador");
            mensaje = "";
            estado = Estado.Menu;
        }

        private void Cerrar() => estado = Estado.Cerrado;

        // =====================================================================
        // Acciones
        // =====================================================================

        /// <summary>Cambia el transporte (y, en Bluetooth, lista los moviles emparejados).</summary>
        private void ElegirModo(ModoConexion nuevo)
        {
            modo = nuevo;
            red = RedManager.ObtenerOCrear();
            red.OnError -= AlError;
            red.OnError += AlError;
            red.CambiarModo(nuevo);

            emparejados.Clear();
            if (red.Servicio is BluetoothAndroidServicio bt)
                emparejados = bt.ObtenerDispositivosEmparejados();
        }

        private void AlError(string error) => mensaje = error;

        private void GuardarPreferencias()
        {
            PlayerPrefs.SetString(PrefNombre, nombre);
            PlayerPrefs.Save();
        }

        private static string GenerarCodigo()
        {
            var c = new char[4];
            for (int i = 0; i < c.Length; i++) c[i] = AlfabetoCodigo[UnityEngine.Random.Range(0, AlfabetoCodigo.Length)];
            return new string(c);
        }

        private void CrearSala()
        {
            GuardarPreferencias();
            mensaje = "";
            ElegirModo(modo);

            if (modo == ModoConexion.InternetRelay)
            {
                codigoSala = GenerarCodigo();
                red.Conectar(InternetRelayServicio.ConstruirUrlSala(UrlRelay, codigoSala), true);
            }
            else
            {
                red.Conectar("", true, humanos - 1);
            }

            anfitrion = gameObject.AddComponent<PartidaRedAnfitrion>();
            anfitrion.AbrirSala(red, humanos, invitadoCompanero, nombre);
            estado = Estado.SalaAnfitrion;
        }

        private void EmpezarPartida()
        {
            DificultadIA[] niveles = obtenerNiveles != null ? obtenerNiveles() : null;
            if (niveles == null)
                niveles = new[] { DificultadIA.Normal, DificultadIA.Normal, DificultadIA.Normal };

            // La sesion pasa a GameManager; el lobby solo se cierra
            PartidaRedAnfitrion sesion = anfitrion;
            anfitrion = null;
            Cerrar();

            alEmpezar?.Invoke(niveles, sesion);
            if (uiManager != null) uiManager.IrAMesaDeJuego();
        }

        private void CancelarSalaAnfitrion()
        {
            if (anfitrion != null) anfitrion.Cerrar();
            anfitrion = null;
            estado = Estado.Menu;
        }

        private void Unirse()
        {
            string destino;
            if (modo == ModoConexion.InternetRelay)
            {
                if (string.IsNullOrWhiteSpace(codigoSala)) { mensaje = "Escribe el código de la sala."; return; }
                destino = InternetRelayServicio.ConstruirUrlSala(UrlRelay,codigoSala.Trim().ToUpperInvariant());
            }
            else
            {
                if (string.IsNullOrEmpty(dispositivo)) { mensaje = "Elige el móvil del anfitrión."; return; }
                destino = dispositivo;
            }

            GuardarPreferencias();
            mensaje = "";
            ElegirModo(modo);
            red.Conectar(destino, false);

            cliente = gameObject.AddComponent<PartidaRedCliente>();
            cliente.Unirse(red, nombre, cartaVisualManager, hudMesa, uiManager);
            cliente.OnPartidaIniciada += Cerrar;
            cliente.OnDesconectado += AlPerderAnfitrion;
            estado = Estado.SalaInvitado;
        }

        private void AlPerderAnfitrion(string motivo)
        {
            mensaje = motivo;
            bool estabamosJugando = estado == Estado.Cerrado;

            if (cliente != null) cliente.Salir();
            cliente = null;

            if (estabamosJugando && uiManager != null) uiManager.IrAInicio();
            estado = Estado.Menu;
        }

        private void SalirDeSalaInvitado()
        {
            if (cliente != null) cliente.Salir();
            cliente = null;
            estado = Estado.Menu;
        }

        // =====================================================================
        // Interfaz (IMGUI)
        // =====================================================================

        private void OnGUI()
        {
            if (estado == Estado.Cerrado) return;

            // Escala logica para que se vea bien en moviles de alta densidad
            float escala = Mathf.Max(1f, Screen.height / 540f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(escala, escala, 1f));
            float ancho = Mathf.Min(460f, Screen.width / escala - 24f);
            float alto = Screen.height / escala - 24f;

            GUILayout.BeginArea(new Rect((Screen.width / escala - ancho) / 2f, 12f, ancho, alto), GUI.skin.box);
            GUILayout.Label("<b>Multijugador</b>", new GUIStyle(GUI.skin.label) { fontSize = 20, richText = true });

            switch (estado)
            {
                case Estado.Menu:            DibujarMenu(); break;
                case Estado.ConfigAnfitrion: DibujarConfigAnfitrion(); break;
                case Estado.SalaAnfitrion:   DibujarSalaAnfitrion(); break;
                case Estado.ConfigInvitado:  DibujarConfigInvitado(); break;
                case Estado.SalaInvitado:    DibujarSalaInvitado(); break;
            }

            if (!string.IsNullOrEmpty(mensaje))
                GUILayout.Label(mensaje, new GUIStyle(GUI.skin.label) { wordWrap = true, normal = { textColor = new Color(1f, 0.6f, 0.4f) } });

            GUILayout.EndArea();
        }

        private static bool Boton(string texto) => GUILayout.Button(texto, GUILayout.Height(40f));

        private void SelectorModo()
        {
            int sel = GUILayout.Toolbar(modo == ModoConexion.InternetRelay ? 0 : 1,
                new[] { "Internet", "Bluetooth" }, GUILayout.Height(36f));
            ModoConexion elegido = sel == 0 ? ModoConexion.InternetRelay : ModoConexion.BluetoothLocal;
            if (elegido != modo) ElegirModo(elegido);
        }

        private void DibujarMenu()
        {
            GUILayout.Label("Tu nombre");
            nombre = GUILayout.TextField(nombre, 16, GUILayout.Height(32f));
            GUILayout.Space(8f);

            if (Boton("Crear partida")) { mensaje = ""; ElegirModo(modo); estado = Estado.ConfigAnfitrion; }
            if (Boton("Unirse a una partida")) { mensaje = ""; ElegirModo(modo); estado = Estado.ConfigInvitado; }
            if (Boton("Cerrar")) { GuardarPreferencias(); Cerrar(); }
        }

        private void DibujarConfigAnfitrion()
        {
            GUILayout.Label("Conexión");
            SelectorModo();

            GUILayout.Label("Jugadores humanos");
            humanos = GUILayout.Toolbar(humanos == 2 ? 0 : 1, new[] { "2", "4" }, GUILayout.Height(36f)) == 0 ? 2 : 4;

            if (humanos == 2)
            {
                GUILayout.Label("El otro jugador es…");
                invitadoCompanero = GUILayout.Toolbar(invitadoCompanero ? 0 : 1,
                    new[] { "Mi compañero", "Mi rival" }, GUILayout.Height(36f)) == 0;
            }
            GUILayout.Label("Los asientos sin humano los juega la IA con los niveles de la pantalla anterior.");

            if (modo == ModoConexion.BluetoothLocal)
            {
                GUILayout.Label("Los demás móviles deben estar emparejados con este en Ajustes > Bluetooth.");
            }

            GUILayout.Space(8f);
            if (Boton("Crear sala")) CrearSala();
            if (Boton("Atrás")) estado = Estado.Menu;
        }

        private void DibujarSalaAnfitrion()
        {
            if (anfitrion == null) { estado = Estado.Menu; return; }

            if (modo == ModoConexion.InternetRelay)
                GUILayout.Label($"Código de sala: <b>{codigoSala}</b>", new GUIStyle(GUI.skin.label) { fontSize = 24, richText = true });
            else
                GUILayout.Label("Esperando conexiones por Bluetooth…");

            DibujarAsientos(i => anfitrion.NombreAsiento(i));

            GUILayout.Space(8f);
            bool puedeEmpezar = anfitrion.InvitadosConectados >= 1;
            GUI.enabled = puedeEmpezar;
            if (Boton(anfitrion.SalaCompleta ? "Empezar" : "Empezar (los huecos los juega la IA)")) EmpezarPartida();
            GUI.enabled = true;
            if (Boton("Cancelar")) CancelarSalaAnfitrion();
        }

        private void DibujarConfigInvitado()
        {
            GUILayout.Label("Conexión");
            SelectorModo();

            if (modo == ModoConexion.InternetRelay)
            {
                GUILayout.Label("Código de sala");
                codigoSala = GUILayout.TextField(codigoSala, 4, GUILayout.Height(32f));
            }
            else
            {
                GUILayout.Label("Móvil del anfitrión (emparejado)");
                if (emparejados.Count == 0)
                    GUILayout.Label("No hay dispositivos emparejados.");
                foreach (string d in emparejados)
                    if (GUILayout.Toggle(dispositivo == d, d, GUI.skin.button, GUILayout.Height(36f)))
                        dispositivo = d;
            }

            GUILayout.Space(8f);
            if (Boton("Unirse")) Unirse();
            if (Boton("Atrás")) estado = Estado.Menu;
        }

        private void DibujarSalaInvitado()
        {
            EstadoSalaDto sala = cliente != null ? cliente.UltimoEstado : null;

            if (cliente == null || !cliente.Unido)
            {
                bool llena = sala != null && cliente != null && !cliente.Unido;
                GUILayout.Label(llena
                    ? "No hay sitio en esa sala (completa o ya empezada)."
                    : "Conectando con el anfitrión…");
            }
            else
            {
                GUILayout.Label("Esperando a que el anfitrión empiece la partida…");
            }

            if (sala != null)
                DibujarAsientos(i => string.IsNullOrEmpty(sala.nombres[i]) ? null : sala.nombres[i]);

            GUILayout.Space(8f);
            if (Boton("Salir")) SalirDeSalaInvitado();
        }

        private static void DibujarAsientos(Func<int, string> nombreDe)
        {
            GUILayout.Space(6f);
            GUILayout.Label("Asientos");
            string[] posiciones = { "Anfitrión", "Rival del anfitrión", "Compañero del anfitrión", "Rival del anfitrión" };
            for (int i = 0; i < 4; i++)
            {
                string n = nombreDe(i);
                GUILayout.Label($"{i + 1}. {posiciones[i]}: {(string.IsNullOrEmpty(n) ? "IA / libre" : n)}");
            }
        }
    }
}
