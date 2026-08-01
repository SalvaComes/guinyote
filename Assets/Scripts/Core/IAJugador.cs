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
        /// <param name="mano">Mano del jugador IA.</param>
        /// <param name="estadoBaza">Estado actual de la baza en curso.</param>
        /// <param name="fase">Fase actual de la partida.</param>
        /// <param name="triunfo">Palo de triunfo.</param>
        /// <returns>La carta elegida. Nunca null si la mano tiene cartas.</returns>
        Carta ElegirCarta(List<Carta> mano, EstadoBaza estadoBaza, GamePhase fase, Palo triunfo);
    }

    // =========================================================================
    // Implementacion Greedy
    // =========================================================================

    /// <summary>
    /// IA con estrategia greedy:
    ///   - Fase Robo: juega una carta aleatoria.
    ///   - Fase Arrastre:
    ///       1. Obtiene las jugadas validas segun ValidadorReglas.
    ///       2. Si puede ganar la baza → juega la carta valida MAS ALTA que gane
    ///          (maximiza puntos capturados).
    ///       3. Si no puede ganar → descarta la carta de MENOR VALOR
    ///          (minimiza puntos perdidos).
    /// </summary>
    public class IAGreedy : IEstrategiaIA
    {
        private readonly Random rng;

        public IAGreedy(Random random = null)
        {
            rng = random ?? new Random();
        }

        public Carta ElegirCarta(List<Carta> mano, EstadoBaza estadoBaza, GamePhase fase, Palo triunfo)
        {
            if (mano == null || mano.Count == 0)
                return null;

            // ─────────────────────────────────────────────────────────────────
            // FASE ROBO: cualquier carta es valida → aleatoria
            // ─────────────────────────────────────────────────────────────────
            if (fase == GamePhase.FaseRobo)
                return mano[rng.Next(mano.Count)];

            // ─────────────────────────────────────────────────────────────────
            // FASE ARRASTRE: aplicar estrategia greedy con reglas
            // ─────────────────────────────────────────────────────────────────

            // Si somos el primero en salir (baza vacia), salimos con la carta de mayor valor
            if (estadoBaza.CartaSalida == null)
                return ElegirMayorValor(mano);

            // Filtrar jugadas validas
            List<Carta> jugadasValidas = FiltrarJugadasValidas(
                mano, estadoBaza.CartaSalida, triunfo,
                estadoBaza.MejorCartaActual, estadoBaza);

            if (jugadasValidas.Count == 0)
                jugadasValidas = mano; // fallback de seguridad

            // De las validas, ver si alguna puede ganar la baza
            List<Carta> puedenGanar = new List<Carta>();
            foreach (Carta c in jugadasValidas)
            {
                if (ValidadorReglas.GanaA(c, estadoBaza.MejorCartaActual,
                    estadoBaza.CartaSalida.Palo, triunfo))
                {
                    puedenGanar.Add(c);
                }
            }

            if (puedenGanar.Count > 0)
            {
                // Puede ganar → jugar la carta valida ganadora de mayor rango
                return ElegirMayorRango(puedenGanar, estadoBaza.CartaSalida.Palo, triunfo);
            }
            else
            {
                // No puede ganar → descartar la de menor valor
                return ElegirMenorValor(jugadasValidas);
            }
        }

        // =====================================================================
        // Utilidades privadas
        // =====================================================================

        /// <summary>
        /// Filtra la mano devolviendo solo las cartas jugables segun las reglas de arrastre.
        /// </summary>
        private List<Carta> FiltrarJugadasValidas(
            List<Carta> mano,
            Carta cartaSalida,
            Palo triunfo,
            Carta mejorCartaEnMesa,
            EstadoBaza estado)
        {
            List<Carta> validas = new List<Carta>();
            foreach (Carta c in mano)
            {
                if (ValidadorReglas.EsJugadaValidaArrastre(
                        c, cartaSalida, triunfo, mano, mejorCartaEnMesa))
                {
                    validas.Add(c);
                }
            }
            return validas;
        }

        /// <summary>Devuelve la carta de mayor rango priorizando triunfos y palo de salida.</summary>
        private Carta ElegirMayorRango(List<Carta> cartas, Palo paloSalida, Palo triunfo)
        {
            Carta mejor = null;
            foreach (Carta c in cartas)
            {
                if (mejor == null)
                {
                    mejor = c;
                    continue;
                }
                if (ValidadorReglas.GanaA(c, mejor, paloSalida, triunfo))
                    mejor = c;
            }
            return mejor;
        }

        /// <summary>Devuelve la carta con mas puntos de la mano (para salir primero).</summary>
        private Carta ElegirMayorValor(List<Carta> cartas)
        {
            Carta mejor = cartas[0];
            for (int i = 1; i < cartas.Count; i++)
            {
                if (cartas[i].ObtenerPuntos() > mejor.ObtenerPuntos() ||
                   (cartas[i].ObtenerPuntos() == mejor.ObtenerPuntos() &&
                    cartas[i].Rango > mejor.Rango))
                {
                    mejor = cartas[i];
                }
            }
            return mejor;
        }

        /// <summary>Devuelve la carta de menor valor (para descartar).</summary>
        private Carta ElegirMenorValor(List<Carta> cartas)
        {
            Carta peor = cartas[0];
            for (int i = 1; i < cartas.Count; i++)
            {
                if (cartas[i].ObtenerPuntos() < peor.ObtenerPuntos() ||
                   (cartas[i].ObtenerPuntos() == peor.ObtenerPuntos() &&
                    cartas[i].Rango < peor.Rango))
                {
                    peor = cartas[i];
                }
            }
            return peor;
        }
    }
}
