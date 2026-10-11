using System;
using System.Collections.Generic;

namespace GuinoteApp.Core
{
    /// <summary>
    /// IA de dificultad Experto: el jugador que "ve" las posibilidades. No hace trampas
    /// (no mira las manos ajenas); en su lugar, por cada carta legal simula muchas manos
    /// posibles hasta el final con <see cref="BuscadorMonteCarlo"/> y se queda con la que,
    /// de media, deja mejor diferencia de puntos a su pareja. Usa la busqueda en toda la
    /// partida (Robo y Arrastre). Las decisiones de cante y cambio son las del nivel Dificil.
    /// </summary>
    public class IAExperto : IEstrategiaIA
    {
        /// <summary>Presupuesto de jugadas simuladas por decision (limita el tiempo de calculo).</summary>
        private const int PresupuestoJugadas = 12000;
        private const int MuestrasMinimas = 24;
        private const int MuestrasMaximas = 150;

        /// <summary>Tiempo maximo de calculo por decision (ms); evita congelar el juego en equipos lentos.</summary>
        private const int TiempoMaximoMs = 250;

        private readonly IADificil politicaReferencia;
        private readonly BuscadorMonteCarlo buscador;
        private readonly int muestrasFijas;

        /// <param name="random">Generador aleatorio (inyectable para tests deterministas).</param>
        /// <param name="muestras">Numero de repartos a simular por decision; 0 = adaptativo.</param>
        public IAExperto(Random random = null, int muestras = 0)
        {
            Random rng = random ?? new Random();
            politicaReferencia = new IADificil(rng, busquedaEnArrastre: false);
            buscador = new BuscadorMonteCarlo(rng, politicaReferencia);
            muestrasFijas = muestras;
        }

        public Carta ElegirCarta(ContextoIA ctx)
        {
            if (ctx?.Mano == null || ctx.Mano.Count == 0)
                return null;

            List<Carta> validas = HeuristicasIA.JugadasValidas(ctx);
            if (validas.Count == 1)
                return validas[0];

            int muestras = muestrasFijas > 0
                ? muestrasFijas
                : BuscadorMonteCarlo.CalcularMuestras(
                    ctx.Mano.Count, validas.Count, PresupuestoJugadas, MuestrasMinimas, MuestrasMaximas);

            // Si el contexto es incoherente (no se pueden repartir las cartas ocultas) cae a la
            // estrategia heuristica
            return buscador.Buscar(ctx, validas, muestras, TiempoMaximoMs) ?? politicaReferencia.ElegirCarta(ctx);
        }

        public Palo? DecidirCante(ContextoIA ctx, IReadOnlyList<Palo> palosDisponibles)
        {
            return HeuristicasIA.MejorCante(palosDisponibles, ctx.Triunfo);
        }

        public bool DecidirCambio(ContextoIA ctx, Carta cartaPropia) => true;
    }
}
