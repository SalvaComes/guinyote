using System.Collections.Generic;
using UnityEngine;
using GuinoteApp.Core;
using GuinoteApp.View;
using GuinoteApp.UI;
using GuinoteApp.Network;

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

        // Gestor del Coto (Malas/Buenas, Juegos ganados). Si no esta en la escena se crea al arrancar.
        private MatchManager matchManager;

        // Jugador que abre la primera baza de la ultima mano (para rotar la salida entre manos)
        private int ultimoJugadorInicial = -1;

        // Ganador de la ultima baza de la mano en curso (abre la primera mano del siguiente Juego)
        private int ganadorUltimaBaza = -1;
        private int salidaForzadaSiguienteMano = -1;

        // Nivel de IA elegido en la pantalla de seleccion para J2, J3 (compañero) y J4
        private readonly NivelDificultadIA[] nivelesIA =
            { NivelDificultadIA.Normal, NivelDificultadIA.Normal, NivelDificultadIA.Normal };

        // Pinta que hay ahora mismo sobre la mesa (cambia si alguien hace el cambio del 7/2)
        private Carta triunfoEnMesa;
        private bool ultimaRondaMostrada;

        /// <summary>Textos del sorteo de quien sale, segun la posicion en la mesa
        /// (el turno avanza 0→1→2→3: J2 a tu derecha, J3 enfrente, J4 a tu izquierda).</summary>
        private readonly string[] textosSalida =
        {
            "Sorteo: sales tú",
            "Sorteo: sale el rival de tu derecha",
            "Sorteo: sale tu compañero",
            "Sorteo: sale el rival de tu izquierda"
        };

        // =====================================================================
        // Ciclo de vida Unity
        // =====================================================================

        private void Awake()
        {
            matchManager = GetComponent<MatchManager>();
            if (matchManager == null)
                matchManager = gameObject.AddComponent<MatchManager>();
            matchManager.OnRondaEvaluada += ManejarRondaEvaluada;

            // Suscribirse al evento "Comenzar partida" de la pantalla de seleccion
            if (pantallaSeleccion != null)
            {
                pantallaSeleccion.OnComenzarPartida += OnComenzarPartidaDesdeSeleccion;
                pantallaSeleccion.OnAbrirMultijugador += AbrirLobbyMultijugador;
            }
        }

        // =====================================================================
        // Multijugador (anfitrion-autoridad): este dispositivo corre toda la partida y los
        // invitados reciben los eventos y envian sus jugadas (ver PartidaRedAnfitrion / Cliente)
        // =====================================================================

        // Sesion de red del anfitrion; null en partidas en solitario
        private PartidaRedAnfitrion partidaRed;
        private LobbyRed lobby;

        private void AbrirLobbyMultijugador()
        {
            CerrarSesionRed(); // una sesion anterior compartiria el RedManager con la nueva
            if (lobby == null)
                lobby = gameObject.AddComponent<LobbyRed>();

            lobby.Abrir(
                cartaVisualManager, hudMesa, uiManager,
                () => pantallaSeleccion != null ? pantallaSeleccion.NivelesJugador : null,
                ComenzarPartidaRedAnfitrion);
        }

        /// <summary>
        /// El anfitrion empieza la partida con los humanos que se han unido a la sala; los asientos
        /// sin humano los juega la IA con los niveles elegidos en la pantalla de seleccion.
        /// </summary>
        private void ComenzarPartidaRedAnfitrion(DificultadIA[] dificultades, PartidaRedAnfitrion red)
        {
            partidaRed = red;
            partidaRed.OnJugadorDesconectado += asiento =>
            {
                if (hudMesa != null)
                    hudMesa.MostrarAviso($"Jugador {asiento + 1} se ha ido: lo sustituye la IA");
            };

            turnManager.ConfigurarHumanos(partidaRed.AsientosHumanos());
            partidaRed.Iniciar(turnManager,
                () => manosJugadores,
                () => baraja.CartasRestantes(),
                () => triunfoEnMesa);

            AplicarDificultadesYEmpezarCoto(dificultades);
        }

        /// <summary>Cierra la sesion de red del anfitrion (si la hay) y avisa a los invitados.</summary>
        private void CerrarSesionRed()
        {
            if (partidaRed == null) return;
            partidaRed.Cerrar();
            partidaRed = null;
        }

        private MarcadorDto CrearMarcadorDto(int puntosManoA, int puntosManoB) => new MarcadorDto
        {
            puntosEq1 = MarcadorEquipo.Describir(matchManager.MarcadorEquipo1.PuntosTotales + puntosManoA),
            puntosEq2 = MarcadorEquipo.Describir(matchManager.MarcadorEquipo2.PuntosTotales + puntosManoB),
            juegosEq1 = matchManager.JuegosGanadosEquipo1,
            juegosEq2 = matchManager.JuegosGanadosEquipo2
        };

        // =====================================================================
        // Arranque de partida
        // =====================================================================

        /// <summary>
        /// Callback desde PantallaSeleccion cuando el jugador pulsa "Comenzar partida".
        /// </summary>
        private void OnComenzarPartidaDesdeSeleccion(DificultadIA[] dificultades)
        {
            // Partida en solitario: solo el asiento 0 es humano
            CerrarSesionRed();
            turnManager.ConfigurarHumanos(new[] { 0 });
            AplicarDificultadesYEmpezarCoto(dificultades);
        }

        private void AplicarDificultadesYEmpezarCoto(DificultadIA[] dificultades)
        {
            for (int i = 0; i < nivelesIA.Length && i < dificultades.Length; i++)
                nivelesIA[i] = ConvertirDificultad(dificultades[i]);

            Debug.Log($"[GameManager] Iniciando partida. IA: J2={nivelesIA[0]} J3={nivelesIA[1]} J4={nivelesIA[2]}");
            matchManager.IniciarCoto();
            ultimoJugadorInicial = -1;
            ganadorUltimaBaza = -1;
            salidaForzadaSiguienteMano = -1;
            IniciarPartida();
        }

        private static NivelDificultadIA ConvertirDificultad(DificultadIA dificultad)
        {
            switch (dificultad)
            {
                case DificultadIA.Facil:   return NivelDificultadIA.Facil;
                case DificultadIA.Dificil: return NivelDificultadIA.Dificil;
                case DificultadIA.Experto: return NivelDificultadIA.Experto;
                default:                   return NivelDificultadIA.Normal;
            }
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
            triunfoEnMesa = triunfo;
            ultimaRondaMostrada = false;
            Debug.Log($"Triunfo: {triunfo} | Cartas en mazo: {baraja.CartasRestantes()}");

            // ── 3b. Sorteo: quien sale en la primera baza ──────────────────
            // Primera mano del Coto: sorteo; entre manos la salida pasa al siguiente jugador;
            // al empezar un nuevo Juego sale quien gano la ultima baza del Juego anterior
            int jugadorInicial;
            if (salidaForzadaSiguienteMano >= 0)
                jugadorInicial = salidaForzadaSiguienteMano;
            else
                jugadorInicial = ultimoJugadorInicial < 0 ? Random.Range(0, 4) : (ultimoJugadorInicial + 1) % 4;
            salidaForzadaSiguienteMano = -1;
            ultimoJugadorInicial = jugadorInicial;
            Debug.Log($"[GameManager] {textosSalida[jugadorInicial]} (J{jugadorInicial + 1})");

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
                // -= antes de += : IniciarPartida se ejecuta en cada partida y sin esto los
                // handlers se acumularian (cada carta jugada dispararia N veces).
                cartaVisualManager.OnJugadorSeleccionoCarta -= ManejarCartaSeleccionada;
                cartaVisualManager.OnJugadorSeleccionoCarta += ManejarCartaSeleccionada;
                cartaVisualManager.OnJugadorArrastroCartaAJugar -= ManejarCartaArrastradaAJugar;
                cartaVisualManager.OnJugadorArrastroCartaAJugar += ManejarCartaArrastradaAJugar;
                Debug.Log("[GameManager] Capa visual lista.");
            }

            // ── 6. HUD ─────────────────────────────────────────────────────
            if (hudMesa != null)
            {
                hudMesa.Resetear();
                ActualizarMarcadorHUD(0, 0);
                hudMesa.ActualizarMazo(baraja.CartasRestantes());
                hudMesa.MostrarAviso(textosSalida[jugadorInicial]);
            }

            // ── 7. TurnManager ─────────────────────────────────────────────
            if (turnManager != null)
            {
                SuscribirEventosTurnManager();
                turnManager.ConfigurarDificultades(nivelesIA[0], nivelesIA[1], nivelesIA[2]);
                // Los invitados reciben su mano antes de que arranque la primera baza
                partidaRed?.EnviarReparto(manosJugadores, triunfo, baraja.CartasRestantes(),
                    jugadorInicial, CrearMarcadorDto(0, 0));

                turnManager.ConfigurarPuntosPrevios(
                    matchManager.MarcadorEquipo1.PuntosTotales, matchManager.MarcadorEquipo2.PuntosTotales,
                    matchManager.EsManoDeVueltas);
                turnManager.Inicializar(manosJugadores, baraja, triunfo, GamePhase.FaseRobo, jugadorInicial);
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
            // Evita suscripciones duplicadas al reiniciar partida
            turnManager.OnTurnoJugadorHumano -= HabilitarInteraccionHumano;
            turnManager.OnCartaJugada        -= ManejarCartaJugadaPorCualquiera;
            turnManager.OnBazaResuelta       -= ManejarBazaResuelta;
            turnManager.OnCambioFase         -= ManejarCambioFase;
            turnManager.OnFinPartida         -= ManejarFinPartida;
            turnManager.OnCante              -= ManejarCante;
            turnManager.OnCambioTriunfo      -= ManejarCambioTriunfo;
            turnManager.OnManosActualizadas  -= ActualizarVisualesDeManos;
            turnManager.OnJugadorRobo        -= ManejarJugadorRobo;
            turnManager.OnDecisionCanteHumano  -= ManejarDecisionCante;
            turnManager.OnDecisionCambioHumano -= ManejarDecisionCambio;

            turnManager.OnTurnoJugadorHumano += HabilitarInteraccionHumano;
            turnManager.OnCartaJugada        += ManejarCartaJugadaPorCualquiera;
            turnManager.OnBazaResuelta       += ManejarBazaResuelta;
            turnManager.OnCambioFase         += ManejarCambioFase;
            turnManager.OnFinPartida         += ManejarFinPartida;
            turnManager.OnCante              += ManejarCante;
            turnManager.OnCambioTriunfo      += ManejarCambioTriunfo;
            turnManager.OnManosActualizadas  += ActualizarVisualesDeManos;
            turnManager.OnJugadorRobo        += ManejarJugadorRobo;
            turnManager.OnDecisionCanteHumano  += ManejarDecisionCante;
            turnManager.OnDecisionCambioHumano += ManejarDecisionCambio;
        }

        /// <summary>El humano puede cantar: se le pregunta (o, sin HUD, se canta lo mejor).
        /// Los asientos remotos los gestiona PartidaRedAnfitrion.</summary>
        private void ManejarDecisionCante(int asiento, IReadOnlyList<Palo> palos)
        {
            if (asiento != 0) return;

            if (hudMesa == null)
            {
                turnManager.ResponderCanteHumano(HeuristicasIA.MejorCante(palos, triunfo.Palo));
                return;
            }
            hudMesa.PedirCante(palos, triunfo.Palo, palo => turnManager.ResponderCanteHumano(palo));
        }

        /// <summary>El humano puede cambiar la pinta: se le pregunta (o, sin HUD, se cambia).</summary>
        private void ManejarDecisionCambio(int asiento, Carta cartaPropia, Carta pinta)
        {
            if (asiento != 0) return;

            if (hudMesa == null)
            {
                turnManager.ResponderCambioHumano(true);
                return;
            }
            hudMesa.PedirCambio(cartaPropia, pinta, cambiar => turnManager.ResponderCambioHumano(cambiar));
        }

        private void ManejarJugadorRobo(int jugadorIdx, bool esPinta)
        {
            AudioManager.Reproducir(TipoSonido.CartaRobar);

            if (cartaVisualManager == null) return;

            // El mazo baja de grosor con cada carta robada (la pinta no cuenta: no esta en el mazo)
            if (!esPinta)
            {
                int restantes = baraja.CartasRestantes();
                cartaVisualManager.MostrarMazo(restantes, restantes > 0 && restantes < 4);
            }

            cartaVisualManager.AnimarRobo(jugadorIdx, manosJugadores, esPinta);
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
            AudioManager.Reproducir(TipoSonido.CartaLanzar);

            // Mostrar la carta jugada en la posicion de baza del jugador
            if (cartaVisualManager != null)
                cartaVisualManager.MostrarCartaEnBaza(jugadorIdx, carta);

            if (cartaVisualManager != null)
            {
                if (jugadorIdx == 0 && cartaSeleccionada != null)
                {
                    // Jugador humano: quitar la carta concreta de su mano visual
                    cartaVisualManager.QuitarCartaDeMano(cartaSeleccionada);
                    cartaSeleccionada = null;
                }
                else if (jugadorIdx != 0)
                {
                    // IA: reducir su pila de dorsos ya mismo (RegistrarJugada ya quito la
                    // carta de manosJugadores[jugadorIdx] antes de disparar este evento).
                    // Sin esto, el dorso no baja hasta que se resuelve toda la baza.
                    cartaVisualManager.ActualizarDorsosIA(manosJugadores);
                }
            }
        }

        private void ManejarBazaResuelta(int ganador, int puntos, int ptsA, int ptsB)
        {
            string pareja = (ganador == 0 || ganador == 2) ? "A (J1+J3)" : "B (J2+J4)";
            ganadorUltimaBaza = ganador;
            if (manosJugadores[0].Count == 0)
                AudioManager.Reproducir(TipoSonido.UltimasBaza);
            Debug.Log($"[GameManager] Baza → J{ganador + 1} | Pareja {pareja} | A:{ptsA} B:{ptsB}");

            // Las cuatro cartas viajan hacia el ganador y desaparecen
            if (cartaVisualManager != null)
                cartaVisualManager.AnimarBazaHaciaGanador(ganador);

            // Si quedan menos cartas boca abajo que jugadores, el robo que viene es el ultimo
            // (el ultimo en robar se lleva la pinta) y despues empieza el arrastre.
            int restantes = baraja.CartasRestantes();
            if (hudMesa != null && restantes > 0 && restantes < 4)
                hudMesa.MostrarAviso("Última ronda de robo: después, arrastre");

            // Estado inmediato tras jugar (util sobre todo en Arrastre, donde no hay robo
            // y esta es la unica actualizacion). En Fase de Robo, ActualizarVisualesDeManos()
            // se vuelve a llamar cuando OnManosActualizadas avisa de que ya se ha robado.
            ActualizarVisualesDeManos();

            ActualizarMarcadorHUD(ptsA, ptsB);
        }

        /// <summary>
        /// Pinta el marcador del Coto: puntos acumulados en el Juego mas los de la mano en curso,
        /// en Malas/Buenas, y los Juegos ganados por cada equipo.
        /// </summary>
        private void ActualizarMarcadorHUD(int puntosManoA, int puntosManoB)
        {
            if (matchManager == null) return;
            partidaRed?.Difundir(TipoMensajeRed.Marcador, CrearMarcadorDto(puntosManoA, puntosManoB));

            if (hudMesa == null) return;

            hudMesa.ActualizarMarcadorCoto(
                MarcadorEquipo.Describir(matchManager.MarcadorEquipo1.PuntosTotales + puntosManoA),
                MarcadorEquipo.Describir(matchManager.MarcadorEquipo2.PuntosTotales + puntosManoB),
                matchManager.JuegosGanadosEquipo1,
                matchManager.JuegosGanadosEquipo2);
        }

        /// <summary>Refresca mano del humano, dorsos de IA y contador de mazo (Canvas y mundo 3D).</summary>
        private void ActualizarVisualesDeManos()
        {
            int restantes = baraja.CartasRestantes();
            bool ultimaRonda = restantes > 0 && restantes < 4;

            if (cartaVisualManager != null)
            {
                cartaVisualManager.MostrarManoJugador(manosJugadores[0]);
                cartaVisualManager.ActualizarDorsosIA(manosJugadores);
                cartaVisualManager.MostrarMazo(restantes, ultimaRonda);

                // Resaltar la pinta solo cuando cambia el estado (y mientras siga en la mesa:
                // tras el ultimo robo ManejarCambioFase la retira y restantes es 0)
                if (restantes > 0 && ultimaRonda != ultimaRondaMostrada)
                {
                    cartaVisualManager.MostrarTriunfo(triunfoEnMesa, ultimaRonda);
                    ultimaRondaMostrada = ultimaRonda;
                }
            }

            if (hudMesa != null)
                hudMesa.ActualizarMazo(restantes, ultimaRonda);
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
            {
                hudMesa.ActualizarMazo(0);
                hudMesa.MostrarAviso("¡Arrastre! Hay que asistir y montar");
            }
        }

        /// <summary>Fin de una mano: se anotan los puntos en el Coto (el MatchManager decide si hay Juego o Coto).</summary>
        private void ManejarFinPartida(int ptsA, int ptsB)
        {
            Debug.Log($"[GameManager] FIN DE MANO — Equipo 1:{ptsA} | Equipo 2:{ptsB}");
            ultimosPuntosMano = (ptsA, ptsB);
            matchManager.EvaluarFinDeRonda(ptsA, ptsB, turnManager.EquipoQueHizoUltimas,
                turnManager.PuntosBazasSinCantes(1), turnManager.PuntosBazasSinCantes(2));
        }

        private (int a, int b) ultimosPuntosMano;

        /// <summary>Tras evaluar la mano: se muestra el marcador y se ofrece seguir con la siguiente mano o Juego.</summary>
        private void ManejarRondaEvaluada(ResultadoRonda r)
        {
            if (r.GanadorJuego != 0)
                salidaForzadaSiguienteMano = ganadorUltimaBaza;

            if (r.GanadorCoto != 0)
                AudioManager.Reproducir(r.GanadorCoto == 1 ? TipoSonido.Victoria : TipoSonido.Derrota);

            // Resultado para los invitados (con el marcador de antes de reiniciar el Juego)
            partidaRed?.Difundir(TipoMensajeRed.FinDeMano, new FinManoDto
            {
                ptsManoEq1 = ultimosPuntosMano.a,
                ptsManoEq2 = ultimosPuntosMano.b,
                ganadorJuego = r.GanadorJuego,
                ganadorCoto = r.GanadorCoto,
                juegosEq1 = r.JuegosEquipo1,
                juegosEq2 = r.JuegosEquipo2,
                resumen = r.Resumen,
                marcador = new MarcadorDto
                {
                    puntosEq1 = MarcadorEquipo.Describir(r.PuntosTotalesEquipo1),
                    puntosEq2 = MarcadorEquipo.Describir(r.PuntosTotalesEquipo2),
                    juegosEq1 = r.JuegosEquipo1 - (r.GanadorJuego == 1 ? 1 : 0),
                    juegosEq2 = r.JuegosEquipo2 - (r.GanadorJuego == 2 ? 1 : 0)
                }
            });

            if (hudMesa == null) return;

            // El marcador muestra el estado de la mano que acaba de terminar, antes de reiniciar el Juego
            hudMesa.ActualizarMarcadorCoto(
                MarcadorEquipo.Describir(r.PuntosTotalesEquipo1),
                MarcadorEquipo.Describir(r.PuntosTotalesEquipo2),
                r.JuegosEquipo1 - (r.GanadorJuego == 1 ? 1 : 0),
                r.JuegosEquipo2 - (r.GanadorJuego == 2 ? 1 : 0));

            string detalleMano = $"Esta mano: nosotros {ultimosPuntosMano.a} · ellos {ultimosPuntosMano.b}.\n{r.Resumen}";

            if (r.GanadorCoto != 0)
            {
                hudMesa.MostrarFinCoto(r.GanadorCoto == 1, r.JuegosEquipo1, r.JuegosEquipo2);
                return;
            }

            if (r.GanadorJuego != 0)
            {
                hudMesa.PedirContinuar(
                    r.GanadorJuego == 1 ? "¡Ganáis el juego!" : "Ellos ganan el juego",
                    $"{detalleMano}\nJuegos: nosotros {r.JuegosEquipo1} · ellos {r.JuegosEquipo2}.",
                    "Siguiente juego",
                    SiguienteMano);
                return;
            }

            hudMesa.PedirContinuar("Fin de la mano", detalleMano, "Siguiente mano", SiguienteMano);
        }

        private void SiguienteMano()
        {
            IniciarPartida();
        }

        private void ManejarCante(int jugadorIdx, Palo palo, int puntos)
        {
            Debug.Log($"[GameManager] J{jugadorIdx + 1} canta {puntos} en {palo}.");
            AudioManager.Reproducir(puntos >= 40 ? TipoSonido.Cante40 : TipoSonido.Cante20);

            if (hudMesa != null)
            {
                hudMesa.MostrarCante(palo, puntos);
                ActualizarMarcadorHUD(turnManager.GetPuntosPareja(0), turnManager.GetPuntosPareja(1));
            }
        }

        private void ManejarCambioTriunfo(int jugadorIdx, Carta cartaEntregada, Carta cartaRecibida)
        {
            Debug.Log($"[GameManager] J{jugadorIdx + 1} cambia el {cartaEntregada.Rango} por la pinta ({cartaRecibida}).");

            // La nueva pinta es la carta que entrega el jugador (el 7 o el 2); la que "recibe"
            // es la pinta antigua, que pasa a su mano.
            triunfoEnMesa = cartaEntregada;

            if (cartaVisualManager != null)
            {
                cartaVisualManager.MostrarTriunfo(cartaEntregada, ultimaRondaMostrada);
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
        /// El jugador arrastró la carta hasta la zona de juego y la soltó: equivale a
        /// tenerla ya seleccionada y hacer clic de nuevo. Si la jugada no es válida,
        /// IntentarJugarCarta ya la deselecciona, lo que además la devuelve a su sitio
        /// en la mano (CardView.Seleccionar recalcula la posición desde su base real).
        /// </summary>
        private void ManejarCartaArrastradaAJugar(View.CardView cartaVista)
        {
            cartaSeleccionada = cartaVista;
            IntentarJugarCarta(cartaVista);
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
