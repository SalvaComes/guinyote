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
        private Carta triunfoVisible;                // La pinta, mientras no se haya cambiado/agotado
        private GamePhase faseActual;

        // =====================================================================
        // Estado de turno
        // =====================================================================

        private int jugadorActual;                  // 0..3 quien debe jugar ahora
        private int jugadorQueAbre;                 // quien abre la baza
        private EstadoBaza bazaActual;
        private int numeroBaza;

        // Palos ya cantados en la partida (Rey+Sota solo se puede cantar una vez por palo)
        private HashSet<Palo> palosCantados = new HashSet<Palo>();

        // Palo del cante recien anunciado: el jugador que abre debe salir con Rey o Sota de este palo
        private Palo? paloCantoActual;

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

        /// <summary>Un jugador canta 20 o 40. Parametros: jugadorIdx, palo cantado, puntos (20 o 40).</summary>
        public event Action<int, Palo, int> OnCante;

        /// <summary>Un jugador cambia el 7 (o el 2) de triunfo por la pinta. Parametros: jugadorIdx, carta entregada, carta recibida.</summary>
        public event Action<int, Carta, Carta> OnCambioTriunfo;

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
            Carta triunfo,
            GamePhase fase,
            int jugadorInicial = 0)
        {
            manosJugadores = manos;
            baraja = gestorBaraja;
            paloTriunfo = triunfo.Palo;
            triunfoVisible = triunfo;
            faseActual = fase;
            jugadorQueAbre = jugadorInicial;
            numeroBaza = 0;
            puntosPareja_A = 0;
            puntosPareja_B = 0;
            palosCantados.Clear();
            paloCantoActual = null;

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

            // Cantes: solo puede cantar quien abre baza, solo en Fase de Robo, y solo si su
            // pareja gano la baza anterior (numeroBaza > 1 lo garantiza, ya que quien abre
            // es siempre el ganador de la ultima baza)
            if (numeroBaza > 1 && faseActual == GamePhase.FaseRobo)
                IntentarCantarMejorOpcion(jugadorActual);

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

            // Tras cantar, la salida obligatoria es el Rey o la Sota del palo cantado
            if (paloCantoActual.HasValue && bazaActual.CartaSalida == null)
            {
                bool cumpleObligacionCante = carta.Palo == paloCantoActual.Value &&
                    (carta.Rango == Rango.Rey || carta.Rango == Rango.Sota);

                if (!cumpleObligacionCante)
                {
                    Debug.LogWarning($"[TurnManager] Tras cantar debes salir con el Rey o la Sota de {paloCantoActual.Value}.");
                    return false;
                }
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

            // Tras cantar, la IA debe salir obligatoriamente con el Rey o la Sota del palo cantado
            if (paloCantoActual.HasValue && bazaActual.CartaSalida == null)
            {
                var mano = manosJugadores[jugadorIdx];
                Carta cartaObligada = mano.Find(c => c.Palo == paloCantoActual.Value && c.Rango == Rango.Sota)
                                    ?? mano.Find(c => c.Palo == paloCantoActual.Value && c.Rango == Rango.Rey);
                if (cartaObligada != null)
                    cartaElegida = cartaObligada;
            }

            Debug.Log($"[TurnManager] J{jugadorIdx + 1} (IA) juega: {cartaElegida} ({cartaElegida.ObtenerPuntos()} pts)");
            RegistrarJugada(jugadorIdx, cartaElegida);
        }

        /// <summary>Registra una carta jugada y avanza el estado de la baza.</summary>
        private void RegistrarJugada(int jugadorIdx, Carta carta)
        {
            // La obligacion de cante solo aplica a la carta de salida de la baza
            if (bazaActual.CartaSalida == null)
                paloCantoActual = null;

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

            // Cambio del 7 (o del 2 si la pinta es de valor 7 o inferior): solo en Fase Robo,
            // solo puede hacerlo la pareja que acaba de ganar la baza, antes de robar
            if (faseActual == GamePhase.FaseRobo)
            {
                int[] jugadoresPareja = ganaPareja_A ? new[] { 0, 2 } : new[] { 1, 3 };
                foreach (int idx in jugadoresPareja)
                    IntentarCambiarTriunfo(idx);
            }

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
                triunfoVisible = null; // La pinta ya no esta disponible: ni cante ni cambio a partir de aqui
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
        // Cantes (Las 20 y Las 40)
        // =====================================================================

        /// <summary>Comprueba si la mano tiene el Rey y la Sota de un palo determinado.</summary>
        private static bool TieneReyYSota(List<Carta> mano, Palo palo)
        {
            bool tieneRey = false, tieneSota = false;
            foreach (Carta c in mano)
            {
                if (c.Palo == palo && c.Rango == Rango.Rey) tieneRey = true;
                if (c.Palo == palo && c.Rango == Rango.Sota) tieneSota = true;
            }
            return tieneRey && tieneSota;
        }

        /// <summary>
        /// Busca el mejor cante disponible para el jugador (prioriza 40 sobre 20, ya que
        /// solo se puede cantar un palo por baza) y lo anuncia si existe.
        /// </summary>
        private bool IntentarCantarMejorOpcion(int jugadorIdx)
        {
            List<Carta> mano = manosJugadores[jugadorIdx];
            Palo? mejorPalo = null;
            int mejorPuntos = 0;

            foreach (Palo palo in Enum.GetValues(typeof(Palo)))
            {
                if (palosCantados.Contains(palo)) continue;
                if (!TieneReyYSota(mano, palo)) continue;

                int puntos = palo == paloTriunfo ? 40 : 20;
                if (puntos > mejorPuntos)
                {
                    mejorPuntos = puntos;
                    mejorPalo = palo;
                }
            }

            if (mejorPalo == null) return false;

            palosCantados.Add(mejorPalo.Value);
            paloCantoActual = mejorPalo.Value;

            bool esPareja_A = (jugadorIdx == 0 || jugadorIdx == 2);
            if (esPareja_A) puntosPareja_A += mejorPuntos;
            else puntosPareja_B += mejorPuntos;

            Debug.Log($"[TurnManager] J{jugadorIdx + 1} canta {mejorPuntos} en {mejorPalo.Value}. Debe salir con Rey o Sota de ese palo.");
            OnCante?.Invoke(jugadorIdx, mejorPalo.Value, mejorPuntos);
            return true;
        }

        // =====================================================================
        // Cambio del 7 (y del 2) de triunfo por la pinta
        // =====================================================================

        /// <summary>
        /// Intenta cambiar la pinta (carta de triunfo visible) por el 7 de triunfo del jugador
        /// si la pinta es superior al 7, o por el 2 de triunfo si la pinta es de valor 7 o
        /// inferior (variante que extiende el cambio del 7 al caso de pintas bajas).
        /// Solo aplicable justo despues de que la pareja del jugador gane una baza, en Fase de Robo.
        /// </summary>
        private bool IntentarCambiarTriunfo(int jugadorIdx)
        {
            if (triunfoVisible == null) return false;

            Rango rangoVisible = triunfoVisible.Rango;
            Rango rangoBuscado;

            if (rangoVisible > Rango.Siete)
                rangoBuscado = Rango.Siete;
            else if (rangoVisible > Rango.Dos)
                rangoBuscado = Rango.Dos;
            else
                return false; // La pinta ya es el Dos: no hay mejora posible

            List<Carta> mano = manosJugadores[jugadorIdx];
            Carta cartaPropia = mano.Find(c => c.Palo == paloTriunfo && c.Rango == rangoBuscado);
            if (cartaPropia == null) return false;

            Carta cartaRecibida = triunfoVisible;
            mano.Remove(cartaPropia);
            mano.Add(cartaRecibida);
            triunfoVisible = cartaPropia;

            Debug.Log($"[TurnManager] J{jugadorIdx + 1} cambia el {cartaPropia.Rango} de triunfo por la pinta ({cartaRecibida}).");
            OnCambioTriunfo?.Invoke(jugadorIdx, cartaPropia, cartaRecibida);
            return true;
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
