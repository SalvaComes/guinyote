using System;
using System.Collections.Generic;
using GuinoteApp.Core;
using NUnit.Framework;

namespace GuinoteApp.Core.Tests
{
    public class IADificilTests
    {
        private static readonly IReadOnlyList<Carta> SinHistorial = new List<Carta>();

        [Test]
        public void ElegirCarta_ManoVacia_DevuelveNull()
        {
            var ia = new IADificil(new Random(1));

            Carta resultado = ia.ElegirCarta(1, new List<Carta>(), new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos, SinHistorial);

            Assert.IsNull(resultado);
        }

        // =====================================================================
        // Fase de Robo
        // =====================================================================

        [Test]
        public void FaseRobo_AlAbrir_SaleConLaCartaMasBarata()
        {
            var ia = new IADificil(new Random(1));
            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.As),   // 11 pts
                new Carta(Palo.Copas, Rango.Dos), // 0 pts, la mas barata
            };

            Carta elegida = ia.ElegirCarta(1, mano, new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Rango.Dos, elegida.Rango, "Debe reservar el As para el Arrastre, no abrir con el");
        }

        [Test]
        public void FaseRobo_CompaneroGanandoYQuedanJugadoresPorResponder_TiraBarato()
        {
            // Jugador 1 (idx=1), companero = idx 3. La baza va ganando el jugador 3.
            var ia = new IADificil(new Random(1));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.Dos), Palo.Bastos);
            baza.RegistrarJugada(3, new Carta(Palo.Oros, Rango.As), Palo.Bastos); // companero gana, faltan J1 y J2 aun no...

            var mano = new List<Carta>
            {
                new Carta(Palo.Copas, Rango.As),  // 11 pts
                new Carta(Palo.Espadas, Rango.Dos), // 0 pts
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseRobo, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Rango.Dos, elegida.Rango, "Con companero ganando pero sin ser el ultimo en jugar, no debe arriesgar sus cartas de valor");
        }

        [Test]
        public void FaseRobo_CompaneroGanandoYEsElUltimoEnJugar_CargaPuntos()
        {
            var ia = new IADificil(new Random(1));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.Dos), Palo.Bastos);
            baza.RegistrarJugada(3, new Carta(Palo.Oros, Rango.As), Palo.Bastos); // companero (idx 3) gana
            baza.RegistrarJugada(2, new Carta(Palo.Copas, Rango.Cuatro), Palo.Bastos);
            // Ahora juega J1 (idx 1), el ultimo de la baza (CartasJugadasCount == 3)

            var mano = new List<Carta>
            {
                new Carta(Palo.Espadas, Rango.As),   // 11 pts, la mas valiosa
                new Carta(Palo.Espadas, Rango.Dos),  // 0 pts
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseRobo, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Rango.As, elegida.Rango, "Siendo el ultimo en jugar y con la baza asegurada, debe sumar puntos");
        }

        [Test]
        public void FaseRobo_RivalGanandoYCompensaGanar_GanaConLaCartaMinima()
        {
            var ia = new IADificil(new Random(1));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(2, new Carta(Palo.Oros, Rango.As), Palo.Bastos); // rival (idx 2, no companero de idx 1) gana con 11 pts en mesa

            var mano = new List<Carta>
            {
                new Carta(Palo.Bastos, Rango.Rey),  // triunfo alto, gana
                new Carta(Palo.Bastos, Rango.Dos),  // triunfo bajo, tambien gana (cualquier triunfo gana a un no-triunfo)
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseRobo, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Rango.Dos, elegida.Rango, "Debe ganar con el triunfo mas barato posible, no malgastar el Rey");
        }

        // =====================================================================
        // Fase de Arrastre — apertura con conteo de cartas
        // =====================================================================

        [Test]
        public void FaseArrastre_AlAbrirConTriunfosRivalesPendientes_TanteaConCartaBarataNoTriunfo()
        {
            var ia = new IADificil(new Random(1));
            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.As),    // 11 pts, no triunfo
                new Carta(Palo.Copas, Rango.Dos),  // 0 pts, no triunfo
                new Carta(Palo.Bastos, Rango.Rey), // triunfo
            };
            // Historial vacio: quedan 10 - 1 (el propio Rey de triunfo) = 9 triunfos sin ver → hay riesgo
            Carta elegida = ia.ElegirCarta(1, mano, new EstadoBaza(), GamePhase.FaseArrastre, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Palo.Copas, elegida.Palo);
            Assert.AreEqual(Rango.Dos, elegida.Rango);
        }

        [Test]
        public void FaseArrastre_AlAbrirSinTriunfosRivalesPendientes_SaleConSuMejorCarta()
        {
            var ia = new IADificil(new Random(1));

            // Historial: ya han salido 9 triunfos de Bastos en manos anteriores
            var historial = new List<Carta>();
            foreach (Rango r in new[] { Rango.Dos, Rango.Cuatro, Rango.Cinco, Rango.Seis, Rango.Siete,
                                          Rango.Caballo, Rango.Sota, Rango.Rey, Rango.Tres })
            {
                historial.Add(new Carta(Palo.Bastos, r));
            }

            var mano = new List<Carta>
            {
                new Carta(Palo.Bastos, Rango.As), // el ultimo triunfo posible, lo tiene el
                new Carta(Palo.Oros, Rango.Dos),
            };

            Carta elegida = ia.ElegirCarta(1, mano, new EstadoBaza(), GamePhase.FaseArrastre, Palo.Bastos, historial);

            Assert.AreEqual(Rango.As, elegida.Rango, "Sin triunfos rivales pendientes, puede abrir con su carta mas valiosa sin riesgo");
        }

        // =====================================================================
        // Fase de Arrastre — ganar barato / conciencia de companero
        // =====================================================================

        [Test]
        public void FaseArrastre_SiPuedeGanar_UsaLaCartaMinimaSuficiente()
        {
            var ia = new IADificil(new Random(1));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.Sota), Palo.Bastos);

            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.Rey), // supera a la Sota (rango 7 > 6)
                new Carta(Palo.Oros, Rango.As),  // tambien supera (rango 9), pero es mas valiosa
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseArrastre, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Rango.Rey, elegida.Rango, "Debe ganar con la carta minima suficiente (el Rey), reservando el As");
        }

        [Test]
        public void FaseArrastre_NoPuedeGanarYCompaneroGana_AportaLaCartaDeMayorValor()
        {
            var ia = new IADificil(new Random(1));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.Rey), Palo.Bastos);
            baza.RegistrarJugada(3, new Carta(Palo.Oros, Rango.As), Palo.Bastos); // companero (idx 3 de J1) va ganando

            // J1 (idx 1) no tiene Oros ni Bastos: jugada libre
            var mano = new List<Carta>
            {
                new Carta(Palo.Copas, Rango.As),   // 11 pts
                new Carta(Palo.Espadas, Rango.Dos), // 0 pts
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseArrastre, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Rango.As, elegida.Rango, "Con el companero ya ganando, debe cargar puntos con seguridad");
        }

        [Test]
        public void FaseArrastre_NoPuedeGanarYRivalGana_DescartaLaCartaDeMenorValor()
        {
            var ia = new IADificil(new Random(1));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(2, new Carta(Palo.Oros, Rango.Rey), Palo.Bastos); // rival (idx 2) va ganando

            var mano = new List<Carta>
            {
                new Carta(Palo.Copas, Rango.As),   // 11 pts
                new Carta(Palo.Espadas, Rango.Dos), // 0 pts
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseArrastre, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Rango.Dos, elegida.Rango, "Con el rival ganando, debe minimizar la perdida");
        }
    }
}
