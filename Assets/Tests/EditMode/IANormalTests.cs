using System;
using System.Collections.Generic;
using GuinoteApp.Core;
using NUnit.Framework;

namespace GuinoteApp.Core.Tests
{
    public class IANormalTests
    {
        private static readonly IReadOnlyList<Carta> SinHistorial = IATestHelper.SinHistorial;

        [Test]
        public void ElegirCarta_ManoVacia_DevuelveNull()
        {
            var ia = new IANormal(new Random(1));

            Carta resultado = ia.ElegirCarta(1, new List<Carta>(), new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos, SinHistorial);

            Assert.IsNull(resultado);
        }

        [Test]
        public void ElegirCarta_ManoNull_DevuelveNull()
        {
            var ia = new IANormal(new Random(1));

            Carta resultado = ia.ElegirCarta(1, null, new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos, SinHistorial);

            Assert.IsNull(resultado);
        }

        // =====================================================================
        // Apertura
        // =====================================================================

        [Test]
        public void FaseRobo_AlAbrir_SaleConLaCartaMasBarata()
        {
            var ia = new IANormal(new Random(1));
            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.As),
                new Carta(Palo.Copas, Rango.Dos),
                new Carta(Palo.Bastos, Rango.Cuatro), // triunfo: no se malgasta
            };

            Carta elegida = ia.ElegirCarta(1, mano, new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Palo.Copas, elegida.Palo);
            Assert.AreEqual(Rango.Dos, elegida.Rango);
        }

        [Test]
        public void FaseArrastre_AlAbrirBaza_TanteaConLaCartaMasBarata()
        {
            var ia = new IANormal(new Random(1));
            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.Dos),
                new Carta(Palo.Copas, Rango.As),
                new Carta(Palo.Espadas, Rango.Rey),
            };

            Carta elegida = ia.ElegirCarta(1, mano, new EstadoBaza(), GamePhase.FaseArrastre, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Rango.Dos, elegida.Rango, "No expone el As al abrir");
        }

        // =====================================================================
        // Responder en Fase Robo
        // =====================================================================

        [Test]
        public void FaseRobo_RivalGanaConPuntos_GanaConLaCartaMasDebilQueBasta()
        {
            var ia = new IANormal(new Random(1));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(2, new Carta(Palo.Oros, Rango.Rey), Palo.Bastos); // 4 pts, rival

            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.As),     // gana, pero es la mas fuerte
                new Carta(Palo.Oros, Rango.Tres),   // gana por poco
                new Carta(Palo.Copas, Rango.Dos),
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseRobo, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Rango.Tres, elegida.Rango);
        }

        [Test]
        public void FaseRobo_RivalGanaSinPuntos_NoGastaCartaParaGanar()
        {
            var ia = new IANormal(new Random(1));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(2, new Carta(Palo.Oros, Rango.Dos), Palo.Bastos); // 0 pts

            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.As),
                new Carta(Palo.Copas, Rango.Cuatro),
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseRobo, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Rango.Cuatro, elegida.Rango, "No malgasta el As por una baza vacia");
        }

        [Test]
        public void FaseRobo_RivalGanaConPocosPuntos_NoGastaTriunfo()
        {
            var ia = new IANormal(new Random(1));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(2, new Carta(Palo.Oros, Rango.Rey), Palo.Bastos); // 4 pts

            var mano = new List<Carta>
            {
                new Carta(Palo.Bastos, Rango.Dos), // unico modo de ganar: cortar con triunfo
                new Carta(Palo.Copas, Rango.Cuatro),
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseRobo, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Palo.Copas, elegida.Palo, "Cortar con triunfo exige una baza de al menos 10 puntos");
        }

        [Test]
        public void FaseRobo_RivalGanaConAs_CortaConTriunfoBarato()
        {
            var ia = new IANormal(new Random(1));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(2, new Carta(Palo.Oros, Rango.As), Palo.Bastos); // 11 pts

            var mano = new List<Carta>
            {
                new Carta(Palo.Bastos, Rango.Rey),
                new Carta(Palo.Bastos, Rango.Dos),
                new Carta(Palo.Copas, Rango.Cuatro),
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseRobo, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Palo.Bastos, elegida.Palo);
            Assert.AreEqual(Rango.Dos, elegida.Rango, "Corta con el triunfo mas bajo");
        }

        // =====================================================================
        // Compañero
        // =====================================================================

        [Test]
        public void CompaneroGanandoYQuedanRivalesPorJugar_TiraBarato()
        {
            var ia = new IANormal(new Random(1));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.Dos), Palo.Bastos);
            baza.RegistrarJugada(3, new Carta(Palo.Oros, Rango.As), Palo.Bastos); // compañero de idx 1

            var mano = new List<Carta>
            {
                new Carta(Palo.Copas, Rango.As),
                new Carta(Palo.Espadas, Rango.Dos),
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseRobo, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Rango.Dos, elegida.Rango);
        }

        [Test]
        public void CompaneroGanandoYEsElUltimo_CargaPuntos()
        {
            var ia = new IANormal(new Random(1));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.Dos), Palo.Bastos);
            baza.RegistrarJugada(3, new Carta(Palo.Oros, Rango.As), Palo.Bastos);
            baza.RegistrarJugada(2, new Carta(Palo.Copas, Rango.Cuatro), Palo.Bastos);

            var mano = new List<Carta>
            {
                new Carta(Palo.Espadas, Rango.As),
                new Carta(Palo.Espadas, Rango.Dos),
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseRobo, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Rango.As, elegida.Rango);
        }

        // =====================================================================
        // Fase Arrastre
        // =====================================================================

        [Test]
        public void FaseArrastre_SiPuedeGanar_JuegaLaCartaValidaMasDebilQueGane()
        {
            var ia = new IANormal(new Random(1));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.Sota), Palo.Bastos);

            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.Rey),   // supera a la Sota
                new Carta(Palo.Oros, Rango.As),    // tambien, pero es mas fuerte
                new Carta(Palo.Copas, Rango.Tres),
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseArrastre, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Palo.Oros, elegida.Palo);
            Assert.AreEqual(Rango.Rey, elegida.Rango, "Gana lo justo y conserva el As");
        }

        [Test]
        public void FaseArrastre_SiNoPuedeGanar_DescartaLaCartaDeMenorValor()
        {
            var ia = new IANormal(new Random(1));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.Rey), Palo.Bastos);

            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.Sota), // 3 puntos, no supera al Rey
                new Carta(Palo.Oros, Rango.Dos),  // 0 puntos, no supera al Rey
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseArrastre, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Rango.Dos, elegida.Rango, "Debe descartar la carta de menor valor si no puede ganar");
        }

        [Test]
        public void FaseArrastre_SinPaloNiTriunfo_JuegaLaCartaDeMenorValorDeCualquierPalo()
        {
            var ia = new IANormal(new Random(1));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.Rey), Palo.Bastos);

            var mano = new List<Carta>
            {
                new Carta(Palo.Copas, Rango.As),
                new Carta(Palo.Espadas, Rango.Dos),
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseArrastre, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Palo.Espadas, elegida.Palo);
            Assert.AreEqual(Rango.Dos, elegida.Rango);
        }

        [Test]
        public void FaseArrastre_RespetaSiempreLasReglas()
        {
            var ia = new IANormal(new Random(9));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.Caballo), Palo.Bastos);

            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.Rey),
                new Carta(Palo.Oros, Rango.Cuatro),   // no monta: ilegal teniendo el Rey
                new Carta(Palo.Copas, Rango.Dos),
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseArrastre, Palo.Bastos, SinHistorial);

            Assert.IsTrue(ValidadorReglas.EsJugadaValidaArrastre(elegida, baza.CartaSalida, Palo.Bastos, mano, baza.MejorCartaActual));
        }

        // =====================================================================
        // Cantes y cambios
        // =====================================================================

        [Test]
        public void DecidirCante_PrefiereElPaloDeTriunfo()
        {
            var ia = new IANormal(new Random(1));
            var ctx = IATestHelper.Contexto(1, new List<Carta>(), new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos);

            Palo? res = ia.DecidirCante(ctx, new List<Palo> { Palo.Oros, Palo.Bastos });

            Assert.AreEqual(Palo.Bastos, res);
        }

        [Test]
        public void DecidirCante_SinOpciones_DevuelveNull()
        {
            var ia = new IANormal(new Random(1));
            var ctx = IATestHelper.Contexto(1, new List<Carta>(), new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos);

            Assert.IsNull(ia.DecidirCante(ctx, new List<Palo>()));
        }

        [Test]
        public void DecidirCambio_SiempreCambia()
        {
            var ia = new IANormal(new Random(1));
            var ctx = IATestHelper.Contexto(1, new List<Carta>(), new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos);

            Assert.IsTrue(ia.DecidirCambio(ctx, new Carta(Palo.Bastos, Rango.Siete)));
        }
    }
}
