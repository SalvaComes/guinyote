using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GuinoteApp.Core;

namespace GuinoteApp.Managers
{
    /// <summary>
    /// Orquesta el ciclo de turnos de la partida.
    /// Gestiona las bazas, el robo de cartas, el cambio de fase y el fin de partida.
    /// Separa claramente la logica (Core) de los tiempos visuales (Coroutines).
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        // =====================================================================
        // Configuracion
        // =====================================================================

        [Header("Tiempos de IA (segundos)")]
        [SerializeField] private float delayIAPiensa = 0.6f;
        [SerializeField] private float delayEntreCartas = 0.3f;
        [SerializeField] private float delayFinBaza = 1.2f;

        // =====================================================================
        // Estado de partida (referencias compartidas con GameManager)
        // =====================================================================

        private List<Carta>[] manosJugadores;       // [0..3]
        private GestorBaraja baraja;
        private Palo paloTriunfo;
        private GamePhase faseActual;

        // =====================================================================
        // Estado de turno
        // =====================================================================

        private int jugadorActual;                  // 0..3 quien debe jugar ahora
        private int jugadorQueAbre;                 // quien abre la baza
        private EstadoBaza bazaActual;
        private int numeroBaza;

        // Puntos acumulados por pareja (A = jugadores 0,2 | B = jugadores 1,3)
        private int puntosPareja_A;
        private int puntosPareja_B;

        // IA para los 3 jugadores no-humanos
        private IEstrategiaIA[] estrategiasIA;

        // =====================================================================
        // Eventos publicos
        // =====================================================================

        /// <summary>El turno ahora le corresponde al jugador humano (indice 0).</summary>
        public event Action OnTurnoJugadorHumano;

        /// <summary>Se resolvio una baza. Parametros: indice ganador, puntos ganados, PuntosTotales(A,B).</summary>
        public event Action<int, int, int, int> OnBazaResuelta;

        /// <summary>Un jugador acaba de jugar una carta. Parametros: jugadorIdx, carta.</summary>
        public event Action<int, Carta> OnCartaJugada;

        /// <summary>La partida termino. Parametros: puntos pareja A, puntos pareja B.</summary>
        public event Action<int, int> OnFinPartida;

        /// <summary>Cambio de fase de la partida.</summary>
        public event Action<GamePhase> OnCambioFase;

        // =====================================================================
        // Inicializacion publica
        // =====================================================================

        /// <summary>
        /// Inicializa el TurnManager con todos los datos de la partida.
        /// Llamar desde GameManager despues del reparto.
        /// </summary>
        public void Inicializar(
            List<Carta>[] manos,
            GestorBaraja gestorBaraja,
            Palo triunfo,
            GamePhase fase,
            int jugadorInicial = 0)
        {
            manosJugadores = manos;
            baraja = gestorBaraja;
            paloTriunfo = triunfo;
            faseActual = fase;
            jugadorQueAbre = jugadorInicial;
            numeroBaza = 0;
            puntosPareja_A = 0;
            puntosPareja_B = 0;

            // Crear una IA greedy compartida para los 3 jugadores IA
            var ia = new IAGreedy();
            estrategiasIA = new IEstrategiaIA[]
            {
                null, // Jugador 0 es humano
                ia,
                ia,
                ia
            };

            Debug.Log($"[TurnManager] Inicializado. Triunfo: {paloTriunfo} | Fase: {faseActual} | Abre: J{jugadorQueAbre + 1}");
            IniciarSiguienteBaza();
        }

        // =====================================================================
        // Flujo de baza
        // =====================================================================

        private void IniciarSiguienteBaza()
        {
            // Comprobar fin de partida
            if (manosJugadores[0].Count == 0)
            {
                TerminarPartida();
                return;
            }

            numeroBaza++;
            bazaActual = new EstadoBaza();
            jugadorActual = jugadorQueAbre;

            Debug.Log($"[TurnManager] ── Baza {numeroBaza} ── Sale: J{jugadorActual + 1} | Fase: {faseActual}");
            AvanzarTurno();
        }

        private void AvanzarTurno()
        {
            if (jugadorActual == 0)
            {
                // Turno del jugador humano
                Debug.Log("[TurnManager] Esperando jugada del jugador humano...");
                OnTurnoJugadorHumano?.Invoke();
            }
            else
            {
                // Turno de la IA
                StartCoroutine(TurnoIA(jugadorActual));
            }
        }

        // =====================================================================
        // Jugadas
        // =====================================================================

        /// <summary>
        /// Llamar cuando el jugador humano selecciona una carta para jugar.
        /// Valida la jugada y la procesa si es correcta.
        /// </summary>
        /// <returns>True si la jugada fue valida y procesada.</returns>
        public bool ProcesarJugadaHumano(Carta carta)
        {
            if (jugadorActual != 0)
            {
                Debug.LogWarning("[TurnManager] No es el turno del jugador humano.");
                return false;
            }

            // Validar en Fase Arrastre
            if (faseActual == GamePhase.FaseArrastre && bazaActual.CartaSalida != null)
            {
                bool valida = ValidadorReglas.EsJugadaValidaArrastre(
                    carta,
                    bazaActual.CartaSalida,
                    paloTriunfo,
                    manosJugadores[0],
                    bazaActual.MejorCartaActual);

                if (!valida)
                {
                    Debug.LogWarning($"[TurnManager] Jugada invalida en Arrastre: {carta}");
                    return false;
                }
            }

            RegistrarJugada(0, carta);
            return true;
        }

        /// <summary>Coroutine para el turno de un jugador IA con delay visual.</summary>
        private IEnumerator TurnoIA(int jugadorIdx)
        {
            yield return new WaitForSeconds(delayIAPiensa);

            Carta cartaElegida = estrategiasIA[jugadorIdx].ElegirCarta(
                manosJugadores[jugadorIdx],
                bazaActual,
                faseActual,
                paloTriunfo);

            if (cartaElegida == null)
            {
                Debug.LogError($"[TurnManager] IA J{jugadorIdx + 1} devolvio null. Mano vacia?");
                yield break;
            }

            Debug.Log($"[TurnManager] J{jugadorIdx + 1} (IA) juega: {cartaElegida} ({cartaElegida.ObtenerPuntos()} pts)");
            RegistrarJugada(jugadorIdx, cartaElegida);
        }

        /// <summary>Registra una carta jugada y avanza el estado de la baza.</summary>
        private void RegistrarJugada(int jugadorIdx, Carta carta)
        {
            manosJugadores[jugadorIdx].Remove(carta);
            bazaActual.RegistrarJugada(jugadorIdx, carta, paloTriunfo);
            OnCartaJugada?.Invoke(jugadorIdx, carta);

            Debug.Log($"[TurnManager] J{jugadorIdx + 1} juega: {carta} | {bazaActual}");

            if (bazaActual.EstaCompleta)
            {
                StartCoroutine(FinalizarBaza());
            }
            else
            {
                // Siguiente jugador en la baza (rotacion 0→1→2→3→0)
                jugadorActual = (jugadorActual + 1) % 4;
                StartCoroutine(EsperarYAvanzar());
            }
        }

        private IEnumerator EsperarYAvanzar()
        {
            yield return new WaitForSeconds(delayEntreCartas);
            AvanzarTurno();
        }

        // =====================================================================
        // Resolucion de baza
        // =====================================================================

        private IEnumerator FinalizarBaza()
        {
            yield return new WaitForSeconds(delayFinBaza);

            int ganador = bazaActual.ObtenerGanador();
            int puntos = bazaActual.PuntosEnMesa;

            // Asignar puntos a la pareja correspondiente
            // Pareja A: jugadores 0 y 2 | Pareja B: jugadores 1 y 3
            bool ganaPareja_A = (ganador == 0 || ganador == 2);
            if (ganaPareja_A)
                puntosPareja_A += puntos;
            else
                puntosPareja_B += puntos;

            // Las 10 de ultimas: si es la ultima baza (+10 al ganador)
            bool esUltimaBaza = manosJugadores[0].Count == 0;
            if (esUltimaBaza)
            {
                if (ganaPareja_A) puntosPareja_A += 10;
                else              puntosPareja_B += 10;
                Debug.Log($"[TurnManager] Las 10 de ultimas → {(ganaPareja_A ? "Pareja A" : "Pareja B")}");
            }

            string pareja = ganaPareja_A ? "Pareja A (J1+J3)" : "Pareja B (J2+J4)";
            Debug.Log($"[TurnManager] Baza {numeroBaza} ganada por J{ganador + 1} ({pareja}) | +{puntos} pts | Total A:{puntosPareja_A} B:{puntosPareja_B}");

            OnBazaResuelta?.Invoke(ganador, puntos, puntosPareja_A, puntosPareja_B);

            // El ganador de la baza abre la siguiente
            jugadorQueAbre = ganador;

            // Robo de cartas (solo en Fase Robo, con cartas en el mazo)
            if (faseActual == GamePhase.FaseRobo)
                yield return StartCoroutine(RealizarRobo(ganador));

            IniciarSiguienteBaza();
        }

        /// <summary>
        /// Cada jugador roba una carta empezando por el ganador de la baza.
        /// Si el mazo se agota, se pasa a Fase Arrastre.
        /// </summary>
        private IEnumerator RealizarRobo(int ganadorBaza)
        {
            // Orden de robo: empieza el ganador
            for (int i = 0; i < 4; i++)
            {
                int idx = (ganadorBaza + i) % 4;
                Carta robada = baraja.RobarCarta();
                if (robada != null)
                {
                    manosJugadores[idx].Add(robada);
                    Debug.Log($"[TurnManager] J{idx + 1} roba: {robada} | Mazo restante: {baraja.CartasRestantes()}");
                }
                else
                {
                    Debug.Log($"[TurnManager] Mazo vacio. J{idx + 1} no puede robar.");
                }
                yield return new WaitForSeconds(0.15f);
            }

            // Verificar si el mazo se agoto → cambiar a Fase Arrastre
            if (baraja.CartasRestantes() == 0 && faseActual == GamePhase.FaseRobo)
            {
                faseActual = GamePhase.FaseArrastre;
                Debug.Log("[TurnManager] Mazo agotado → FASE ARRASTRE");
                OnCambioFase?.Invoke(faseActual);
            }
        }

        // =====================================================================
        // Fin de partida
        // =====================================================================

        private void TerminarPartida()
        {
            faseActual = GamePhase.FinDePartida;

            string resultado;
            if (puntosPareja_A > puntosPareja_B)
                resultado = $"GANA Pareja A (J1+J3) con {puntosPareja_A} puntos!";
            else if (puntosPareja_B > puntosPareja_A)
                resultado = $"GANA Pareja B (J2+J4) con {puntosPareja_B} puntos!";
            else
                resultado = "EMPATE!";

            Debug.Log($"[TurnManager] FIN DE PARTIDA — A:{puntosPareja_A} | B:{puntosPareja_B} — {resultado}");
            OnCambioFase?.Invoke(faseActual);
            OnFinPartida?.Invoke(puntosPareja_A, puntosPareja_B);
        }

        // =====================================================================
        // Utilidades publicas
        // =====================================================================

        public int GetPuntosPareja(int parejaIdx) =>
            parejaIdx == 0 ? puntosPareja_A : puntosPareja_B;

        public GamePhase GetFaseActual() => faseActual;

        public EstadoBaza GetBazaActual() => bazaActual;
    }
}
