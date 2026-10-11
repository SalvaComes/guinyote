using System.Collections.Generic;
using GuinoteApp.Core;
using NUnit.Framework;

namespace GuinoteApp.Core.Tests
{
    public class ReglasEspecialesTests
    {
        [Test]
        public void PuntosCante_TriunfoVale40_RestoVale20()
        {
            Assert.AreEqual(40, ReglasEspeciales.PuntosCante(Palo.Bastos, Palo.Bastos));
            Assert.AreEqual(20, ReglasEspeciales.PuntosCante(Palo.Oros, Palo.Bastos));
        }

        [Test]
        public void PalosCantables_ReyYSota_DevuelveElPalo()
        {
            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.Rey), new Carta(Palo.Oros, Rango.Sota),
                new Carta(Palo.Copas, Rango.Rey), new Carta(Palo.Espadas, Rango.Sota),
            };

            List<Palo> res = ReglasEspeciales.PalosCantables(mano, new List<Palo>());

            CollectionAssert.AreEqual(new[] { Palo.Oros }, res);
        }

        [Test]
        public void PalosCantables_ExcluyeLosYaCantados()
        {
            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.Rey), new Carta(Palo.Oros, Rango.Sota),
                new Carta(Palo.Copas, Rango.Rey), new Carta(Palo.Copas, Rango.Sota),
            };

            List<Palo> res = ReglasEspeciales.PalosCantables(mano, new List<Palo> { Palo.Oros });

            CollectionAssert.AreEqual(new[] { Palo.Copas }, res);
        }

        [Test]
        public void PalosCantables_SoloReyOSoloSota_NoCanta()
        {
            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.Rey), new Carta(Palo.Copas, Rango.Sota),
            };

            Assert.IsEmpty(ReglasEspeciales.PalosCantables(mano, null));
        }

        [TestCase(Rango.As, Rango.Siete)]
        [TestCase(Rango.Tres, Rango.Siete)]
        [TestCase(Rango.Caballo, Rango.Siete)]
        [TestCase(Rango.Siete, Rango.Dos)]
        [TestCase(Rango.Cuatro, Rango.Dos)]
        public void RangoDeCambio_SegunLaPinta(Rango pinta, Rango esperado)
        {
            Assert.AreEqual(esperado, ReglasEspeciales.RangoDeCambio(new Carta(Palo.Bastos, pinta)));
        }

        [Test]
        public void RangoDeCambio_PintaEsElDos_NoHayMejora()
        {
            Assert.IsNull(ReglasEspeciales.RangoDeCambio(new Carta(Palo.Bastos, Rango.Dos)));
        }

        [Test]
        public void RangoDeCambio_SinPinta_DevuelveNull()
        {
            Assert.IsNull(ReglasEspeciales.RangoDeCambio(null));
        }

        [Test]
        public void CartaDeCambio_TieneElSieteDeTriunfo_LaDevuelve()
        {
            var siete = new Carta(Palo.Bastos, Rango.Siete);
            var mano = new List<Carta> { new Carta(Palo.Oros, Rango.Siete), siete };

            Carta res = ReglasEspeciales.CartaDeCambio(mano, new Carta(Palo.Bastos, Rango.As), Palo.Bastos);

            Assert.AreSame(siete, res, "Debe ser el 7 del palo de triunfo, no el de otro palo");
        }

        [Test]
        public void CartaDeCambio_SieteDeOtroPalo_DevuelveNull()
        {
            var mano = new List<Carta> { new Carta(Palo.Oros, Rango.Siete) };

            Assert.IsNull(ReglasEspeciales.CartaDeCambio(mano, new Carta(Palo.Bastos, Rango.As), Palo.Bastos));
        }

        [Test]
        public void CartaDeCambio_PintaBaja_ExigeElDos()
        {
            var dos = new Carta(Palo.Bastos, Rango.Dos);
            var mano = new List<Carta> { new Carta(Palo.Bastos, Rango.Siete), dos };

            Carta res = ReglasEspeciales.CartaDeCambio(mano, new Carta(Palo.Bastos, Rango.Cinco), Palo.Bastos);

            Assert.AreSame(dos, res);
        }
    }
}
