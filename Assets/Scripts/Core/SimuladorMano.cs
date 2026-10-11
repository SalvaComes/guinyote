using System.Collections.Generic;

namespace GuinoteApp.Core
{
    /// <summary>
    /// Motor minimo de una mano de Guiñote pensado para simular partidas completas desde un
    /// estado dado (lo usa <see cref="IAExperto"/> y las pruebas de nivel de las IAs).
    /// Reproduce el flujo de TurnManager: bazas, cambio de la pinta, robo (el ultimo en
    /// robar se lleva la pinta), cantes, paso a Fase Arrastre y las 10 de ultimas.
    /// Si <see cref="Estrategias"/> es null no hay cantes ni cambios.
    /// Clase pura sin dependencias de Unity.
    /// </summary>
    public sealed class SimuladorMano
    {
        public List<Carta>[] Manos { get; }
        public List<Carta> Mazo { get; }
        public Carta Pinta { get; private set; }
        public Palo Triunfo { get; }
        public GamePhase Fase { get; private set; }
        public EstadoBaza Baza { get; private set; }
        public int Turno { get; private set; }
        public bool Terminada { get; private set; }

        /// <summary>Puntos acumulados por equipo, cantes incluidos (0: jugadores 0 y 2; 1: jugadores 1 y 3).</summary>
        public int[] Puntos { get; } = new int[2];

        public List<Carta> Historial { get; } = new List<Carta>(40);

        /// <summary>Palos cuyo cante ya se anuncio.</summary>
        public HashSet<Palo> PalosCantados { get; } = new HashSet<Palo>();

        /// <summary>Estrategia de cada jugador para decidir cantes y cambios; null = sin cantes ni cambios.</summary>
        public IEstrategiaIA[] Estrategias { get; set; }

        public static int Equipo(int jugador) => jugador % 2;

        public SimuladorMano(
            List<Carta>[] manos, List<Carta> mazo, Carta pinta, Palo triunfo,
            GamePhase fase, EstadoBaza baza, int turno)
        {
            Manos = manos;
            Mazo = mazo;
            Pinta = pinta;
            Triunfo = triunfo;
            Fase = fase;
            Baza = baza;
            Turno = turno;
        }

        /// <summary>Informacion publica que veria el jugador <paramref name="jugador"/> ahora mismo.</summary>
        public ContextoIA CrearContexto(int jugador)
        {
            int equipo = Equipo(jugador);
            return new ContextoIA
            {
                JugadorIdx = jugador,
                Mano = Manos[jugador],
                Baza = Baza,
                Fase = Fase,
                Triunfo = Triunfo,
                CartasJugadas = Historial,
                PuntosEquipo = Puntos[equipo],
                PuntosRival = Puntos[1 - equipo],
                PintaVisible = Pinta,
                CartasEnMazo = Mazo.Count,
                PalosCantados = PalosCantados
            };
        }

        /// <summary>El jugador que tiene el turno juega <paramref name="carta"/>.</summary>
        public void Jugar(Carta carta)
        {
            Manos[Turno].Remove(carta);
            Baza.RegistrarJugada(Turno, carta, Triunfo);

            if (!Baza.EstaCompleta)
            {
                Turno = (Turno + 1) % 4;
                return;
            }

            int ganador = Baza.ObtenerGanador();
            Puntos[Equipo(ganador)] += Baza.PuntosEnMesa;
            foreach (var (_, c) in Baza.CartasJugadas)
                Historial.Add(c);

            if (Manos[ganador].Count == 0)
            {
                Puntos[Equipo(ganador)] += 10; // las 10 de ultimas
                Terminada = true;
                return;
            }

            if (Fase == GamePhase.FaseRobo)
            {
                if (Estrategias != null)
                    IntentarCambios(ganador);
                Robar(ganador);
            }

            Baza = new EstadoBaza();
            Turno = ganador;

            if (Estrategias != null)
                IntentarCantar(ganador);
        }

        // =====================================================================
        // Cantes y cambios
        // =====================================================================

        /// <summary>
        /// Pueden cantar los dos jugadores de la pareja que gano la baza (primero quien abre y
        /// luego su compañero), un solo palo por baza.
        /// </summary>
        private void IntentarCantar(int quienAbre)
        {
            foreach (int jugador in new[] { quienAbre, (quienAbre + 2) % 4 })
            {
                List<Palo> cantables = ReglasEspeciales.PalosCantables(Manos[jugador], PalosCantados);
                if (cantables.Count == 0) continue;

                Palo? elegido = Estrategias[jugador].DecidirCante(CrearContexto(jugador), cantables);
                if (elegido == null || !cantables.Contains(elegido.Value)) continue;

                PalosCantados.Add(elegido.Value);
                Puntos[Equipo(jugador)] += ReglasEspeciales.PuntosCante(elegido.Value, Triunfo);
                return;
            }
        }

        private void IntentarCambios(int ganador)
        {
            int[] pareja = Equipo(ganador) == 0 ? new[] { 0, 2 } : new[] { 1, 3 };
            foreach (int idx in pareja)
            {
                if (Pinta == null) return;
                Carta propia = ReglasEspeciales.CartaDeCambio(Manos[idx], Pinta, Triunfo);
                if (propia == null) continue;
                if (!Estrategias[idx].DecidirCambio(CrearContexto(idx), propia)) continue;

                Manos[idx].Remove(propia);
                Manos[idx].Add(Pinta);
                Pinta = propia;
            }
        }

        private void Robar(int ganador)
        {
            for (int i = 0; i < 4; i++)
            {
                int idx = (ganador + i) % 4;
                Carta robada = null;
                if (Mazo.Count > 0)
                {
                    robada = Mazo[0];
                    Mazo.RemoveAt(0);
                }
                else if (Pinta != null)
                {
                    robada = Pinta;
                    Pinta = null;
                }
                if (robada != null)
                    Manos[idx].Add(robada);
            }

            if (Mazo.Count == 0 && Pinta == null)
                Fase = GamePhase.FaseArrastre;
        }
    }
}
