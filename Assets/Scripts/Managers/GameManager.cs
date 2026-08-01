using System.Collections.Generic;
using UnityEngine;
using GuinoteApp.Core;
using GuinoteApp.View;
using GuinoteApp.UI;

namespace GuinoteApp.Managers
{
    /// <summary>
    /// Punto de entrada de la partida.
    /// Coordina el reparto inicial, la capa visual (CartaVisualManager),
    /// el HUD (HUDMesaDeJuego), la navegacion (UIManager) y el TurnManager.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Managers")]
        [SerializeField] private CartaVisualManager cartaVisualManager;
        [SerializeField] private TurnManager turnManager;

        [Header("UI")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private HUDMesaDeJuego hudMesa;
        [SerializeField] private PantallaSeleccion pantallaSeleccion;

        // =====================================================================
        // Estado de partida (Core)
        // =====================================================================

        private GestorBaraja baraja;
        private List<Carta>[] manosJugadores;
        private Carta triunfo;

        private readonly string[] nombresJugadores =
            { "Jugador 1 (Tu)", "Jugador 2 (IA)", "Jugador 3 (IA Aliado)", "Jugador 4 (IA)" };

        // Carta que el humano tiene seleccionada para jugar
        private View.CardView cartaSeleccionada;

        // =====================================================================
        // Ciclo de vida Unity
        // =====================================================================

        private void Awake()
        {
            // Suscribirse al evento "Comenzar partida" de la pantalla de seleccion
            if (pantallaSeleccion != null)
                pantallaSeleccion.OnComenzarPartida += OnComenzarPartidaDesdeSeleccion;
        }

        // =====================================================================
        // Arranque de partida
        // =====================================================================

        /// <summary>
        /// Callback desde PantallaSeleccion cuando el jugador pulsa "Comenzar partida".
        /// </summary>
        private void OnComenzarPartidaDesdeSeleccion(DificultadIA dificultad)
        {
            Debug.Log($"[GameManager] Iniciando partida. Dificultad: {dificultad}");
            IniciarPartida();
        }

        private void IniciarPartida()
        {
            // ── 1. Baraja ──────────────────────────────────────────────────
            baraja = new GestorBaraja();
            baraja.Barajar();
            Debug.Log("=== GUINOTE FAMILIAR ===");

            // ── 2. Manos ───────────────────────────────────────────────────
            manosJugadores = new List<Carta>[4];
            for (int i = 0; i < 4; i++)
                manosJugadores[i] = new List<Carta>();

            for (int ronda = 0; ronda < 6; ronda++)
                for (int j = 0; j < 4; j++)
                    manosJugadores[j].Add(baraja.RobarCarta());

            // ── 3. Triunfo ─────────────────────────────────────────────────
            triunfo = baraja.RobarCarta();
            Debug.Log($"Triunfo: {triunfo} | Cartas en mazo: {baraja.CartasRestantes()}");

            // ── 4. Log de manos ────────────────────────────────────────────
            for (int i = 0; i < 4; i++)
            {
                string mano = "";
                foreach (Carta c in manosJugadores[i])
                    mano += $" [{c} {c.ObtenerPuntos()}p]";
                Debug.Log($"{nombresJugadores[i]}:{mano}");
            }

            // ── 5. Capa visual ─────────────────────────────────────────────
            if (cartaVisualManager != null)
            {
                cartaVisualManager.LimpiarTodo();
                cartaVisualManager.MostrarManoJugador(manosJugadores[0]);
                cartaVisualManager.ActualizarDorsosIA(manosJugadores);
                cartaVisualManager.MostrarTriunfo(triunfo);
                cartaVisualManager.MostrarMazo(baraja.CartasRestantes());
                cartaVisualManager.OnJugadorSeleccionoCarta += ManejarCartaSeleccionada;
                Debug.Log("[GameManager] Capa visual lista.");
            }

            // ── 6. HUD ─────────────────────────────────────────────────────
            if (hudMesa != null)
            {
                hudMesa.Resetear();
                hudMesa.ActualizarMazo(baraja.CartasRestantes());
            }

            // ── 7. TurnManager ─────────────────────────────────────────────
            if (turnManager != null)
            {
                SuscribirEventosTurnManager();
                turnManager.Inicializar(manosJugadores, baraja, triunfo, GamePhase.FaseRobo);
            }
            else
            {
                Debug.LogWarning("[GameManager] TurnManager no asignado — solo modo consola.");
            }
        }

        // =====================================================================
        // Suscripcion a eventos del TurnManager
        // =====================================================================

        private void SuscribirEventosTurnManager()
        {
            turnManager.OnTurnoJugadorHumano += HabilitarInteraccionHumano;
            turnManager.OnCartaJugada        += ManejarCartaJugadaPorCualquiera;
            turnManager.OnBazaResuelta       += ManejarBazaResuelta;
            turnManager.OnCambioFase         += ManejarCambioFase;
            turnManager.OnFinPartida         += ManejarFinPartida;
            turnManager.OnCante              += ManejarCante;
            turnManager.OnCambioTriunfo      += ManejarCambioTriunfo;
        }

        // =====================================================================
        // Callbacks del TurnManager
        // =====================================================================

        private void HabilitarInteraccionHumano()
        {
            Debug.Log("[GameManager] Turno del jugador humano.");
            cartaSeleccionada = null;
        }

        private void ManejarCartaJugadaPorCualquiera(int jugadorIdx, Carta carta)
        {
            // Mostrar la carta jugada en la posicion de baza del jugador
            if (cartaVisualManager != null)
                cartaVisualManager.MostrarCartaEnBaza(jugadorIdx, carta);

            // Si es el jugador humano, quitar la carta de la vista de mano
            if (jugadorIdx == 0 && cartaVisualManager != null && cartaSeleccionada != null)
            {
                cartaVisualManager.QuitarCartaDeMano(cartaSeleccionada);
                cartaSeleccionada = null;
            }
        }

        private void ManejarBazaResuelta(int ganador, int puntos, int ptsA, int ptsB)
        {
            string pareja = (ganador == 0 || ganador == 2) ? "A (J1+J3)" : "B (J2+J4)";
            Debug.Log($"[GameManager] Baza → J{ganador + 1} | Pareja {pareja} | A:{ptsA} B:{ptsB}");

            // Limpiar cartas de la baza del tapete
            if (cartaVisualManager != null)
                cartaVisualManager.LimpiarBaza();

            // Actualizar mano visual del humano y dorsos de IA tras el robo
            if (cartaVisualManager != null)
            {
                cartaVisualManager.MostrarManoJugador(manosJugadores[0]);
                cartaVisualManager.ActualizarDorsosIA(manosJugadores);
                cartaVisualManager.MostrarMazo(baraja.CartasRestantes());
            }

            // Actualizar HUD
            if (hudMesa != null)
            {
                // Pareja A = jugadores 0 y 2 = "Nosotros" (J1 es el humano)
                hudMesa.ActualizarMarcador(ptsA, ptsB);
                hudMesa.ActualizarMazo(baraja.CartasRestantes());
            }
        }

        private void ManejarCambioFase(GamePhase nuevaFase)
        {
            Debug.Log($"[GameManager] Nueva fase: {nuevaFase}");

            if (nuevaFase == GamePhase.FaseArrastre && cartaVisualManager != null)
            {
                // El triunfo ya no está en el mazo: ocultarlo
                cartaVisualManager.MostrarTriunfo(null);
                cartaVisualManager.MostrarMazo(0);
            }

            if (nuevaFase == GamePhase.FaseArrastre && hudMesa != null)
                hudMesa.ActualizarMazo(0);
        }

        private void ManejarFinPartida(int ptsA, int ptsB)
        {
            string resultado = ptsA > ptsB ? "Pareja A (J1+J3)" : (ptsB > ptsA ? "Pareja B (J2+J4)" : "EMPATE");
            Debug.Log($"[GameManager] FIN — A:{ptsA} | B:{ptsB} → {resultado}");

            if (hudMesa != null)
                hudMesa.MostrarFinPartida(ptsA, ptsB);
        }

        private void ManejarCante(int jugadorIdx, Palo palo, int puntos)
        {
            Debug.Log($"[GameManager] J{jugadorIdx + 1} canta {puntos} en {palo}.");

            if (hudMesa != null)
                hudMesa.MostrarCante(palo, puntos);
        }

        private void ManejarCambioTriunfo(int jugadorIdx, Carta cartaEntregada, Carta cartaRecibida)
        {
            Debug.Log($"[GameManager] J{jugadorIdx + 1} cambia el {cartaEntregada.Rango} por la pinta ({cartaRecibida}).");

            if (cartaVisualManager != null)
            {
                cartaVisualManager.MostrarTriunfo(cartaRecibida);
                if (jugadorIdx == 0)
                    cartaVisualManager.MostrarManoJugador(manosJugadores[0]);
            }
        }

        // =====================================================================
        // Input del jugador humano
        // =====================================================================

        private void ManejarCartaSeleccionada(View.CardView cartaVista)
        {
            // Si ya hay una seleccionada y se hace clic en la misma → jugarla
            if (cartaSeleccionada == cartaVista && cartaVista.CartaData != null)
            {
                IntentarJugarCarta(cartaVista);
                return;
            }

            // Primera seleccion → guardar referencia
            cartaSeleccionada = cartaVista;
            Debug.Log($"[GameManager] Seleccionada: {cartaVista.CartaData} | Click de nuevo para jugarla.");
        }

        /// <summary>
        /// Envia la carta al TurnManager para validarla y procesarla.
        /// </summary>
        private void IntentarJugarCarta(View.CardView cartaVista)
        {
            if (turnManager == null) return;

            bool exito = turnManager.ProcesarJugadaHumano(cartaVista.CartaData);
            if (!exito)
            {
                Debug.LogWarning($"[GameManager] Jugada rechazada: {cartaVista.CartaData}");
                cartaVista.Seleccionar(false);
                cartaSeleccionada = null;
            }
        }
    }
}
