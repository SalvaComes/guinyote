using System.Collections.Generic;

namespace GuinoteApp.Core
{
    /// <summary>
    /// Utilidades comunes a las estrategias de IA: jugadas legales, memoria de cartas,
    /// comparaciones de coste y fuerza. Clase estatica pura, sin estado.
    /// </summary>
    public static class HeuristicasIA
    {
        public const int PalosCount = 4;
        public const int RangosCount = 10;
        public const int TotalCartas = PalosCount * RangosCount;

        /// <summary>Indice 0..39 de una carta (palo * 10 + rango).</summary>
        public static int Clave(Carta c) => (int)c.Palo * RangosCount + (int)c.Rango;

        // =====================================================================
        // Jugadas legales
        // =====================================================================

        /// <summary>
        /// Cartas que la IA puede jugar ahora mismo. Ver <see cref="JugadasPermitidas"/>.
        /// Nunca devuelve una lista vacia si la mano tiene cartas.
        /// </summary>
        public static List<Carta> JugadasValidas(ContextoIA ctx)
        {
            return JugadasPermitidas(ctx.Mano, ctx.Baza, ctx.Fase, ctx.Triunfo);
        }

        /// <summary>
        /// Cartas que un jugador puede jugar: en Fase Robo cualquiera; en Fase Arrastre, las que
        /// cumplen asistir / montar / fallar. Nunca devuelve una lista vacia si la mano tiene cartas.
        /// </summary>
        public static List<Carta> JugadasPermitidas(
            List<Carta> mano, EstadoBaza baza, GamePhase fase, Palo triunfo)
        {
            var legales = new List<Carta>(mano.Count);
            bool apertura = baza == null || baza.CartaSalida == null;

            if (!apertura && fase == GamePhase.FaseArrastre)
            {
                foreach (Carta c in mano)
                    if (ValidadorReglas.EsJugadaValidaArrastre(c, baza.CartaSalida, triunfo, mano, baza.MejorCartaActual))
                        legales.Add(c);
            }
            if (legales.Count == 0)
                legales.AddRange(mano);

            return legales;
        }

        /// <summary>Subconjunto de <paramref name="candidatas"/> que supera la mejor carta en mesa.</summary>
        public static List<Carta> QuePuedenGanar(ContextoIA ctx, List<Carta> candidatas)
        {
            var ganadoras = new List<Carta>();
            if (ctx.EsApertura) return ganadoras;
            foreach (Carta c in candidatas)
                if (ValidadorReglas.GanaA(c, ctx.Baza.MejorCartaActual, ctx.Baza.CartaSalida.Palo, ctx.Triunfo))
                    ganadoras.Add(c);
            return ganadoras;
        }

        // =====================================================================
        // Memoria de cartas
        // =====================================================================

        /// <summary>
        /// Marca las cartas que la IA ya ha visto: su mano, las bazas resueltas, la baza en
        /// curso y la pinta. El resto estan en manos ajenas o en el mazo.
        /// </summary>
        public static bool[] CartasVistas(ContextoIA ctx)
        {
            var vistas = new bool[TotalCartas];
            foreach (Carta c in ctx.Mano) vistas[Clave(c)] = true;
            foreach (Carta c in ctx.CartasJugadas) vistas[Clave(c)] = true;
            if (ctx.Baza != null)
                foreach (var (_, c) in ctx.Baza.CartasJugadas) vistas[Clave(c)] = true;
            if (ctx.PintaVisible != null) vistas[Clave(ctx.PintaVisible)] = true;
            return vistas;
        }

        /// <summary>Cuantas cartas de un palo no ha visto aun la IA.</summary>
        public static int DesconocidasDelPalo(bool[] vistas, Palo palo)
        {
            int n = 0;
            for (int r = 0; r < RangosCount; r++)
                if (!vistas[(int)palo * RangosCount + r]) n++;
            return n;
        }

        /// <summary>True si ninguna carta aun no vista del mismo palo supera a <paramref name="c"/>.</summary>
        public static bool EsMaestra(Carta c, bool[] vistas)
        {
            for (int r = (int)c.Rango + 1; r < RangosCount; r++)
                if (!vistas[(int)c.Palo * RangosCount + r]) return false;
            return true;
        }

        /// <summary>
        /// Estimacion de la probabilidad de que <paramref name="carta"/> (que va ganando, o que
        /// la IA piensa jugar) sea superada por alguno de los <paramref name="restantes"/>
        /// jugadores que aun tienen que jugar en la baza.
        /// Considera dos amenazas: una carta mayor del mismo palo y, si la carta no es triunfo,
        /// un triunfo (en Arrastre, solo de un jugador que no pueda asistir al palo de salida).
        /// Es una aproximacion que reparte las cartas desconocidas al azar; no usa informacion
        /// oculta.
        /// </summary>
        public static double ProbSuperada(
            ContextoIA ctx, bool[] vistas, Carta carta, Palo paloSalida, int restantes)
        {
            if (restantes <= 0) return 0.0;

            int desconocidas = 0;
            for (int i = 0; i < TotalCartas; i++)
                if (!vistas[i]) desconocidas++;
            if (desconocidas == 0) return 0.0;

            int h = System.Math.Max(1, ctx.Mano.Count);
            double qMano = System.Math.Min(1.0, restantes * (double)h / desconocidas);

            int superiores = 0;
            for (int r = (int)carta.Rango + 1; r < RangosCount; r++)
                if (!vistas[(int)carta.Palo * RangosCount + r]) superiores++;
            double pSuperior = 1.0 - System.Math.Pow(1.0 - qMano, superiores);

            double pTriunfo = 0.0;
            if (carta.Palo != ctx.Triunfo)
            {
                int triunfosFuera = DesconocidasDelPalo(vistas, ctx.Triunfo);
                if (triunfosFuera > 0)
                {
                    double pTieneTriunfo = 1.0 - System.Math.Pow(
                        1.0 - System.Math.Min(1.0, (double)triunfosFuera / desconocidas), h);

                    double porJugador = pTieneTriunfo;
                    if (ctx.Fase == GamePhase.FaseArrastre)
                    {
                        // Solo corta con triunfo quien no tiene del palo de salida
                        int delPalo = DesconocidasDelPalo(vistas, paloSalida);
                        double pSinPalo = System.Math.Pow(
                            1.0 - System.Math.Min(1.0, (double)delPalo / desconocidas), h);
                        porJugador = pTieneTriunfo * pSinPalo;
                    }
                    pTriunfo = 1.0 - System.Math.Pow(1.0 - porJugador, restantes);
                }
            }

            return 1.0 - (1.0 - pSuperior) * (1.0 - pTriunfo);
        }

        // =====================================================================
        // Comparadores
        // =====================================================================

        /// <summary>
        /// Coste de desprenderse de una carta: los triunfos y las cartas de puntos pesan mas.
        /// Sirve para elegir la "mas barata" cuando hay que descartar.
        /// </summary>
        public static int Coste(Carta c, Palo triunfo)
        {
            int coste = c.ObtenerPuntos() * 10 + (int)c.Rango;
            if (c.Palo == triunfo) coste += 150;
            return coste;
        }

        /// <summary>
        /// Coste de desprenderse de una carta teniendo en cuenta su valor oculto: formar parte
        /// de un cante todavia posible (Rey + Sota del mismo palo) o ser la carta con la que se
        /// puede cambiar una pinta valiosa.
        /// </summary>
        public static int CosteEstrategico(ContextoIA ctx, Carta c)
        {
            int coste = Coste(c, ctx.Triunfo);

            if ((c.Rango == Rango.Rey || c.Rango == Rango.Sota) &&
                !Contiene(ctx.PalosCantados, c.Palo) &&
                ReglasEspeciales.TieneReyYSota(ctx.Mano, c.Palo))
            {
                coste += c.Palo == ctx.Triunfo ? 400 : 200;
            }

            if (ctx.Fase == GamePhase.FaseRobo && ctx.PintaVisible != null &&
                ctx.PintaVisible.Rango > Rango.Siete &&
                c.Palo == ctx.Triunfo && c.Rango == Rango.Siete)
            {
                coste += 500;
            }

            return coste;
        }

        public static Carta MenorCoste(ContextoIA ctx, List<Carta> cartas)
        {
            Carta mejor = cartas[0];
            int mejorCoste = CosteEstrategico(ctx, mejor);
            for (int i = 1; i < cartas.Count; i++)
            {
                int c = CosteEstrategico(ctx, cartas[i]);
                if (c < mejorCoste) { mejor = cartas[i]; mejorCoste = c; }
            }
            return mejor;
        }

        private static bool Contiene(IReadOnlyCollection<Palo> palos, Palo palo)
        {
            if (palos == null) return false;
            foreach (Palo p in palos)
                if (p == palo) return true;
            return false;
        }

        public static Carta MenorCoste(List<Carta> cartas, Palo triunfo)
        {
            Carta mejor = cartas[0];
            int mejorCoste = Coste(mejor, triunfo);
            for (int i = 1; i < cartas.Count; i++)
            {
                int c = Coste(cartas[i], triunfo);
                if (c < mejorCoste) { mejor = cartas[i]; mejorCoste = c; }
            }
            return mejor;
        }

        /// <summary>Carta con mas puntos; a igualdad, la que no es triunfo y de mayor rango.</summary>
        public static Carta MayorValor(List<Carta> cartas, Palo triunfo)
        {
            Carta mejor = cartas[0];
            for (int i = 1; i < cartas.Count; i++)
            {
                Carta c = cartas[i];
                int pc = c.ObtenerPuntos(), pm = mejor.ObtenerPuntos();
                if (pc > pm)
                {
                    mejor = c;
                }
                else if (pc == pm)
                {
                    bool cTriunfo = c.Palo == triunfo, mTriunfo = mejor.Palo == triunfo;
                    if (cTriunfo != mTriunfo)
                    {
                        if (!cTriunfo) mejor = c;
                    }
                    else if (c.Rango > mejor.Rango)
                    {
                        mejor = c;
                    }
                }
            }
            return mejor;
        }

        /// <summary>
        /// De las cartas ganadoras, la mas debil: prefiere las que no son triunfo y, dentro de
        /// cada grupo, la de menor rango. Conserva las cartas fuertes para bazas futuras.
        /// </summary>
        public static Carta GanadoraMasDebil(List<Carta> ganadoras, Palo triunfo)
        {
            Carta mejor = ganadoras[0];
            for (int i = 1; i < ganadoras.Count; i++)
            {
                Carta c = ganadoras[i];
                bool cTriunfo = c.Palo == triunfo, mTriunfo = mejor.Palo == triunfo;
                if (cTriunfo != mTriunfo)
                {
                    if (!cTriunfo) mejor = c;
                }
                else if (c.Rango < mejor.Rango)
                {
                    mejor = c;
                }
            }
            return mejor;
        }

        /// <summary>De las cartas ganadoras, la mas fuerte segun las reglas de la baza.</summary>
        public static Carta GanadoraMasFuerte(List<Carta> ganadoras, Palo paloSalida, Palo triunfo)
        {
            Carta mejor = ganadoras[0];
            for (int i = 1; i < ganadoras.Count; i++)
                if (ValidadorReglas.GanaA(ganadoras[i], mejor, paloSalida, triunfo))
                    mejor = ganadoras[i];
            return mejor;
        }

        public static List<Carta> SinTriunfos(List<Carta> cartas, Palo triunfo)
        {
            var res = new List<Carta>(cartas.Count);
            foreach (Carta c in cartas)
                if (c.Palo != triunfo) res.Add(c);
            return res;
        }

        public static int ContarTriunfos(List<Carta> cartas, Palo triunfo)
        {
            int n = 0;
            foreach (Carta c in cartas)
                if (c.Palo == triunfo) n++;
            return n;
        }

        // =====================================================================
        // Cantes y cambios (decisiones compartidas)
        // =====================================================================

        /// <summary>Elige el cante de mas puntos: el triunfo vale 40, el resto 20.</summary>
        public static Palo? MejorCante(IReadOnlyList<Palo> palosDisponibles, Palo triunfo)
        {
            if (palosDisponibles == null || palosDisponibles.Count == 0) return null;
            foreach (Palo p in palosDisponibles)
                if (p == triunfo) return p;
            return palosDisponibles[0];
        }
    }
}
