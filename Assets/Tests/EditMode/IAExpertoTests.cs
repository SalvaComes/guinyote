using System;
using System.Collections.Generic;
using GuinoteApp.Core;
using NUnit.Framework;

namespace GuinoteApp.Core.Tests
{
    public class IAExpertoTests
    {
        private static readonly IReadOnlyList<Carta> SinHistorial = IATestHelper.SinHistorial;

        [Test]
        public void ElegirCarta_ManoVacia_DevuelveNull()
        {
            var ia = new IAExperto(new Random(1), muestras: 8);

            Assert.IsNull(ia.ElegirCarta(1, new List<Carta>(), new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos, SinHistorial));
        }

        [Test]
        public void ElegirCarta_UnaSolaJugadaLegal_LaDevuelve()
        {
            var ia = new IAExperto(new Random(1), muestras: 8);
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.Rey), Palo.Bastos);
            var unicaDeOros = new Carta(Palo.Oros, Rango.Cuatro);
            var mano = new List<Carta> { unicaDeOros, new Carta(Palo.Copas, Rango.As) };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseArrastre, Palo.Bastos, SinHistorial);

            Assert.AreSame(unicaDeOros, elegida, "Debe asistir: no hay eleccion");
        }

        [Test]
        public void ElegirCarta_ContextoIncoherente_CaeALaHeuristicaYDevuelveUnaCartaDeLaMano()
        {
            // Historial vacio: las cartas desconocidas no cuadran con las manos ajenas
            var ia = new IAExperto(new Random(1), muestras: 8);
            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.As),
                new Carta(Palo.Copas, Rango.Dos),
            };

            Carta elegida = ia.ElegirCarta(1, mano, new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos, SinHistorial);

            CollectionAssert.Contains(mano, elegida);
        }

        [Test]
        public void ElegirCarta_ConInformacionCasiCompleta_GuardaElTriunfoMaximoParaLaUltimaBaza()
        {
            Palo triunfo = Palo.Bastos;
            var asBastos = new Carta(Palo.Bastos, Rango.As);
            var dosOros = new Carta(Palo.Oros, Rango.Dos);
            var mano = new List<Carta> { asBastos, dosOros };

            var ajenas = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.Tres), new Carta(Palo.Oros, Rango.As),
                new Carta(Palo.Copas, Rango.Dos), new Carta(Palo.Copas, Rango.Cuatro),
                new Carta(Palo.Espadas, Rango.Dos), new Carta(Palo.Espadas, Rango.Cuatro),
            };
            var historial = IATestHelper.Baraja();
            historial.RemoveAll(c => mano.Exists(m => m.Palo == c.Palo && m.Rango == c.Rango) ||
                                     ajenas.Exists(m => m.Palo == c.Palo && m.Rango == c.Rango));

            var ctx = IATestHelper.Contexto(0, mano, new EstadoBaza(), GamePhase.FaseArrastre, triunfo, historial);

            Carta elegida = new IAExperto(new Random(5), muestras: 60).ElegirCarta(ctx);

            Assert.AreSame(dosOros, elegida);
        }

        [Test]
        public void ManoCompleta_ConExpertos_SiempreJuegaJugadasLegalesYTermina()
        {
            var rng = new Random(2024);
            for (int partida = 0; partida < 3; partida++)
            {
                var estrategias = new IEstrategiaIA[]
                {
                    new IAExperto(rng, 6), new IAExperto(rng, 6), new IAExperto(rng, 6), new IAExperto(rng, 6)
                };
                SimuladorMano sim = IATestHelper.RepartirMano(rng, estrategias);

                // JugarHastaElFinal lanza si hay una jugada ilegal o una mano que no termina
                IATestHelper.JugarHastaElFinal(sim, estrategias);

                Assert.GreaterOrEqual(sim.Puntos[0] + sim.Puntos[1], 130);
            }
        }

        [Test]
        public void ManoCompleta_JugadasEnArrastreCumplenLasReglas()
        {
            var rng = new Random(99);
            var estrategias = new IEstrategiaIA[]
            {
                new IAExperto(rng, 6), new IADificil(rng), new IAExperto(rng, 6), new IADificil(rng)
            };
            SimuladorMano sim = IATestHelper.RepartirMano(rng, estrategias);
            sim.Estrategias = estrategias;

            while (!sim.Terminada)
            {
                int turno = sim.Turno;
                ContextoIA ctx = sim.CrearContexto(turno);
                Carta carta = estrategias[turno].ElegirCarta(ctx);

                if (sim.Fase == GamePhase.FaseArrastre && sim.Baza.CartaSalida != null)
                {
                    Assert.IsTrue(
                        ValidadorReglas.EsJugadaValidaArrastre(
                            carta, sim.Baza.CartaSalida, sim.Triunfo, sim.Manos[turno], sim.Baza.MejorCartaActual),
                        $"Jugada ilegal en Arrastre del asiento {turno}: {carta}");
                }
                sim.Jugar(carta);
            }
        }

        [Test]
        public void DecidirCante_PrefiereElPaloDeTriunfo()
        {
            var ia = new IAExperto(new Random(1), muestras: 8);
            var ctx = IATestHelper.Contexto(1, new List<Carta>(), new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos);

            Assert.AreEqual(Palo.Bastos, ia.DecidirCante(ctx, new List<Palo> { Palo.Oros, Palo.Bastos }));
        }
    }
}
