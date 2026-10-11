using System;
using System.Collections.Generic;

namespace GuinoteApp.Core
{
    /// <summary>
    /// IA de dificultad Facil: el principiante despistado.
    ///   - Respeta las reglas obligatorias, pero juega casi al azar.
    ///   - Cuando se acuerda de intentar ganar la baza, lo hace a ciegas: no mira si su
    ///     compañero ya va ganando (le puede quitar la baza) ni cuanto cuesta la carta.
    ///   - Se olvida de cantar y de cambiar el siete con frecuencia.
    /// </summary>
    public class IAFacil : IEstrategiaIA
    {
        /// <summary>Probabilidad de jugar una carta legal totalmente al azar.</summary>
        private const double ProbAzar = 0.55;
        private const double ProbCantar = 0.65;
        private const double ProbCambiar = 0.50;

        private readonly Random rng;

        public IAFacil(Random random = null)
        {
            rng = random ?? new Random();
        }

        public Carta ElegirCarta(ContextoIA ctx)
        {
            if (ctx?.Mano == null || ctx.Mano.Count == 0)
                return null;

            List<Carta> validas = HeuristicasIA.JugadasValidas(ctx);

            if (ctx.EsApertura || rng.NextDouble() < ProbAzar)
                return validas[rng.Next(validas.Count)];

            // Intento torpe de ganar: cualquier carta ganadora, sin pensar en el compañero
            List<Carta> ganadoras = HeuristicasIA.QuePuedenGanar(ctx, validas);
            if (ganadoras.Count > 0)
                return ganadoras[rng.Next(ganadoras.Count)];

            return validas[rng.Next(validas.Count)];
        }

        public Palo? DecidirCante(ContextoIA ctx, IReadOnlyList<Palo> palosDisponibles)
        {
            if (palosDisponibles == null || palosDisponibles.Count == 0) return null;
            if (rng.NextDouble() > ProbCantar) return null;
            return palosDisponibles[rng.Next(palosDisponibles.Count)];
        }

        public bool DecidirCambio(ContextoIA ctx, Carta cartaPropia)
        {
            return rng.NextDouble() < ProbCambiar;
        }
    }
}
