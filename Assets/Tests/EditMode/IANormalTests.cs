using System;
using System.Collections.Generic;
using GuinoteApp.Core;
using NUnit.Framework;

namespace GuinoteApp.Core.Tests
{
    public class IANormalTests
    {
        private static readonly IReadOnlyList<Carta> SinHistorial = new List<Carta>();

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

        [Test]
        public void FaseRobo_DevuelveSiempreUnaCartaDeLaMano()
        {
            var ia = new IANormal(new Random(42));
            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.As),
                new Carta(Palo.Copas, Rango.Dos),
                new Carta(Palo.Espadas, Rango.Rey),
            };

            for (int i = 0; i < 20; i++)
            {
                Carta elegida = ia.ElegirCarta(1, mano, new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos, SinHistorial);
                CollectionAssert.Contains(mano, elegida);
            }
        }

        [Test]
        public void FaseArrastre_AlAbrirBaza_JuegaLaCartaDeMayorValor()
        {
            var ia = new IANormal(new Random(1));
            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.Dos),
                new Carta(Palo.Copas, Rango.As),  // 11 puntos, la mas valiosa
                new Carta(Palo.Espadas, Rango.Rey), // 4 puntos
            };

            Carta elegida = ia.ElegirCarta(1, mano, new EstadoBaza(), GamePhase.FaseArrastre, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Rango.As, elegida.Rango);
        }

        [Test]
        public void FaseArrastre_SiPuedeGanar_JuegaLaCartaValidaMasAlta()
        {
            var ia = new IANormal(new Random(1));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.Sota), Palo.Bastos);

            // Debe asistir a Oros; tiene dos cartas de Oros que superan la Sota
            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.Rey),   // supera, rango 7
                new Carta(Palo.Oros, Rango.As),    // supera, rango 9 (la mas alta)
                new Carta(Palo.Copas, Rango.Tres),
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseArrastre, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Palo.Oros, elegida.Palo);
            Assert.AreEqual(Rango.As, elegida.Rango);
        }

        [Test]
        public void FaseArrastre_SiNoPuedeGanar_DescartaLaCartaDeMenorValor()
        {
            var ia = new IANormal(new Random(1));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.Rey), Palo.Bastos);

            // Ninguna carta de Oros en mano supera al Rey → no hay montar posible,
            // cualquier carta de Oros es jugada valida, pero ninguna gana la baza
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

            // Sin cartas de Oros ni de Bastos (triunfo): puede descartar libremente
            var mano = new List<Carta>
            {
                new Carta(Palo.Copas, Rango.As),   // 11 puntos
                new Carta(Palo.Espadas, Rango.Dos), // 0 puntos, la mas barata
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseArrastre, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Palo.Espadas, elegida.Palo);
            Assert.AreEqual(Rango.Dos, elegida.Rango);
        }
    }
}
