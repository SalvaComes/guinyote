using GuinoteApp.Core;
using NUnit.Framework;

namespace GuinoteApp.Core.Tests
{
    public class EstadoBazaTests
    {
        [Test]
        public void EstadoInicial_EstaVacio()
        {
            var baza = new EstadoBaza();

            Assert.AreEqual(0, baza.CartasJugadasCount);
            Assert.IsNull(baza.CartaSalida);
            Assert.IsNull(baza.MejorCartaActual);
            Assert.AreEqual(-1, baza.IndicadorGanadorActual);
            Assert.AreEqual(0, baza.PuntosEnMesa);
            Assert.IsFalse(baza.EstaCompleta);
        }

        [Test]
        public void PrimeraJugada_DefineSalidaYGanadorProvisional()
        {
            var baza = new EstadoBaza();
            var carta = new Carta(Palo.Oros, Rango.Sota);

            baza.RegistrarJugada(0, carta, Palo.Bastos);

            Assert.AreEqual(carta, baza.CartaSalida);
            Assert.AreEqual(carta, baza.MejorCartaActual);
            Assert.AreEqual(0, baza.IndicadorGanadorActual);
        }

        [Test]
        public void SegundaJugada_MismoParloMayorRango_CambiaGanador()
        {
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.Sota), Palo.Bastos);

            baza.RegistrarJugada(1, new Carta(Palo.Oros, Rango.Rey), Palo.Bastos);

            Assert.AreEqual(1, baza.IndicadorGanadorActual);
            Assert.AreEqual(Rango.Rey, baza.MejorCartaActual.Rango);
        }

        [Test]
        public void SegundaJugada_MismoPaloMenorRango_MantieneGanador()
        {
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.Rey), Palo.Bastos);

            baza.RegistrarJugada(1, new Carta(Palo.Oros, Rango.Sota), Palo.Bastos);

            Assert.AreEqual(0, baza.IndicadorGanadorActual);
        }

        [Test]
        public void Triunfo_GanaAPaloDeSalidaAunqueSeaDeMenorRango()
        {
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.As), Palo.Bastos);

            baza.RegistrarJugada(1, new Carta(Palo.Bastos, Rango.Dos), Palo.Bastos);

            Assert.AreEqual(1, baza.IndicadorGanadorActual);
            Assert.AreEqual(Palo.Bastos, baza.MejorCartaActual.Palo);
        }

        [Test]
        public void PuntosEnMesa_AcumulaLosPuntosDeCadaCarta()
        {
            var baza = new EstadoBaza();

            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.As), Palo.Bastos);   // 11
            baza.RegistrarJugada(1, new Carta(Palo.Copas, Rango.Tres), Palo.Bastos); // 10
            baza.RegistrarJugada(2, new Carta(Palo.Espadas, Rango.Dos), Palo.Bastos); // 0

            Assert.AreEqual(21, baza.PuntosEnMesa);
        }

        [Test]
        public void EstaCompleta_SoloTrasCuatroJugadas()
        {
            var baza = new EstadoBaza();
            var palos = new[] { Palo.Oros, Palo.Copas, Palo.Espadas, Palo.Bastos };

            for (int i = 0; i < 3; i++)
            {
                baza.RegistrarJugada(i, new Carta(palos[i], Rango.Dos), Palo.Bastos);
                Assert.IsFalse(baza.EstaCompleta);
            }

            baza.RegistrarJugada(3, new Carta(palos[3], Rango.Dos), Palo.Bastos);

            Assert.IsTrue(baza.EstaCompleta);
            Assert.AreEqual(4, baza.CartasJugadasCount);
        }

        [Test]
        public void ObtenerCartas_DevuelveTodasLasCartasJugadasEnOrden()
        {
            var baza = new EstadoBaza();
            var c1 = new Carta(Palo.Oros, Rango.Dos);
            var c2 = new Carta(Palo.Oros, Rango.Rey);

            baza.RegistrarJugada(0, c1, Palo.Bastos);
            baza.RegistrarJugada(1, c2, Palo.Bastos);

            var cartas = baza.ObtenerCartas();

            Assert.AreEqual(2, cartas.Count);
            Assert.AreEqual(c1, cartas[0]);
            Assert.AreEqual(c2, cartas[1]);
        }
    }
}
