using System;
using System.Collections.Generic;
using GuinoteApp.Core;

namespace GuinoteApp.Core.Tests
{
    /// <summary>Utilidades para construir situaciones de juego concretas en los tests de IA.</summary>
    public static class IATestHelper
    {
        public static readonly IReadOnlyList<Carta> SinHistorial = new List<Carta>();

        /// <summary>Construye un contexto de IA con los datos relevantes para el test.</summary>
        public static ContextoIA Contexto(
            int jugadorIdx,
            List<Carta> mano,
            EstadoBaza baza,
            GamePhase fase,
            Palo triunfo,
            IReadOnlyList<Carta> historial = null,
            Carta pinta = null,
            int cartasEnMazo = 0,
            IReadOnlyCollection<Palo> palosCantados = null)
        {
            return new ContextoIA
            {
                JugadorIdx = jugadorIdx,
                Mano = mano,
                Baza = baza ?? new EstadoBaza(),
                Fase = fase,
                Triunfo = triunfo,
                CartasJugadas = historial ?? SinHistorial,
                PintaVisible = pinta,
                CartasEnMazo = cartasEnMazo,
                PalosCantados = palosCantados ?? new List<Palo>()
            };
        }

        /// <summary>Atajo con la firma antigua (jugador, mano, baza, fase, triunfo, historial).</summary>
        public static Carta ElegirCarta(
            this IEstrategiaIA ia,
            int jugadorIdx,
            List<Carta> mano,
            EstadoBaza baza,
            GamePhase fase,
            Palo triunfo,
            IReadOnlyList<Carta> historial)
        {
            return ia.ElegirCarta(Contexto(jugadorIdx, mano, baza, fase, triunfo, historial));
        }

        /// <summary>Todas las cartas de la baraja.</summary>
        public static List<Carta> Baraja()
        {
            var mazo = new List<Carta>(40);
            foreach (Palo palo in Enum.GetValues(typeof(Palo)))
                foreach (Rango rango in Enum.GetValues(typeof(Rango)))
                    mazo.Add(new Carta(palo, rango));
            return mazo;
        }

        public static void Barajar(List<Carta> mazo, Random rng)
        {
            for (int n = mazo.Count - 1; n > 0; n--)
            {
                int k = rng.Next(n + 1);
                (mazo[k], mazo[n]) = (mazo[n], mazo[k]);
            }
        }

        /// <summary>
        /// Reparte una mano completa (6 cartas por jugador, pinta y mazo restante) y devuelve
        /// un simulador listo para jugar. El jugador que sale se sortea con <paramref name="rng"/>.
        /// </summary>
        public static SimuladorMano RepartirMano(Random rng, IEstrategiaIA[] estrategias = null)
        {
            List<Carta> mazo = Baraja();
            Barajar(mazo, rng);

            var manos = new List<Carta>[4];
            for (int i = 0; i < 4; i++) manos[i] = new List<Carta>();

            int cursor = 0;
            for (int ronda = 0; ronda < 6; ronda++)
                for (int j = 0; j < 4; j++)
                    manos[j].Add(mazo[cursor++]);

            Carta pinta = mazo[cursor++];
            List<Carta> resto = mazo.GetRange(cursor, mazo.Count - cursor);

            return new SimuladorMano(
                manos, resto, pinta, pinta.Palo, GamePhase.FaseRobo, new EstadoBaza(), rng.Next(4))
            {
                Estrategias = estrategias
            };
        }

        /// <summary>Juega la mano completa con la estrategia de cada asiento.</summary>
        public static void JugarHastaElFinal(SimuladorMano sim, IEstrategiaIA[] estrategias)
        {
            sim.Estrategias = estrategias;
            int seguridad = 0;
            while (!sim.Terminada)
            {
                if (++seguridad > 200)
                    throw new InvalidOperationException("La mano no termina: posible bucle.");

                int turno = sim.Turno;
                Carta carta = estrategias[turno].ElegirCarta(sim.CrearContexto(turno));
                if (carta == null || !sim.Manos[turno].Contains(carta))
                    throw new InvalidOperationException($"Jugada ilegal del asiento {turno}: {carta}");
                sim.Jugar(carta);
            }
        }

        /// <summary>
        /// Enfrenta dos niveles en <paramref name="manos"/> manos, alternando que pareja ocupa
        /// los asientos pares, y devuelve el porcentaje de manos que gana <paramref name="a"/>
        /// (los empates cuentan medio).
        /// </summary>
        public static double PorcentajeVictorias(
            NivelDificultadIA a, NivelDificultadIA b, int manos, int semilla)
        {
            var rng = new Random(semilla);
            double victoriasA = 0;
            for (int m = 0; m < manos; m++)
            {
                bool aEnPares = m % 2 == 0;
                var estrategias = new IEstrategiaIA[4];
                for (int asiento = 0; asiento < 4; asiento++)
                {
                    bool par = asiento % 2 == 0;
                    estrategias[asiento] = IAFactory.Crear(par == aEnPares ? a : b, rng);
                }

                SimuladorMano sim = RepartirMano(rng, estrategias);
                JugarHastaElFinal(sim, estrategias);

                int puntosA = aEnPares ? sim.Puntos[0] : sim.Puntos[1];
                int puntosB = aEnPares ? sim.Puntos[1] : sim.Puntos[0];
                if (puntosA > puntosB) victoriasA += 1;
                else if (puntosA == puntosB) victoriasA += 0.5;
            }
            return 100.0 * victoriasA / manos;
        }
    }
}
