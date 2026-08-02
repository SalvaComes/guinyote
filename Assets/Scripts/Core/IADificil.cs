using System;
using System.Collections.Generic;

namespace GuinoteApp.Core
{
    /// <summary>
    /// IA de dificultad Dificil. Amplia la estrategia greedy de <see cref="IANormal"/> con:
    ///
    ///   - Conciencia de compañero: si el compañero ((jugadorIdx + 2) % 4) ya va ganando la
    ///     baza, evita "quemar" cartas de valor salvo que sea seguro (ultimo en jugar en Fase
    ///     Robo, o la jugada es obligatoria en Fase Arrastre).
    ///   - Conteo de cartas: al abrir baza en Fase Arrastre, cuenta cuantos triunfos han
    ///     salido ya (en <paramref name="cartasJugadasEnLaMano"/> y en su propia mano) para
    ///     decidir si es seguro abrir con su carta mas valiosa o si conviene tantear primero.
    ///   - Ganar barato: cuando puede ganar la baza, elige la carta MINIMA suficiente para
    ///     ganar en vez de la mas alta, conservando sus cartas fuertes para bazas futuras.
    /// </summary>
    public class IADificil : IEstrategiaIA
    {
        private static readonly int CartasPorPalo = Enum.GetValues(typeof(Rango)).Length;

        private readonly Random rng;

        public IADificil(Random random = null)
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

            int companeroIdx = (jugadorIdx + 2) % 4;
            bool companeroGanando = estadoBaza.IndicadorGanadorActual == companeroIdx;

            if (fase == GamePhase.FaseRobo)
                return ElegirEnFaseRobo(mano, estadoBaza, triunfo, companeroGanando);

            return ElegirEnFaseArrastre(mano, estadoBaza, triunfo, cartasJugadasEnLaMano, companeroGanando);
        }

        // =====================================================================
        // Fase de Robo — sin obligacion de asistir/montar/fallar
        // =====================================================================

        private Carta ElegirEnFaseRobo(
            List<Carta> mano, EstadoBaza estadoBaza, Palo triunfo, bool companeroGanando)
        {
            // Al abrir: sale barato, reservando cartas de valor y triunfos para el Arrastre
            if (estadoBaza.CartaSalida == null)
                return ElegirMenorValor(mano);

            if (companeroGanando)
            {
                // Si nadie mas puede responder ya, aprovecha para sumar puntos sin riesgo
                bool esUltimoEnJugar = estadoBaza.CartasJugadasCount == 3;
                return esUltimoEnJugar ? ElegirMayorValor(mano) : ElegirMenorValor(mano);
            }

            // El rival va ganando: si compensa (hay puntos en juego), gana barato; si no, descarta
            List<Carta> puedenGanar = new List<Carta>();
            foreach (Carta c in mano)
            {
                if (ValidadorReglas.GanaA(c, estadoBaza.MejorCartaActual, estadoBaza.CartaSalida.Palo, triunfo))
                    puedenGanar.Add(c);
            }

            if (puedenGanar.Count > 0 && estadoBaza.PuntosEnMesa > 0)
                return ElegirMinimaGanadora(puedenGanar, estadoBaza.CartaSalida.Palo, triunfo);

            return ElegirMenorValor(mano);
        }

        // =====================================================================
        // Fase de Arrastre — asistir/montar/fallar obligatorios
        // =====================================================================

        private Carta ElegirEnFaseArrastre(
            List<Carta> mano,
            EstadoBaza estadoBaza,
            Palo triunfo,
            IReadOnlyList<Carta> cartasJugadasEnLaMano,
            bool companeroGanando)
        {
            if (estadoBaza.CartaSalida == null)
                return ElegirAperturaArrastre(mano, triunfo, cartasJugadasEnLaMano);

            List<Carta> jugadasValidas = FiltrarJugadasValidas(
                mano, estadoBaza.CartaSalida, triunfo, estadoBaza.MejorCartaActual);

            if (jugadasValidas.Count == 0)
                jugadasValidas = mano; // fallback de seguridad

            List<Carta> puedenGanar = new List<Carta>();
            foreach (Carta c in jugadasValidas)
            {
                if (ValidadorReglas.GanaA(c, estadoBaza.MejorCartaActual, estadoBaza.CartaSalida.Palo, triunfo))
                    puedenGanar.Add(c);
            }

            if (puedenGanar.Count > 0)
                return ElegirMinimaGanadora(puedenGanar, estadoBaza.CartaSalida.Palo, triunfo);

            // No puede ganar: si el compañero ya gana la baza, la jugada es libre entre las
            // validas → aporta la de mayor valor (puntos seguros). Si no, descarta la mas barata.
            return companeroGanando ? ElegirMayorValor(jugadasValidas) : ElegirMenorValor(jugadasValidas);
        }

        /// <summary>
        /// Elige la carta de apertura en Fase Arrastre contando triunfos ya vistos: si no
        /// puede quedar ningun triunfo en manos rivales, abre con su carta mas valiosa (nadie
        /// puede fallarle); si aun quedan triunfos fuera, tantea con una carta barata que no
        /// sea triunfo para no exponer sus Ases/Treses.
        /// </summary>
        private Carta ElegirAperturaArrastre(List<Carta> mano, Palo triunfo, IReadOnlyList<Carta> historial)
        {
            int triunfosVistos = 0;
            foreach (Carta c in historial)
                if (c.Palo == triunfo) triunfosVistos++;
            foreach (Carta c in mano)
                if (c.Palo == triunfo) triunfosVistos++;

            bool quedanTriunfosFuera = triunfosVistos < CartasPorPalo;

            if (!quedanTriunfosFuera)
                return ElegirMayorValor(mano);

            List<Carta> noTriunfo = new List<Carta>();
            foreach (Carta c in mano)
                if (c.Palo != triunfo) noTriunfo.Add(c);

            return noTriunfo.Count > 0 ? ElegirMenorValor(noTriunfo) : ElegirMenorValor(mano);
        }

        // =====================================================================
        // Utilidades privadas
        // =====================================================================

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

        /// <summary>Entre cartas que ya ganan la baza, devuelve la MINIMA suficiente (no malgasta cartas fuertes).</summary>
        private static Carta ElegirMinimaGanadora(List<Carta> candidatas, Palo paloSalida, Palo triunfo)
        {
            Carta minima = candidatas[0];
            foreach (Carta c in candidatas)
            {
                if (ValidadorReglas.GanaA(minima, c, paloSalida, triunfo))
                    minima = c;
            }
            return minima;
        }

        private static Carta ElegirMayorValor(List<Carta> cartas)
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

        private static Carta ElegirMenorValor(List<Carta> cartas)
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
