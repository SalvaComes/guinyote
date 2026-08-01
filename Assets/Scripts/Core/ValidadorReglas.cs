using System.Collections.Generic;

namespace GuinoteApp.Core
{
    /// <summary>
    /// Clase estática que valida las jugadas según las reglas del Guiñote.
    /// En Fase de Arrastre: obligatorio asistir, montar o fallar.
    /// En Fase de Robo: cualquier carta es válida (no se valida aquí).
    /// </summary>
    public static class ValidadorReglas
    {
        /// <summary>
        /// Verifica si una jugada es válida durante la Fase de Arrastre.
        /// 
        /// Reglas de arrastre:
        /// 1. ASISTIR: Si el jugador tiene cartas del palo de salida, debe jugar una de ellas.
        /// 2. MONTAR: Si asiste, debe superar la mejor carta en mesa (del mismo palo), si es posible.
        /// 3. FALLAR: Si no puede asistir, debe jugar triunfo si tiene.
        ///    - Si la mejor carta en mesa es un triunfo, debe superarla con un triunfo mayor si puede.
        /// 4. Si no puede asistir ni fallar, puede jugar cualquier carta.
        /// </summary>
        /// <param name="cartaJugada">La carta que el jugador intenta jugar.</param>
        /// <param name="cartaSalida">La primera carta jugada en la baza (define el palo de salida).</param>
        /// <param name="triunfo">El palo de triunfo de la partida.</param>
        /// <param name="manoJugador">La mano actual del jugador (incluye la carta que intenta jugar).</param>
        /// <param name="mejorCartaEnMesa">La carta que actualmente va ganando la baza.</param>
        /// <returns>True si la jugada es válida según las reglas de arrastre.</returns>
        public static bool EsJugadaValidaArrastre(
            Carta cartaJugada,
            Carta cartaSalida,
            Palo triunfo,
            List<Carta> manoJugador,
            Carta mejorCartaEnMesa)
        {
            Palo paloSalida = cartaSalida.Palo;

            bool tieneCartasDelPalo = TieneCartasDelPalo(manoJugador, paloSalida);
            bool tieneTriunfos = (paloSalida != triunfo) && TieneCartasDelPalo(manoJugador, triunfo);

            // --- CASO 1: Puede asistir (tiene cartas del palo de salida) ---
            if (tieneCartasDelPalo)
            {
                // Debe jugar una carta del palo de salida
                if (cartaJugada.Palo != paloSalida)
                    return false;

                // Debe montar si la mejor carta en mesa es del mismo palo y puede superarla
                if (mejorCartaEnMesa.Palo == paloSalida)
                {
                    bool puedeMontar = ExisteCartaSuperior(manoJugador, paloSalida, mejorCartaEnMesa.Rango);

                    if (puedeMontar && cartaJugada.Rango <= mejorCartaEnMesa.Rango)
                        return false;
                }
                // Si la mejor carta es un triunfo (y el palo de salida no es triunfo),
                // no se puede superar con una carta del palo de salida → cualquier carta del palo vale.

                return true;
            }

            // --- CASO 2: No puede asistir, pero tiene triunfos → debe fallar ---
            if (tieneTriunfos)
            {
                // Debe jugar un triunfo
                if (cartaJugada.Palo != triunfo)
                    return false;

                // Si la mejor carta en mesa ya es un triunfo, debe superarla si puede
                if (mejorCartaEnMesa.Palo == triunfo)
                {
                    bool puedeSuperarTriunfo = ExisteCartaSuperior(manoJugador, triunfo, mejorCartaEnMesa.Rango);

                    if (puedeSuperarTriunfo && cartaJugada.Rango <= mejorCartaEnMesa.Rango)
                        return false;
                }

                return true;
            }

            // --- CASO 3: No puede asistir ni fallar → cualquier carta vale ---
            return true;
        }

        /// <summary>
        /// Comprueba si el jugador tiene alguna carta de un palo determinado.
        /// </summary>
        private static bool TieneCartasDelPalo(List<Carta> mano, Palo palo)
        {
            for (int i = 0; i < mano.Count; i++)
            {
                if (mano[i].Palo == palo)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Comprueba si existe en la mano alguna carta del palo indicado con rango
        /// estrictamente superior al rango dado.
        /// </summary>
        private static bool ExisteCartaSuperior(List<Carta> mano, Palo palo, Rango rangoASuperar)
        {
            for (int i = 0; i < mano.Count; i++)
            {
                if (mano[i].Palo == palo && mano[i].Rango > rangoASuperar)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Determina si cartaA vence a cartaB en una baza, considerando el palo de salida y el triunfo.
        /// Útil para determinar quién gana una baza.
        /// </summary>
        /// <param name="cartaA">La carta retadora.</param>
        /// <param name="cartaB">La carta que actualmente va ganando.</param>
        /// <param name="paloSalida">El palo de la primera carta jugada en la baza.</param>
        /// <param name="triunfo">El palo de triunfo de la partida.</param>
        /// <returns>True si cartaA vence a cartaB.</returns>
        public static bool GanaA(Carta cartaA, Carta cartaB, Palo paloSalida, Palo triunfo)
        {
            bool aEsTriunfo = cartaA.Palo == triunfo;
            bool bEsTriunfo = cartaB.Palo == triunfo;

            // Triunfo siempre gana a no-triunfo
            if (aEsTriunfo && !bEsTriunfo)
                return true;
            if (!aEsTriunfo && bEsTriunfo)
                return false;

            // Si ambas son del mismo palo, gana la de mayor rango
            if (cartaA.Palo == cartaB.Palo)
                return cartaA.Rango > cartaB.Rango;

            // Si A es del palo de salida y B no (y ninguna es triunfo), gana A
            if (cartaA.Palo == paloSalida && cartaB.Palo != paloSalida)
                return true;

            // En cualquier otro caso (B es del palo de salida o ambas de palos distintos sin triunfo), B gana
            return false;
        }
    }
}
