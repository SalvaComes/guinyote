using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace GuinoteApp.Core
{
    /// <summary>
    /// Elige una carta simulando manos completas ("determinizacion"): reparte al azar las cartas
    /// que la IA no ha visto entre los demas jugadores y el mazo (respetando cuantas cartas tiene
    /// cada uno y que la pinta es lo ultimo que se roba), juega cada carta candidata y termina la
    /// mano con una estrategia de referencia. Gana la carta que, de media, deja mejor diferencia
    /// de puntos a la pareja de la IA. Los mismos repartos se reutilizan para todas las
    /// candidatas, de modo que la comparacion es justa.
    /// No usa informacion oculta: solo lo que ve un jugador sentado a la mesa.
    /// </summary>
    public sealed class BuscadorMonteCarlo
    {
        private readonly Random rng;
        private readonly IEstrategiaIA politica;
        private readonly IEstrategiaIA[] estrategiasSim;

        /// <param name="random">Generador usado para repartir las cartas desconocidas.</param>
        /// <param name="politicaReferencia">Estrategia con la que los 4 jugadores terminan la mano simulada.
        /// No debe usar a su vez una busqueda (seria recursivo).</param>
        public BuscadorMonteCarlo(Random random, IEstrategiaIA politicaReferencia)
        {
            rng = random;
            politica = politicaReferencia;
            estrategiasSim = new[] { politica, politica, politica, politica };
        }

        /// <summary>Numero de repartos que cabe simular con un presupuesto de jugadas simuladas.</summary>
        public static int CalcularMuestras(
            int cartasEnMano, int candidatas, int presupuestoJugadas, int minimo, int maximo)
        {
            int jugadasPorMano = Math.Max(1, cartasEnMano * 4);
            int muestras = presupuestoJugadas / Math.Max(1, candidatas * jugadasPorMano);
            return Math.Max(minimo, Math.Min(maximo, muestras));
        }

        /// <summary>Repartos que se simulan como minimo aunque se agote el tiempo.</summary>
        private const int RepartosMinimosConTope = 8;

        /// <summary>
        /// Devuelve la mejor de las <paramref name="candidatas"/> tras simular
        /// <paramref name="muestras"/> repartos, o null si el contexto es incoherente
        /// (las cartas desconocidas no cuadran con las manos y el mazo).
        /// </summary>
        /// <param name="tiempoMaxMs">Si es mayor que 0, deja de simular al superar ese tiempo
        /// (tras un minimo de repartos), para no congelar el juego en equipos lentos.</param>
        public Carta Buscar(ContextoIA ctx, List<Carta> candidatas, int muestras, int tiempoMaxMs = 0)
        {
            if (!TryMuestrear(ctx, out List<Carta> pool, out int[] tamanos))
                return null;

            var total = new double[candidatas.Count];
            int miEquipo = SimuladorMano.Equipo(ctx.JugadorIdx);
            Stopwatch reloj = tiempoMaxMs > 0 ? Stopwatch.StartNew() : null;

            for (int s = 0; s < muestras; s++)
            {
                if (reloj != null && s >= RepartosMinimosConTope && reloj.ElapsedMilliseconds > tiempoMaxMs)
                    break;

                Barajar(pool);
                for (int k = 0; k < candidatas.Count; k++)
                {
                    SimuladorMano sim = CrearSimulacion(ctx, pool, tamanos);
                    sim.Jugar(candidatas[k]);
                    while (!sim.Terminada)
                        sim.Jugar(politica.ElegirCarta(sim.CrearContexto(sim.Turno)));

                    total[k] += sim.Puntos[miEquipo] - sim.Puntos[1 - miEquipo];
                }
            }

            int mejor = 0;
            for (int k = 1; k < candidatas.Count; k++)
                if (total[k] > total[mejor]) mejor = k;

            return candidatas[mejor];
        }

        // =====================================================================
        // Muestreo de la informacion oculta
        // =====================================================================

        /// <summary>
        /// Construye el conjunto de cartas desconocidas y el tamaño de la mano de cada jugador.
        /// Devuelve false si los datos del contexto no cuadran (no se puede simular).
        /// </summary>
        private static bool TryMuestrear(ContextoIA ctx, out List<Carta> pool, out int[] tamanos)
        {
            bool[] vistas = HeuristicasIA.CartasVistas(ctx);

            pool = new List<Carta>(HeuristicasIA.TotalCartas);
            foreach (Palo palo in Enum.GetValues(typeof(Palo)))
                foreach (Rango rango in Enum.GetValues(typeof(Rango)))
                    if (!vistas[(int)palo * HeuristicasIA.RangosCount + (int)rango])
                        pool.Add(new Carta(palo, rango));

            tamanos = new int[4];
            int enManos = 0;
            for (int p = 0; p < 4; p++)
            {
                if (p == ctx.JugadorIdx) continue;
                bool yaJugo = false;
                foreach (var (idx, _) in ctx.Baza.CartasJugadas)
                    if (idx == p) { yaJugo = true; break; }

                tamanos[p] = ctx.Mano.Count - (yaJugo ? 1 : 0);
                enManos += tamanos[p];
            }

            return pool.Count == enManos + ctx.CartasEnMazo;
        }

        private void Barajar(List<Carta> lista)
        {
            for (int n = lista.Count - 1; n > 0; n--)
            {
                int k = rng.Next(n + 1);
                (lista[k], lista[n]) = (lista[n], lista[k]);
            }
        }

        /// <summary>Crea una mano simulable a partir del reparto actual del pool barajado.</summary>
        private SimuladorMano CrearSimulacion(ContextoIA ctx, List<Carta> pool, int[] tamanos)
        {
            var manos = new List<Carta>[4];
            int cursor = 0;
            for (int p = 0; p < 4; p++)
            {
                if (p == ctx.JugadorIdx)
                {
                    manos[p] = new List<Carta>(ctx.Mano);
                    continue;
                }
                manos[p] = pool.GetRange(cursor, tamanos[p]);
                cursor += tamanos[p];
            }

            List<Carta> mazo = pool.GetRange(cursor, ctx.CartasEnMazo);

            var baza = new EstadoBaza();
            foreach (var (idx, carta) in ctx.Baza.CartasJugadas)
                baza.RegistrarJugada(idx, carta, ctx.Triunfo);

            var sim = new SimuladorMano(
                manos, mazo, ctx.PintaVisible, ctx.Triunfo, ctx.Fase, baza, ctx.JugadorIdx);

            // Las IAs simuladas deben recordar lo que ya salio antes de este punto
            sim.Historial.AddRange(ctx.CartasJugadas);

            // Cantes y cambios futuros cuentan en la simulacion; los ya anunciados no se repiten
            foreach (Palo palo in ctx.PalosCantados)
                sim.PalosCantados.Add(palo);
            sim.Estrategias = estrategiasSim;
            return sim;
        }
    }
}
