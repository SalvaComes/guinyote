using System;
using System.Collections.Generic;

namespace GuinoteApp.Core
{
    // =========================================================================
    // Interfaz de estrategia — permite sustituir la IA sin tocar el TurnManager
    // =========================================================================

    /// <summary>
    /// Contrato que debe cumplir cualquier estrategia de IA.
    /// Recibe el estado de la partida y devuelve la carta que decide jugar.
    /// </summary>
    public interface IEstrategiaIA
    {
        /// <summary>
        /// Elige la carta a jugar.
        /// </summary>
        /// <param name="jugadorIdx">Indice (0-3) del jugador IA que decide.</param>
        /// <param name="mano">Mano del jugador IA.</param>
        /// <param name="estadoBaza">Estado actual de la baza en curso.</param>
        /// <param name="fase">Fase actual de la partida.</param>
        /// <param name="triunfo">Palo de triunfo.</param>
        /// <param name="cartasJugadasEnLaMano">
        /// Todas las cartas jugadas por cualquier jugador desde el reparto de esta mano
        /// (bazas anteriores incluidas, sin incluir la baza en curso). Permite a las IA
        /// mas avanzadas contar cartas (p.ej. cuantos triunfos quedan por salir).
        /// </param>
        /// <returns>La carta elegida. Nunca null si la mano tiene cartas.</returns>
        Carta ElegirCarta(
            int jugadorIdx,
            List<Carta> mano,
            EstadoBaza estadoBaza,
            GamePhase fase,
            Palo triunfo,
            IReadOnlyList<Carta> cartasJugadasEnLaMano);
    }

    // =========================================================================
    // Niveles de dificultad
    // =========================================================================

    /// <summary>Nivel de dificultad de una estrategia de IA.</summary>
    public enum NivelDificultadIA { Facil, Normal, Dificil }

    /// <summary>Fabrica de estrategias de IA a partir de un nivel de dificultad.</summary>
    public static class IAFactory
    {
        public static IEstrategiaIA Crear(NivelDificultadIA nivel, Random rng = null)
        {
            switch (nivel)
            {
                case NivelDificultadIA.Facil: return new IAFacil(rng);
                case NivelDificultadIA.Dificil: return new IADificil(rng);
                default: return new IANormal(rng);
            }
        }
    }
}
