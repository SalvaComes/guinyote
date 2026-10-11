using System;
using System.Collections.Generic;
using GuinoteApp.Core;
using NUnit.Framework;

namespace GuinoteApp.Core.Tests
{
    public class IAFacilTests
    {
        private static readonly IReadOnlyList<Carta> SinHistorial = IATestHelper.SinHistorial;

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

            var elegidas = new HashSet<Carta>();
            for (int i = 0; i < 40; i++)
            {
                Carta elegida = ia.ElegirCarta(1, mano, new EstadoBaza(), GamePhase.FaseArrastre, Palo.Bastos, SinHistorial);
                CollectionAssert.Contains(mano, elegida);
                elegidas.Add(elegida);
            }
            Assert.AreEqual(2, elegidas.Count, "Al abrir elige al azar: a la larga sale con ambas cartas");
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

            for (int i = 0; i < 40; i++)
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

            for (int i = 0; i < 40; i++)
            {
                Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseArrastre, Palo.Bastos, SinHistorial);
                Assert.AreEqual(Palo.Bastos, elegida.Palo, "Debe fallar con triunfo si no puede asistir");
            }
        }

        [Test]
        public void FaseRobo_ConElCompaneroGanando_AVecesLeQuitaLaBaza()
        {
            // Es "facil" porque no mira a su compañero: a veces gana por encima de el
            var ia = new IAFacil(new Random(11));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(3, new Carta(Palo.Oros, Rango.Rey), Palo.Bastos); // compañero del idx 1

            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.As),   // gana al Rey del compañero
                new Carta(Palo.Copas, Rango.Dos),
                new Carta(Palo.Espadas, Rango.Cuatro),
            };

            int superaAlCompanero = 0;
            for (int i = 0; i < 200; i++)
            {
                Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseRobo, Palo.Bastos, SinHistorial);
                if (elegida.Rango == Rango.As) superaAlCompanero++;
            }

            Assert.Greater(superaAlCompanero, 40, "Un principiante despistado le quita la baza a su compañero con frecuencia");
        }

        [Test]
        public void DecidirCante_ASvecesSeOlvidaDeCantar()
        {
            var ia = new IAFacil(new Random(21));
            var ctx = IATestHelper.Contexto(1, new List<Carta>(), new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos);
            var palos = new List<Palo> { Palo.Oros };

            int noCanta = 0;
            for (int i = 0; i < 300; i++)
                if (ia.DecidirCante(ctx, palos) == null) noCanta++;

            Assert.Greater(noCanta, 45, "Se olvida de cantar con frecuencia");
            Assert.Less(noCanta, 180, "Pero tambien canta muchas veces");
        }

        [Test]
        public void DecidirCante_SoloDevuelvePalosDisponibles()
        {
            var ia = new IAFacil(new Random(21));
            var ctx = IATestHelper.Contexto(1, new List<Carta>(), new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos);
            var palos = new List<Palo> { Palo.Oros, Palo.Copas };

            for (int i = 0; i < 100; i++)
            {
                Palo? elegido = ia.DecidirCante(ctx, palos);
                if (elegido.HasValue)
                    CollectionAssert.Contains(palos, elegido.Value);
            }
        }

        [Test]
        public void DecidirCambio_ASvecesSeOlvidaDeCambiar()
        {
            var ia = new IAFacil(new Random(33));
            var ctx = IATestHelper.Contexto(1, new List<Carta>(), new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos);

            int cambia = 0;
            for (int i = 0; i < 300; i++)
                if (ia.DecidirCambio(ctx, new Carta(Palo.Bastos, Rango.Siete))) cambia++;

            Assert.Greater(cambia, 90);
            Assert.Less(cambia, 210);
        }
    }
}
