using System;
using System.Collections.Generic;

namespace GuinoteApp.Core
{
    // =========================================================================
    // Interfaz de estrategia — permite sustituir la IA sin tocar el TurnManager
    // =========================================================================

    /// <summary>
    /// Contrato que debe cumplir cualquier estrategia de IA. Cada decision recibe un
    /// <see cref="ContextoIA"/> con la informacion publica de la partida.
    /// </summary>
    public interface IEstrategiaIA
    {
        /// <summary>Elige la carta a jugar. Nunca null si la mano tiene cartas.</summary>
        Carta ElegirCarta(ContextoIA ctx);

        /// <summary>
        /// Decide si canta y que palo. Se consulta al abrir baza si la mano tiene Rey y Sota
        /// de algun palo aun no cantado. Devuelve null para no cantar.
        /// </summary>
        /// <param name="palosDisponibles">Palos cantables ahora (nunca vacio).</param>
        Palo? DecidirCante(ContextoIA ctx, IReadOnlyList<Palo> palosDisponibles);

        /// <summary>
        /// Decide si cambia la pinta por <paramref name="cartaPropia"/> (el 7 o el 2 de triunfo).
        /// Se consulta tras ganar la pareja una baza en Fase Robo.
        /// </summary>
        bool DecidirCambio(ContextoIA ctx, Carta cartaPropia);
    }

    // =========================================================================
    // Niveles de dificultad
    // =========================================================================

    /// <summary>Nivel de dificultad de una estrategia de IA.</summary>
    public enum NivelDificultadIA { Facil, Normal, Dificil, Experto }

    /// <summary>Fabrica de estrategias de IA a partir de un nivel de dificultad.</summary>
    public static class IAFactory
    {
        public static IEstrategiaIA Crear(NivelDificultadIA nivel, Random rng = null)
        {
            switch (nivel)
            {
                case NivelDificultadIA.Facil: return new IAFacil(rng);
                case NivelDificultadIA.Dificil: return new IADificil(rng);
                case NivelDificultadIA.Experto: return new IAExperto(rng);
                default: return new IANormal(rng);
            }
        }
    }
}
