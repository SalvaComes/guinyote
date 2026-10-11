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
        [SerializeField] private float delayRobo = 0.4f;

        [Header("Dificultad IA (jugadores 2, 3 y 4)")]
        [SerializeField] private NivelDificultadIA nivelJugador2 = NivelDificultadIA.Normal;
        [SerializeField] private NivelDificultadIA nivelJugador3 = NivelDificultadIA.Normal;
        [SerializeField] private NivelDificultadIA nivelJugador4 = NivelDificultadIA.Normal;

        // Generador compartido por las tres IAs: new Random() sin semilla puede dar la misma
        // secuencia a dos instancias creadas en el mismo tick del reloj.
        private readonly System.Random rngIA = new System.Random();

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

        // Puntos acumulados por pareja (A = jugadores 0,2 | B = jugadores 1,3)
        private int puntosPareja_A;
        private int puntosPareja_B;

        // IA para los 3 jugadores no-humanos
        private IEstrategiaIA[] estrategiasIA;

        // Todas las cartas jugadas desde el reparto de esta mano (para IA con conteo de cartas)
        private List<Carta> historialCartasJugadas = new List<Carta>();

        // Asientos controlados por humanos (en solitario solo el 0; en red, los de los jugadores conectados)
        private readonly HashSet<int> asientosHumanos = new HashSet<int> { 0 };

        // Asiento humano al que se espera una carta ahora mismo (-1 = ninguno)
        private int esperandoCartaDe = -1;

        // Decisiones pendientes de un jugador humano (cante / cambio de la pinta) y de quien son
        private int asientoDecisionCante = -1;
        private int asientoDecisionCambio = -1;
        private bool esperandoCanteHumano;
        private int siguienteCandidatoCante;   // 0 = quien abre, 1 = su compañero, 2 = nadie mas
        private bool canteHechoEnBaza;
        private bool esperandoCambioHumano;
        private bool cambioHumanoConfirmado;
        private List<Palo> palosCantablesHumano;

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

        /// <summary>Es el turno de un humano (cualquier asiento). Parametro: asiento. Util en red.</summary>
        public event Action<int> OnTurnoDeHumano;

        /// <summary>Un humano puede cantar: la partida espera a <see cref="ResponderCanteDe"/>.
        /// Parametros: asiento, palos cantables (el del triunfo vale 40, el resto 20).</summary>
        public event Action<int, IReadOnlyList<Palo>> OnDecisionCanteHumano;

        /// <summary>Un humano puede cambiar la pinta: la partida espera a <see cref="ResponderCambioDe"/>.
        /// Parametros: asiento, carta propia que entregaria (7 o 2 de triunfo), pinta que recibiria.</summary>
        public event Action<int, Carta, Carta> OnDecisionCambioHumano;

        /// <summary>Se dispara cuando el contenido de las manos/mazo cambia tras el robo
        /// (distinto de OnBazaResuelta, que se dispara antes de robar).</summary>
        public event Action OnManosActualizadas;

        /// <summary>Se dispara justo cuando un jugador roba una carta del mazo (para animar
        /// el vuelo de la carta), en el orden real de robo: gana la baza, luego antihorario.
        /// Parametros: jugadorIdx, true si la carta robada es la pinta (el ultimo robo).</summary>
        public event Action<int, bool> OnJugadorRobo;

        // =====================================================================
        // Inicializacion publica
        // =====================================================================

        /// <summary>Asientos (0-3) controlados por humanos; el resto los juega la IA.
        /// Llamar antes de <see cref="Inicializar"/>. Por defecto, solo el 0.</summary>
        public void ConfigurarHumanos(IEnumerable<int> asientos)
        {
            asientosHumanos.Clear();
            foreach (int a in asientos)
                if (a >= 0 && a < 4) asientosHumanos.Add(a);
        }

        public bool EsHumano(int asiento) => asientosHumanos.Contains(asiento);

        /// <summary>
        /// Un humano abandona la partida y su asiento pasa a la IA (con el nivel configurado para ese
        /// asiento). Resuelve lo que estuviera esperando de el: cante o cambio rechazados, o su carta.
        /// </summary>
        public void SustituirPorIA(int asiento)
        {
            if (!asientosHumanos.Remove(asiento) || estrategiasIA == null) return;

            NivelDificultadIA nivel = asiento == 1 ? nivelJugador2 : asiento == 2 ? nivelJugador3 : nivelJugador4;
            estrategiasIA[asiento] = IAFactory.Crear(nivel, rngIA);
            Debug.Log($"[TurnManager] J{asiento + 1} deja la partida: lo sustituye la IA ({nivel}).");

            if (esperandoCanteHumano && asientoDecisionCante == asiento)
                ResponderCanteDe(asiento, null);
            if (esperandoCambioHumano && asientoDecisionCambio == asiento)
                ResponderCambioDe(asiento, false);

            if (esperandoCartaDe == asiento)
            {
                esperandoCartaDe = -1;
                StartCoroutine(TurnoIA(asiento));
            }
        }

        /// <summary>Fija el mismo nivel de dificultad para los tres jugadores controlados por la IA.
        /// Llamar antes de <see cref="Inicializar"/>.</summary>
        public void ConfigurarDificultad(NivelDificultadIA nivel)
        {
            ConfigurarDificultades(nivel, nivel, nivel);
        }

        /// <summary>Fija un nivel de IA distinto para cada jugador no humano (J2, J3 y J4).
        /// Llamar antes de <see cref="Inicializar"/>.</summary>
        public void ConfigurarDificultades(NivelDificultadIA jugador2, NivelDificultadIA jugador3, NivelDificultadIA jugador4)
        {
            nivelJugador2 = jugador2;
            nivelJugador3 = jugador3;
            nivelJugador4 = jugador4;
        }

        /// <summary>
        /// Inicializa el TurnManager con todos los datos de la partida.
        /// Llamar desde GameManager despues del reparto.
        /// </summary>
        /// <summary>Puntos que cada equipo trae acumulados en el Juego antes de esta mano. Si durante
        /// la mano un equipo supera los 100 en total (51 Buenas), la mano termina en ese momento,
        /// pero solo si <paramref name="cortarAlSuperar100"/> (a partir del segundo Juego del Coto).
        /// Fijar antes de <see cref="Inicializar"/>.</summary>
        public void ConfigurarPuntosPrevios(int puntosEquipo1, int puntosEquipo2, bool cortarAlSuperar100)
        {
            puntosPreviosEquipo1 = puntosEquipo1;
            puntosPreviosEquipo2 = puntosEquipo2;
            cortarManoAlSuperar100 = cortarAlSuperar100;
        }

        private int puntosCantesA;
        private int puntosCantesB;

        /// <summary>Puntos de la mano de un equipo (1 o 2) sin contar los cantes (regla de los 30 puntos).</summary>
        public int PuntosBazasSinCantes(int equipo) =>
            equipo == 1 ? puntosPareja_A - puntosCantesA : puntosPareja_B - puntosCantesB;

        private int puntosPreviosEquipo1;
        private int puntosPreviosEquipo2;
        private bool cortarManoAlSuperar100;

        private bool AlguienGanaElJuego() =>
            cortarManoAlSuperar100 && (puntosPreviosEquipo1 + puntosPareja_A > MarcadorEquipo.PuntosParaGanarJuego ||
            puntosPreviosEquipo2 + puntosPareja_B > MarcadorEquipo.PuntosParaGanarJuego);

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
            puntosCantesA = 0;
            puntosCantesB = 0;
            EquipoQueHizoUltimas = 0;
            palosCantados.Clear();
            historialCartasJugadas.Clear();
            esperandoCanteHumano = false;
            esperandoCambioHumano = false;
            StopAllCoroutines();

            esperandoCartaDe = -1;
            asientoDecisionCante = -1;
            asientoDecisionCambio = -1;

            // Los asientos humanos no tienen estrategia (null); el resto, la IA de su nivel
            estrategiasIA = new IEstrategiaIA[]
            {
                EsHumano(0) ? null : IAFactory.Crear(nivelJugador2, rngIA),
                EsHumano(1) ? null : IAFactory.Crear(nivelJugador2, rngIA),
                EsHumano(2) ? null : IAFactory.Crear(nivelJugador3, rngIA),
                EsHumano(3) ? null : IAFactory.Crear(nivelJugador4, rngIA)
            };

            Debug.Log($"[TurnManager] Dificultad IA: J2={nivelJugador2} J3={nivelJugador3} J4={nivelJugador4}");

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

            // Cantes: pueden cantar los dos jugadores de la pareja que gano la baza anterior
            // (primero quien abre y luego su compañero), en cualquier fase (Robo o Arrastre).
            // Solo se puede cantar un palo por baza.
            if (numeroBaza > 1)
            {
                siguienteCandidatoCante = 0;
                canteHechoEnBaza = false;
                ContinuarOfertasCante();
                return;
            }

            AvanzarTurno();
        }

        private void AvanzarTurno()
        {
            if (faseActual == GamePhase.FinDePartida) return;

            if (EsHumano(jugadorActual))
            {
                // Turno de un jugador humano (el local o uno conectado por red)
                Debug.Log($"[TurnManager] Esperando jugada del jugador humano J{jugadorActual + 1}...");
                esperandoCartaDe = jugadorActual;
                OnTurnoDeHumano?.Invoke(jugadorActual);
                if (jugadorActual == 0)
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
        public bool ProcesarJugadaHumano(Carta carta) => ProcesarJugadaDe(0, carta);

        /// <summary>
        /// Igual que <see cref="ProcesarJugadaHumano"/> pero para cualquier asiento humano (juego en red).
        /// <paramref name="carta"/> debe ser la instancia que esta en la mano de ese asiento.
        /// </summary>
        public bool ProcesarJugadaDe(int asiento, Carta carta)
        {
            if (carta == null || !EsHumano(asiento) || jugadorActual != asiento || esperandoCartaDe != asiento)
            {
                Debug.LogWarning($"[TurnManager] No es el turno del jugador humano J{asiento + 1}.");
                return false;
            }

            if (esperandoCanteHumano || esperandoCambioHumano)
            {
                Debug.LogWarning("[TurnManager] Hay una decision pendiente (cante o cambio).");
                return false;
            }

            // Reglas de la jugada: en Fase Arrastre, asistir / montar / fallar
            List<Carta> permitidas = HeuristicasIA.JugadasPermitidas(
                manosJugadores[asiento], bazaActual, faseActual, paloTriunfo);
            if (!permitidas.Contains(carta))
            {
                Debug.LogWarning($"[TurnManager] Jugada invalida en Arrastre: {carta}");
                return false;
            }

            RegistrarJugada(asiento, carta);
            return true;
        }

        /// <summary>Coroutine para el turno de un jugador IA con delay visual.</summary>
        private IEnumerator TurnoIA(int jugadorIdx)
        {
            yield return new WaitForSeconds(delayIAPiensa);

            Carta cartaElegida = estrategiasIA[jugadorIdx].ElegirCarta(CrearContextoIA(jugadorIdx));

            if (cartaElegida == null)
            {
                Debug.LogError($"[TurnManager] IA J{jugadorIdx + 1} devolvio null. Mano vacia?");
                yield break;
            }

            // Red de seguridad: las estrategias ya respetan las reglas del Arrastre
            List<Carta> permitidas = HeuristicasIA.JugadasPermitidas(
                manosJugadores[jugadorIdx], bazaActual, faseActual, paloTriunfo);
            if (!permitidas.Contains(cartaElegida))
                cartaElegida = permitidas[0];

            Debug.Log($"[TurnManager] J{jugadorIdx + 1} (IA) juega: {cartaElegida} ({cartaElegida.ObtenerPuntos()} pts)");
            RegistrarJugada(jugadorIdx, cartaElegida);
        }

        /// <summary>Registra una carta jugada y avanza el estado de la baza.</summary>
        private void RegistrarJugada(int jugadorIdx, Carta carta)
        {
            esperandoCartaDe = -1;
            manosJugadores[jugadorIdx].Remove(carta);
            bazaActual.RegistrarJugada(jugadorIdx, carta, paloTriunfo);
            historialCartasJugadas.Add(carta);
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
                EquipoQueHizoUltimas = ganaPareja_A ? 1 : 2;
                if (ganaPareja_A) puntosPareja_A += 10;
                else              puntosPareja_B += 10;
                Debug.Log($"[TurnManager] Las 10 de ultimas → {(ganaPareja_A ? "Pareja A" : "Pareja B")}");
            }

            string pareja = ganaPareja_A ? "Pareja A (J1+J3)" : "Pareja B (J2+J4)";
            Debug.Log($"[TurnManager] Baza {numeroBaza} ganada por J{ganador + 1} ({pareja}) | +{puntos} pts | Total A:{puntosPareja_A} B:{puntosPareja_B}");

            OnBazaResuelta?.Invoke(ganador, puntos, puntosPareja_A, puntosPareja_B);

            // El ganador de la baza abre la siguiente
            jugadorQueAbre = ganador;

            // Si un equipo ya supera los 100 puntos del Juego, la mano se acaba aqui
            if (AlguienGanaElJuego())
            {
                Debug.Log("[TurnManager] Un equipo supera los 100 puntos: la mano termina.");
                TerminarPartida();
                yield break;
            }

            // Cambio del 7 (o del 2 si la pinta es de valor 7 o inferior): solo en Fase Robo,
            // solo puede hacerlo la pareja que acaba de ganar la baza, antes de robar
            if (faseActual == GamePhase.FaseRobo)
            {
                int[] jugadoresPareja = ganaPareja_A ? new[] { 0, 2 } : new[] { 1, 3 };
                foreach (int idx in jugadoresPareja)
                    yield return StartCoroutine(ResolverCambio(idx));
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
                bool esPinta = false;

                // El mazo boca abajo tiene 40 - 24 (repartidas) - 1 (pinta) = 15 cartas,
                // no multiplo de 4: el ultimo en robar se lleva la pinta (REGLAS_GUINOTE.md §4.1),
                // para que las 4 manos terminen siempre sincronizadas.
                if (robada == null && triunfoVisible != null)
                {
                    robada = triunfoVisible;
                    triunfoVisible = null;
                    esPinta = true;
                }

                if (robada != null)
                {
                    manosJugadores[idx].Add(robada);
                    Debug.Log($"[TurnManager] J{idx + 1} roba: {robada} | Mazo restante: {baraja.CartasRestantes()}");
                    OnJugadorRobo?.Invoke(idx, esPinta);
                }
                else
                {
                    Debug.Log($"[TurnManager] Mazo vacio. J{idx + 1} no puede robar.");
                }
                yield return new WaitForSeconds(delayRobo);
            }

            // Verificar si el mazo (boca abajo + pinta) se agoto → cambiar a Fase Arrastre
            if (baraja.CartasRestantes() == 0 && triunfoVisible == null && faseActual == GamePhase.FaseRobo)
            {
                faseActual = GamePhase.FaseArrastre;
                Debug.Log("[TurnManager] Mazo agotado → FASE ARRASTRE");
                OnCambioFase?.Invoke(faseActual);
            }

            // OnBazaResuelta ya se disparo antes de robar (mano/mazo aun con el estado
            // previo): este evento avisa de que las manos y el mazo ya reflejan el robo.
            OnManosActualizadas?.Invoke();
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
        // Contexto para las IAs
        // =====================================================================

        /// <summary>Informacion publica de la partida tal y como la veria el jugador indicado.</summary>
        private ContextoIA CrearContextoIA(int jugadorIdx)
        {
            bool parejaA = jugadorIdx == 0 || jugadorIdx == 2;
            return new ContextoIA
            {
                JugadorIdx = jugadorIdx,
                Mano = manosJugadores[jugadorIdx],
                Baza = bazaActual,
                Fase = faseActual,
                Triunfo = paloTriunfo,
                CartasJugadas = historialCartasJugadas,
                PuntosEquipo = parejaA ? puntosPareja_A : puntosPareja_B,
                PuntosRival = parejaA ? puntosPareja_B : puntosPareja_A,
                PintaVisible = triunfoVisible,
                CartasEnMazo = baraja.CartasRestantes(),
                PalosCantados = palosCantados
            };
        }

        // =====================================================================
        // Cantes (Las 20 y Las 40)
        // =====================================================================

        /// <summary>
        /// Ofrece el cante, por orden, a quien abre la baza y a su compañero, hasta que alguien
        /// cante o ambos hayan decidido. Despues comienza el turno de juego.
        /// </summary>
        private void ContinuarOfertasCante()
        {
            while (!canteHechoEnBaza && siguienteCandidatoCante < 2 && faseActual != GamePhase.FinDePartida)
            {
                int jugador = siguienteCandidatoCante == 0 ? jugadorQueAbre : (jugadorQueAbre + 2) % 4;
                siguienteCandidatoCante++;
                if (OfrecerCante(jugador))
                    return; // el humano decide; se continua desde ResponderCanteHumano
            }

            AvanzarTurno();
        }

        /// <summary>
        /// Ofrece el cante a un jugador. Si es una IA decide al momento; si es el humano y
        /// puede cantar, lanza <see cref="OnDecisionCanteHumano"/> y la partida espera.
        /// </summary>
        /// <returns>True si hay que esperar la respuesta del humano.</returns>
        private bool OfrecerCante(int jugadorIdx)
        {
            List<Palo> cantables = ReglasEspeciales.PalosCantables(manosJugadores[jugadorIdx], palosCantados);
            if (cantables.Count == 0) return false;

            if (EsHumano(jugadorIdx))
            {
                palosCantablesHumano = cantables;
                asientoDecisionCante = jugadorIdx;
                esperandoCanteHumano = true;
                OnDecisionCanteHumano?.Invoke(jugadorIdx, cantables);
                return true;
            }

            Palo? elegido = estrategiasIA[jugadorIdx].DecidirCante(CrearContextoIA(jugadorIdx), cantables);
            if (elegido.HasValue && cantables.Contains(elegido.Value))
                AnunciarCante(jugadorIdx, elegido.Value);
            return false;
        }

        /// <summary>
        /// Respuesta del humano a <see cref="OnDecisionCanteHumano"/>: el palo que canta, o null
        /// para no cantar. Reanuda la partida.
        /// </summary>
        public void ResponderCanteHumano(Palo? palo) => ResponderCanteDe(0, palo);

        /// <summary>Respuesta de un asiento humano concreto a <see cref="OnDecisionCanteHumano"/>.</summary>
        public void ResponderCanteDe(int asiento, Palo? palo)
        {
            if (!esperandoCanteHumano || asiento != asientoDecisionCante) return;
            esperandoCanteHumano = false;

            if (palo.HasValue && palosCantablesHumano != null && palosCantablesHumano.Contains(palo.Value))
                AnunciarCante(asiento, palo.Value);

            ContinuarOfertasCante();
        }

        private void AnunciarCante(int jugadorIdx, Palo palo)
        {
            int puntos = ReglasEspeciales.PuntosCante(palo, paloTriunfo);

            palosCantados.Add(palo);
            canteHechoEnBaza = true;


            bool esPareja_A = (jugadorIdx == 0 || jugadorIdx == 2);
            if (esPareja_A) { puntosPareja_A += puntos; puntosCantesA += puntos; }
            else            { puntosPareja_B += puntos; puntosCantesB += puntos; }

            Debug.Log($"[TurnManager] J{jugadorIdx + 1} canta {puntos} en {palo}.");
            OnCante?.Invoke(jugadorIdx, palo, puntos);

            // Un cante que lleva al equipo por encima de 100 acaba la mano al instante
            if (AlguienGanaElJuego())
            {
                Debug.Log("[TurnManager] El cante lleva a un equipo por encima de 100: la mano termina.");
                TerminarPartida();
            }
        }

        // =====================================================================
        // Cambio del 7 (y del 2) de triunfo por la pinta
        // =====================================================================

        /// <summary>
        /// Si el jugador tiene la carta de cambio (el 7 de triunfo si la pinta es mayor que 7;
        /// el 2 si es de valor 7 o inferior) decide si cambia: la IA con su estrategia, el
        /// humano mediante <see cref="OnDecisionCambioHumano"/>.
        /// Solo aplicable justo despues de que la pareja del jugador gane una baza, en Fase de Robo.
        /// </summary>
        private IEnumerator ResolverCambio(int jugadorIdx)
        {
            if (triunfoVisible == null) yield break;

            Carta cartaPropia = ReglasEspeciales.CartaDeCambio(
                manosJugadores[jugadorIdx], triunfoVisible, paloTriunfo);
            if (cartaPropia == null) yield break;

            bool cambiar;
            if (EsHumano(jugadorIdx))
            {
                asientoDecisionCambio = jugadorIdx;
                esperandoCambioHumano = true;
                cambioHumanoConfirmado = false;
                OnDecisionCambioHumano?.Invoke(jugadorIdx, cartaPropia, triunfoVisible);
                yield return new WaitUntil(() => !esperandoCambioHumano);
                cambiar = cambioHumanoConfirmado;
            }
            else
            {
                cambiar = estrategiasIA[jugadorIdx].DecidirCambio(CrearContextoIA(jugadorIdx), cartaPropia);
            }

            if (cambiar)
                EjecutarCambio(jugadorIdx, cartaPropia);
        }

        /// <summary>Respuesta del humano a <see cref="OnDecisionCambioHumano"/>. Reanuda la partida.</summary>
        public void ResponderCambioHumano(bool cambiar) => ResponderCambioDe(0, cambiar);

        /// <summary>Respuesta de un asiento humano concreto a <see cref="OnDecisionCambioHumano"/>.</summary>
        public void ResponderCambioDe(int asiento, bool cambiar)
        {
            if (!esperandoCambioHumano || asiento != asientoDecisionCambio) return;
            cambioHumanoConfirmado = cambiar;
            esperandoCambioHumano = false;
        }

        private void EjecutarCambio(int jugadorIdx, Carta cartaPropia)
        {
            List<Carta> mano = manosJugadores[jugadorIdx];
            Carta cartaRecibida = triunfoVisible;

            mano.Remove(cartaPropia);
            mano.Add(cartaRecibida);
            triunfoVisible = cartaPropia;

            Debug.Log($"[TurnManager] J{jugadorIdx + 1} cambia el {cartaPropia.Rango} de triunfo por la pinta ({cartaRecibida}).");
            OnCambioTriunfo?.Invoke(jugadorIdx, cartaPropia, cartaRecibida);
        }

        // =====================================================================
        // Utilidades publicas
        // =====================================================================

        /// <summary>Equipo (1 = pareja A, jugadores 1 y 3; 2 = pareja B) que gano la ultima baza de la mano; 0 hasta entonces.</summary>
        public int EquipoQueHizoUltimas { get; private set; }

        public int GetPuntosPareja(int parejaIdx) =>
            parejaIdx == 0 ? puntosPareja_A : puntosPareja_B;

        public GamePhase GetFaseActual() => faseActual;

        public EstadoBaza GetBazaActual() => bazaActual;
    }
}
