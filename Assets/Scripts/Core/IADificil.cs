using System;
using System.Collections.Generic;

namespace GuinoteApp.Core
{
    /// <summary>
    /// IA de dificultad Dificil: el jugador de peña. Todo lo del nivel Normal, mas:
    ///   - Memoria: sabe que cartas han salido y estima la probabilidad de que la carta que va
    ///     ganando sea superada por quien aun tiene que jugar.
    ///   - Compañero: solo carga puntos sobre la baza de su compañero cuando es poco probable
    ///     que se la quiten.
    ///   - Reserva: no malgasta las cartas que forman un cante posible (Rey + Sota) ni el 7 de
    ///     triunfo cuando puede cambiar una pinta valiosa.
    ///   - Final de partida: en la Fase de Arrastre, con pocas cartas por repartir, simula
    ///     varias manos posibles (<see cref="BuscadorMonteCarlo"/>) y juega la mejor carta.
    /// </summary>
    public class IADificil : IEstrategiaIA
    {
        /// <summary>Por debajo de esta probabilidad de ser superada, una carta se considera segura.</summary>
        private const double UmbralSeguro = 0.18;

        /// <summary>Por debajo de esta probabilidad compensa cargar puntos sobre la baza del compañero.</summary>
        private const double UmbralCargar = 0.40;

        private const int PuntosMinimosParaGanarEnRobo = 4;
        private const int PuntosMinimosParaTriunfarEnRobo = 10;

        /// <summary>Presupuesto de jugadas simuladas por decision en el Arrastre.</summary>
        private const int PresupuestoArrastre = 2500;
        private const int MuestrasMinimas = 12;
        private const int MuestrasMaximas = 40;
        private const int TiempoMaximoMs = 100;

        private readonly BuscadorMonteCarlo buscador;

        /// <param name="random">Generador aleatorio (inyectable para tests deterministas).</param>
        /// <param name="busquedaEnArrastre">Si es false, decide solo con reglas (lo usan las simulaciones
        /// para no encadenar una busqueda dentro de otra).</param>
        public IADificil(Random random = null, bool busquedaEnArrastre = true)
        {
            if (busquedaEnArrastre)
            {
                Random rng = random ?? new Random();
                buscador = new BuscadorMonteCarlo(rng, new IADificil(rng, busquedaEnArrastre: false));
            }
        }

        public Carta ElegirCarta(ContextoIA ctx)
        {
            if (ctx?.Mano == null || ctx.Mano.Count == 0)
                return null;

            List<Carta> validas = HeuristicasIA.JugadasValidas(ctx);
            if (validas.Count == 1)
                return validas[0];

            if (buscador != null && ctx.Fase == GamePhase.FaseArrastre)
            {
                int muestras = BuscadorMonteCarlo.CalcularMuestras(
                    ctx.Mano.Count, validas.Count, PresupuestoArrastre, MuestrasMinimas, MuestrasMaximas);
                Carta mejor = buscador.Buscar(ctx, validas, muestras, TiempoMaximoMs);
                if (mejor != null)
                    return mejor;
            }

            bool[] vistas = HeuristicasIA.CartasVistas(ctx);

            if (ctx.EsApertura)
            {
                return ctx.Fase == GamePhase.FaseRobo
                    ? AbrirEnRobo(ctx, validas)
                    : AbrirEnArrastre(ctx, validas, vistas);
            }

            return ctx.Fase == GamePhase.FaseRobo
                ? ResponderEnRobo(ctx, validas, vistas)
                : ResponderEnArrastre(ctx, validas, vistas);
        }

        public Palo? DecidirCante(ContextoIA ctx, IReadOnlyList<Palo> palosDisponibles)
        {
            return HeuristicasIA.MejorCante(palosDisponibles, ctx.Triunfo);
        }

        public bool DecidirCambio(ContextoIA ctx, Carta cartaPropia) => true;

        // =====================================================================
        // Fase de Robo
        // =====================================================================

        private static Carta AbrirEnRobo(ContextoIA ctx, List<Carta> validas)
        {
            // En Robo cualquiera puede cortar con triunfo: no hay carta segura. Sale barato y
            // reserva ases, treses, triunfos y parejas de cante para despues.
            return HeuristicasIA.MenorCoste(ctx, validas);
        }

        private static Carta ResponderEnRobo(ContextoIA ctx, List<Carta> validas, bool[] vistas)
        {
            Palo triunfo = ctx.Triunfo;

            if (ctx.CompaneroGanando)
                return ResponderAlCompanero(ctx, validas, vistas);

            List<Carta> ganadoras = HeuristicasIA.QuePuedenGanar(ctx, validas);
            if (ganadoras.Count > 0)
            {
                Carta debil = HeuristicasIA.GanadoraMasDebil(ganadoras, triunfo);
                int umbral = debil.Palo == triunfo ? PuntosMinimosParaTriunfarEnRobo : PuntosMinimosParaGanarEnRobo;
                if (ctx.Baza.PuntosEnMesa >= umbral)
                    return debil;
            }

            return HeuristicasIA.MenorCoste(ctx, validas);
        }

        // =====================================================================
        // Fase de Arrastre (reglas de apoyo: las usa la IA cuando no puede buscar)
        // =====================================================================

        private static Carta AbrirEnArrastre(ContextoIA ctx, List<Carta> validas, bool[] vistas)
        {
            Palo triunfo = ctx.Triunfo;
            int triunfosFuera = HeuristicasIA.DesconocidasDelPalo(vistas, triunfo);

            // Sin triunfos rivales, las cartas mas altas que quedan de cada palo ganan seguro
            if (triunfosFuera == 0)
            {
                var seguras = new List<Carta>();
                foreach (Carta c in validas)
                    if (HeuristicasIA.ProbSuperada(ctx, vistas, c, c.Palo, 3) <= UmbralSeguro)
                        seguras.Add(c);

                List<Carta> segurasSinTriunfo = HeuristicasIA.SinTriunfos(seguras, triunfo);
                if (segurasSinTriunfo.Count > 0)
                    return HeuristicasIA.MayorValor(segurasSinTriunfo, triunfo);
                if (seguras.Count > 0)
                    return HeuristicasIA.GanadoraMasDebil(seguras, triunfo);
            }

            // Si no, tantea con la carta mas barata sin gastar triunfos
            return HeuristicasIA.MenorCoste(ctx, validas);
        }

        private static Carta ResponderEnArrastre(ContextoIA ctx, List<Carta> validas, bool[] vistas)
        {
            if (ctx.CompaneroGanando)
                return ResponderAlCompanero(ctx, validas, vistas);

            List<Carta> ganadoras = HeuristicasIA.QuePuedenGanar(ctx, validas);
            return ganadoras.Count > 0
                ? HeuristicasIA.GanadoraMasDebil(ganadoras, ctx.Triunfo)
                : HeuristicasIA.MenorCoste(ctx, validas);
        }

        // =====================================================================
        // Utilidades privadas
        // =====================================================================

        /// <summary>El compañero va ganando: suma puntos si es poco probable que se la quiten.</summary>
        private static Carta ResponderAlCompanero(ContextoIA ctx, List<Carta> validas, bool[] vistas)
        {
            int restantes = 3 - ctx.Baza.CartasJugadasCount;
            double pSuperada = HeuristicasIA.ProbSuperada(
                ctx, vistas, ctx.Baza.MejorCartaActual, ctx.Baza.CartaSalida.Palo, restantes);

            if (pSuperada >= UmbralCargar)
                return HeuristicasIA.MenorCoste(ctx, validas);

            List<Carta> sinTriunfo = HeuristicasIA.SinTriunfos(validas, ctx.Triunfo);
            return sinTriunfo.Count > 0
                ? HeuristicasIA.MayorValor(sinTriunfo, ctx.Triunfo)
                : HeuristicasIA.MenorCoste(ctx, validas);
        }
    }
}
