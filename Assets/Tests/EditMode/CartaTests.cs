using GuinoteApp.Core;
using NUnit.Framework;

namespace GuinoteApp.Core.Tests
{
    public class CartaTests
    {
        [TestCase(Rango.As, 11)]
        [TestCase(Rango.Tres, 10)]
        [TestCase(Rango.Rey, 4)]
        [TestCase(Rango.Sota, 3)]
        [TestCase(Rango.Caballo, 2)]
        [TestCase(Rango.Dos, 0)]
        [TestCase(Rango.Cuatro, 0)]
        [TestCase(Rango.Cinco, 0)]
        [TestCase(Rango.Seis, 0)]
        [TestCase(Rango.Siete, 0)]
        public void ObtenerPuntos_DevuelveElValorCorrecto(Rango rango, int puntosEsperados)
        {
            var carta = new Carta(Palo.Oros, rango);

            Assert.AreEqual(puntosEsperados, carta.ObtenerPuntos());
        }

        [Test]
        public void Constructor_AsignaPaloYRango()
        {
            var carta = new Carta(Palo.Espadas, Rango.Rey);

            Assert.AreEqual(Palo.Espadas, carta.Palo);
            Assert.AreEqual(Rango.Rey, carta.Rango);
        }
    }
}
