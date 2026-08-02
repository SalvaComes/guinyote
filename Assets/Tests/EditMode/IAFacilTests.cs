using System;
using System.Collections.Generic;
using GuinoteApp.Core;
using NUnit.Framework;

namespace GuinoteApp.Core.Tests
{
    public class IAFacilTests
    {
        private static readonly IReadOnlyList<Carta> SinHistorial = new List<Carta>();

        [Test]
        public void ElegirCarta_ManoVacia_DevuelveNull()
        {
            var ia = new IAFacil(new Random(1));

            Carta resultado = ia.ElegirCarta(1, new List<Carta>(), new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos, SinHistorial);

            Assert.IsNull(resultado);
        }

        [Test]
        public void FaseRobo_DevuelveSiempreUnaCartaDeLaMano()
        {
            var ia = new IAFacil(new Random(7));
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
        public void FaseArrastre_AlAbrirBaza_JuegaCualquierCartaDeLaMano()
        {
            var ia = new IAFacil(new Random(7));
            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.Dos),
                new Carta(Palo.Copas, Rango.As),
            };

            for (int i = 0; i < 20; i++)
            {
                Carta elegida = ia.ElegirCarta(1, mano, new EstadoBaza(), GamePhase.FaseArrastre, Palo.Bastos, SinHistorial);
                CollectionAssert.Contains(mano, elegida);
            }
        }

        [Test]
        public void FaseArrastre_RespetaLaObligacionDeAsistir()
        {
            var ia = new IAFacil(new Random(3));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.Sota), Palo.Bastos);

            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.Rey),  // unica del palo de salida
                new Carta(Palo.Copas, Rango.As),  // no es del palo de salida
                new Carta(Palo.Espadas, Rango.As),
            };

            for (int i = 0; i < 20; i++)
            {
                Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseArrastre, Palo.Bastos, SinHistorial);
                Assert.AreEqual(Palo.Oros, elegida.Palo, "Debe asistir al palo de salida aunque no optimice la jugada");
            }
        }

        [Test]
        public void FaseArrastre_RespetaLaObligacionDeFallar()
        {
            var ia = new IAFacil(new Random(3));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.Sota), Palo.Bastos);

            // Sin cartas de Oros pero con triunfo (Bastos): debe fallar
            var mano = new List<Carta>
            {
                new Carta(Palo.Bastos, Rango.Dos),
                new Carta(Palo.Bastos, Rango.Cuatro),
                new Carta(Palo.Copas, Rango.As),
            };

            for (int i = 0; i < 20; i++)
            {
                Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseArrastre, Palo.Bastos, SinHistorial);
                Assert.AreEqual(Palo.Bastos, elegida.Palo, "Debe fallar con triunfo si no puede asistir");
            }
        }
    }
}
