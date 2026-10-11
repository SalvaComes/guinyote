using System;
using System.Collections.Generic;

namespace GuinoteApp.Core
{
    /// <summary>
    /// IA de dificultad Normal: el jugador de domingo. Juega con sentido comun pero sin
    /// memoria ni planes:
    ///   - Abre con cartas baratas (no malgasta triunfos ni ases).
    ///   - Gana las bazas que valen la pena con la carta mas floja que baste.
    ///   - Mira a su compañero: si ya gana, no le quita la baza.
    ///   - Solo carga puntos sobre la baza de su compañero cuando es el ultimo en jugar
    ///     (unico caso en el que sabe que nadie puede responder).
    ///   - Canta siempre que puede y siempre cambia el siete.
    /// No cuenta cartas ni mira el marcador.
    /// </summary>
    public class IANormal : IEstrategiaIA
    {
        /// <summary>En Fase Robo solo gasta una carta para ganar si la baza lleva al menos esto.</summary>
        private const int PuntosMinimosParaGanarEnRobo = 4;

        /// <summary>En Fase Robo solo gasta un triunfo para ganar si la baza lleva al menos esto.</summary>
        private const int PuntosMinimosParaTriunfarEnRobo = 10;

        private readonly Random rng;

        public IANormal(Random random = null)
        {
            rng = random ?? new Random();
        }

        public Carta ElegirCarta(ContextoIA ctx)
        {
            if (ctx?.Mano == null || ctx.Mano.Count == 0)
                return null;

            List<Carta> validas = HeuristicasIA.JugadasValidas(ctx);
            Palo triunfo = ctx.Triunfo;

            // Abrir: sale con lo mas barato
            if (ctx.EsApertura)
                return HeuristicasIA.MenorCoste(validas, triunfo);

            // El compañero va ganando: solo suma puntos si nadie puede responder ya
            if (ctx.CompaneroGanando)
            {
                return ctx.EsUltimoEnJugar
                    ? HeuristicasIA.MayorValor(validas, triunfo)
                    : HeuristicasIA.MenorCoste(validas, triunfo);
            }

            // Va ganando un rival: intentar ganar si compensa
            List<Carta> ganadoras = HeuristicasIA.QuePuedenGanar(ctx, validas);
            if (ganadoras.Count > 0 && CompensaGanar(ctx, ganadoras))
                return HeuristicasIA.GanadoraMasDebil(ganadoras, triunfo);

            return HeuristicasIA.MenorCoste(validas, triunfo);
        }

        public Palo? DecidirCante(ContextoIA ctx, IReadOnlyList<Palo> palosDisponibles)
        {
            return HeuristicasIA.MejorCante(palosDisponibles, ctx.Triunfo);
        }

        public bool DecidirCambio(ContextoIA ctx, Carta cartaPropia) => true;

        // =====================================================================

        private static bool CompensaGanar(ContextoIA ctx, List<Carta> ganadoras)
        {
            // En Arrastre las cartas jugadas ya estan condenadas a salir: siempre gana
            if (ctx.Fase == GamePhase.FaseArrastre) return true;

            int puntos = ctx.Baza.PuntosEnMesa;
            bool soloConTriunfo = true;
            foreach (Carta c in ganadoras)
                if (c.Palo != ctx.Triunfo) { soloConTriunfo = false; break; }

            int umbral = soloConTriunfo ? PuntosMinimosParaTriunfarEnRobo : PuntosMinimosParaGanarEnRobo;
            return puntos >= umbral;
        }
    }
}
