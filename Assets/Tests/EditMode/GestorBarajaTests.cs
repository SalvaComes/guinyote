using System.Collections.Generic;
using GuinoteApp.Core;
using NUnit.Framework;

namespace GuinoteApp.Core.Tests
{
    public class GestorBarajaTests
    {
        [Test]
        public void Constructor_GeneraLos40NaipesUnicos()
        {
            var baraja = new GestorBaraja();
            var vistas = new HashSet<(Palo, Rango)>();

            Assert.AreEqual(40, baraja.CartasRestantes());

            for (int i = 0; i < 40; i++)
            {
                Carta c = baraja.RobarCarta();
                Assert.IsTrue(vistas.Add((c.Palo, c.Rango)), $"Carta duplicada: {c}");
            }

            Assert.AreEqual(40, vistas.Count);
        }

        [Test]
        public void RobarCarta_DecrementaCartasRestantes()
        {
            var baraja = new GestorBaraja();

            baraja.RobarCarta();

            Assert.AreEqual(39, baraja.CartasRestantes());
        }

        [Test]
        public void RobarCarta_ConMazoVacio_DevuelveNull()
        {
            var baraja = new GestorBaraja();
            for (int i = 0; i < 40; i++)
                baraja.RobarCarta();

            Assert.AreEqual(0, baraja.CartasRestantes());
            Assert.IsNull(baraja.RobarCarta());
        }

        [Test]
        public void Barajar_ConservaLas40CartasSinDuplicarNiPerder()
        {
            var baraja = new GestorBaraja();

            baraja.Barajar();

            var vistas = new HashSet<(Palo, Rango)>();
            for (int i = 0; i < 40; i++)
            {
                Carta c = baraja.RobarCarta();
                Assert.IsTrue(vistas.Add((c.Palo, c.Rango)), $"Carta duplicada tras barajar: {c}");
            }

            Assert.AreEqual(40, vistas.Count);
            Assert.AreEqual(0, baraja.CartasRestantes());
        }
    }
}
