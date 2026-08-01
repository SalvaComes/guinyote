using System.Collections.Generic;
using GuinoteApp.Core;
using NUnit.Framework;

namespace GuinoteApp.Core.Tests
{
    public class ValidadorReglasTests
    {
        // =====================================================================
        // GanaA
        // =====================================================================

        [Test]
        public void GanaA_TriunfoVenceANoTriunfoAunqueSeaDeMenorRango()
        {
            var triunfo = new Carta(Palo.Bastos, Rango.Dos);
            var noTriunfo = new Carta(Palo.Oros, Rango.As);

            Assert.IsTrue(ValidadorReglas.GanaA(triunfo, noTriunfo, Palo.Oros, Palo.Bastos));
            Assert.IsFalse(ValidadorReglas.GanaA(noTriunfo, triunfo, Palo.Oros, Palo.Bastos));
        }

        [Test]
        public void GanaA_MismoPaloGanaElDeMayorRango()
        {
            var alta = new Carta(Palo.Oros, Rango.Rey);
            var baja = new Carta(Palo.Oros, Rango.Sota);

            Assert.IsTrue(ValidadorReglas.GanaA(alta, baja, Palo.Oros, Palo.Bastos));
            Assert.IsFalse(ValidadorReglas.GanaA(baja, alta, Palo.Oros, Palo.Bastos));
        }

        [Test]
        public void GanaA_PaloDeSalidaVenceAPaloDistintoSinTriunfo()
        {
            var deSalida = new Carta(Palo.Oros, Rango.Dos);
            var otroPalo = new Carta(Palo.Copas, Rango.As);

            Assert.IsTrue(ValidadorReglas.GanaA(deSalida, otroPalo, Palo.Oros, Palo.Bastos));
        }

        [Test]
        public void GanaA_PaloDistintoAlDeSalidaYSinTriunfo_Pierde()
        {
            var deSalida = new Carta(Palo.Oros, Rango.Dos);
            var otroPalo = new Carta(Palo.Copas, Rango.As);

            Assert.IsFalse(ValidadorReglas.GanaA(otroPalo, deSalida, Palo.Oros, Palo.Bastos));
        }

        // =====================================================================
        // EsJugadaValidaArrastre — ASISTIR
        // =====================================================================

        [Test]
        public void Arrastre_ConCartasDelPaloDeSalida_DebeAsistir()
        {
            var salida = new Carta(Palo.Oros, Rango.Dos);
            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.Sota),
                new Carta(Palo.Copas, Rango.As),
            };
            var jugarFueraDePalo = mano[1];

            bool valida = ValidadorReglas.EsJugadaValidaArrastre(
                jugarFueraDePalo, salida, Palo.Bastos, mano, salida);

            Assert.IsFalse(valida);
        }

        [Test]
        public void Arrastre_Montar_ObligaASuperarSiPuede()
        {
            var salida = new Carta(Palo.Oros, Rango.Sota);
            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.Rey),  // supera a la Sota
                new Carta(Palo.Oros, Rango.Dos),  // no supera
            };

            bool jugarBaja = ValidadorReglas.EsJugadaValidaArrastre(
                mano[1], salida, Palo.Bastos, mano, salida);
            bool jugarAlta = ValidadorReglas.EsJugadaValidaArrastre(
                mano[0], salida, Palo.Bastos, mano, salida);

            Assert.IsFalse(jugarBaja, "Debe montar con el Rey si puede, no vale jugar el Dos");
            Assert.IsTrue(jugarAlta);
        }

        [Test]
        public void Arrastre_SinPosibilidadDeMontar_CualquierCartaDelPaloVale()
        {
            var salida = new Carta(Palo.Oros, Rango.Rey);
            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.Dos), // no supera al Rey, pero es la unica opcion del palo
            };

            bool valida = ValidadorReglas.EsJugadaValidaArrastre(
                mano[0], salida, Palo.Bastos, mano, salida);

            Assert.IsTrue(valida);
        }

        [Test]
        public void Arrastre_MejorCartaEsTriunfo_NoObligaAMontarConElPaloDeSalida()
        {
            // El rival ya ha fallado con triunfo; el jugador que asiste no puede superarlo
            // jugando el palo de salida, así que cualquier carta de ese palo es válida.
            var salida = new Carta(Palo.Oros, Rango.Dos);
            var mejorEnMesa = new Carta(Palo.Bastos, Rango.Dos); // triunfo
            var mano = new List<Carta> { new Carta(Palo.Oros, Rango.Rey) };

            bool valida = ValidadorReglas.EsJugadaValidaArrastre(
                mano[0], salida, Palo.Bastos, mano, mejorEnMesa);

            Assert.IsTrue(valida);
        }

        // =====================================================================
        // EsJugadaValidaArrastre — FALLAR
        // =====================================================================

        [Test]
        public void Arrastre_SinCartasDelPaloConTriunfos_DebeFallar()
        {
            var salida = new Carta(Palo.Oros, Rango.Dos);
            var mano = new List<Carta>
            {
                new Carta(Palo.Bastos, Rango.Dos), // triunfo
                new Carta(Palo.Copas, Rango.As),   // ni palo de salida ni triunfo
            };

            bool jugarTriunfo = ValidadorReglas.EsJugadaValidaArrastre(
                mano[0], salida, Palo.Bastos, mano, salida);
            bool jugarOtroPalo = ValidadorReglas.EsJugadaValidaArrastre(
                mano[1], salida, Palo.Bastos, mano, salida);

            Assert.IsTrue(jugarTriunfo);
            Assert.IsFalse(jugarOtroPalo, "Teniendo triunfo, no puede descartar de otro palo");
        }

        [Test]
        public void Arrastre_FallarConTriunfoYaEnMesa_ObligaASuperarloSiPuede()
        {
            var salida = new Carta(Palo.Oros, Rango.Dos);
            var mejorEnMesa = new Carta(Palo.Bastos, Rango.Sota); // ya han fallado con triunfo
            var mano = new List<Carta>
            {
                new Carta(Palo.Bastos, Rango.Rey), // triunfo mayor
                new Carta(Palo.Bastos, Rango.Dos), // triunfo menor
            };

            bool jugarMenor = ValidadorReglas.EsJugadaValidaArrastre(
                mano[1], salida, Palo.Bastos, mano, mejorEnMesa);
            bool jugarMayor = ValidadorReglas.EsJugadaValidaArrastre(
                mano[0], salida, Palo.Bastos, mano, mejorEnMesa);

            Assert.IsFalse(jugarMenor);
            Assert.IsTrue(jugarMayor);
        }

        [Test]
        public void Arrastre_FallarSinPosibilidadDeSuperarTriunfo_CualquierTriunfoVale()
        {
            var salida = new Carta(Palo.Oros, Rango.Dos);
            var mejorEnMesa = new Carta(Palo.Bastos, Rango.Rey); // triunfo alto ya en mesa
            var mano = new List<Carta>
            {
                new Carta(Palo.Bastos, Rango.Dos), // unico triunfo, no supera
            };

            bool valida = ValidadorReglas.EsJugadaValidaArrastre(
                mano[0], salida, Palo.Bastos, mano, mejorEnMesa);

            Assert.IsTrue(valida);
        }

        // =====================================================================
        // EsJugadaValidaArrastre — SIN ASISTIR NI FALLAR
        // =====================================================================

        [Test]
        public void Arrastre_SinPaloNiTriunfo_CualquierCartaVale()
        {
            var salida = new Carta(Palo.Oros, Rango.Dos);
            var mejorEnMesa = salida;
            var mano = new List<Carta>
            {
                new Carta(Palo.Copas, Rango.As),
                new Carta(Palo.Espadas, Rango.Rey),
            };

            Assert.IsTrue(ValidadorReglas.EsJugadaValidaArrastre(
                mano[0], salida, Palo.Bastos, mano, mejorEnMesa));
            Assert.IsTrue(ValidadorReglas.EsJugadaValidaArrastre(
                mano[1], salida, Palo.Bastos, mano, mejorEnMesa));
        }

        [Test]
        public void Arrastre_ElPropioTriunfoComoPaloDeSalida_NoSeConsideraFallo()
        {
            // Si el palo de salida ES el triunfo, no aplica la logica de "fallar":
            // el jugador debe asistir con triunfo (rama ASISTIR), no tratarlo como caso de fallo.
            var salida = new Carta(Palo.Bastos, Rango.Dos); // triunfo como salida
            var mano = new List<Carta>
            {
                new Carta(Palo.Bastos, Rango.Rey), // triunfo, asiste
                new Carta(Palo.Oros, Rango.As),    // no es del palo de salida
            };

            bool jugarTriunfo = ValidadorReglas.EsJugadaValidaArrastre(
                mano[0], salida, Palo.Bastos, mano, salida);
            bool jugarOtro = ValidadorReglas.EsJugadaValidaArrastre(
                mano[1], salida, Palo.Bastos, mano, salida);

            Assert.IsTrue(jugarTriunfo);
            Assert.IsFalse(jugarOtro);
        }
    }
}
