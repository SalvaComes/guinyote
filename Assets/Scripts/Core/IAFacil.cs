using System;
using System.Collections.Generic;

namespace GuinoteApp.Core
{
    /// <summary>
    /// IA de dificultad Facil: respeta las reglas obligatorias (asistir/montar/fallar en
    /// Fase de Arrastre) pero no aplica ninguna estrategia — elige siempre al azar entre
    /// las jugadas validas, sin intentar ganar la baza ni proteger sus cartas de valor.
    /// </summary>
    public class IAFacil : IEstrategiaIA
    {
        private readonly Random rng;

        public IAFacil(Random random = null)
        {
            rng = random ?? new Random();
        }

        public Carta ElegirCarta(
            int jugadorIdx,
            List<Carta> mano,
            EstadoBaza estadoBaza,
            GamePhase fase,
            Palo triunfo,
            IReadOnlyList<Carta> cartasJugadasEnLaMano)
        {
            if (mano == null || mano.Count == 0)
                return null;

            // Fase Robo, o abriendo baza en Arrastre: cualquier carta es valida → al azar
            if (fase == GamePhase.FaseRobo || estadoBaza.CartaSalida == null)
                return mano[rng.Next(mano.Count)];

            List<Carta> jugadasValidas = FiltrarJugadasValidas(
                mano, estadoBaza.CartaSalida, triunfo, estadoBaza.MejorCartaActual);

            if (jugadasValidas.Count == 0)
                jugadasValidas = mano; // fallback de seguridad

            return jugadasValidas[rng.Next(jugadasValidas.Count)];
        }

        private static List<Carta> FiltrarJugadasValidas(
            List<Carta> mano, Carta cartaSalida, Palo triunfo, Carta mejorCartaEnMesa)
        {
            var validas = new List<Carta>();
            foreach (Carta c in mano)
            {
                if (ValidadorReglas.EsJugadaValidaArrastre(c, cartaSalida, triunfo, mano, mejorCartaEnMesa))
                    validas.Add(c);
            }
            return validas;
        }
    }
}
