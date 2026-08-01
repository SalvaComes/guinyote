using System.Collections.Generic;

namespace GuinoteApp.Core
{
    /// <summary>
    /// Representa el estado de la baza en curso.
    /// Registra las cartas jugadas, quien va ganando y calcula el ganador final.
    /// Clase pura sin dependencias de Unity.
    /// </summary>
    public class EstadoBaza
    {
        // =====================================================================
        // Datos de la baza
        // =====================================================================

        /// <summary>Par (indice de jugador, carta jugada) para cada turno de la baza.</summary>
        public List<(int jugadorIdx, Carta carta)> CartasJugadas { get; private set; }

        /// <summary>Primera carta jugada. Define el palo de salida de la baza.</summary>
        public Carta CartaSalida { get; private set; }

        /// <summary>La carta que va ganando en este momento.</summary>
        public Carta MejorCartaActual { get; private set; }

        /// <summary>Indice del jugador (0-3) que va ganando actualmente.</summary>
        public int IndicadorGanadorActual { get; private set; }

        /// <summary>Puntos acumulados en las cartas ya jugadas en esta baza.</summary>
        public int PuntosEnMesa { get; private set; }

        /// <summary>Numero de cartas jugadas en esta baza (0 a 4).</summary>
        public int CartasJugadasCount => CartasJugadas.Count;

        /// <summary>True cuando los 4 jugadores han jugado su carta.</summary>
        public bool EstaCompleta => CartasJugadas.Count == 4;

        // =====================================================================
        // Constructor
        // =====================================================================

        public EstadoBaza()
        {
            CartasJugadas = new List<(int, Carta)>(4);
            CartaSalida = null;
            MejorCartaActual = null;
            IndicadorGanadorActual = -1;
            PuntosEnMesa = 0;
        }

        // =====================================================================
        // API Publica
        // =====================================================================

        /// <summary>
        /// Registra la jugada de un jugador y actualiza quien va ganando.
        /// </summary>
        /// <param name="jugadorIdx">Indice del jugador (0-3).</param>
        /// <param name="carta">Carta que juega.</param>
        /// <param name="paloTriunfo">Palo de triunfo de la partida.</param>
        public void RegistrarJugada(int jugadorIdx, Carta carta, Palo paloTriunfo)
        {
            CartasJugadas.Add((jugadorIdx, carta));
            PuntosEnMesa += carta.ObtenerPuntos();

            if (CartaSalida == null)
            {
                // Primera carta de la baza
                CartaSalida = carta;
                MejorCartaActual = carta;
                IndicadorGanadorActual = jugadorIdx;
            }
            else
            {
                // Comprueba si esta carta supera a la actual ganadora
                if (ValidadorReglas.GanaA(carta, MejorCartaActual, CartaSalida.Palo, paloTriunfo))
                {
                    MejorCartaActual = carta;
                    IndicadorGanadorActual = jugadorIdx;
                }
            }
        }

        /// <summary>
        /// Devuelve el indice del jugador ganador de la baza.
        /// Solo llamar cuando EstaCompleta == true.
        /// </summary>
        public int ObtenerGanador() => IndicadorGanadorActual;

        /// <summary>
        /// Devuelve todas las cartas jugadas (sin informacion de jugador).
        /// Util para calcular puntos.
        /// </summary>
        public List<Carta> ObtenerCartas()
        {
            var lista = new List<Carta>(4);
            foreach (var (_, carta) in CartasJugadas)
                lista.Add(carta);
            return lista;
        }

        public override string ToString()
        {
            string resultado = $"Baza [{CartasJugadasCount}/4] — Ganando: J{IndicadorGanadorActual + 1} ({MejorCartaActual}) | Puntos en mesa: {PuntosEnMesa}";
            return resultado;
        }
    }
}
