using System;
using System.Collections.Generic;
using GuinoteApp.Core;
using NUnit.Framework;

namespace GuinoteApp.Core.Tests
{
    public class SimuladorManoTests
    {
        private static IEstrategiaIA[] TodosNormal(Random rng) =>
            new IEstrategiaIA[] { new IANormal(rng), new IANormal(rng), new IANormal(rng), new IANormal(rng) };

        [Test]
        public void ManoSinCantes_SumaSiempre130Puntos()
        {
            var rng = new Random(11);
            for (int i = 0; i < 30; i++)
            {
                var estrategias = TodosNormal(rng);
                SimuladorMano sim = IATestHelper.RepartirMano(rng);
                // Sin Estrategias asignadas al simulador no hay cantes; solo las usamos para elegir carta
                int seguridad = 0;
                while (!sim.Terminada)
                {
                    Assert.Less(++seguridad, 100);
                    sim.Jugar(estrategias[sim.Turno].ElegirCarta(sim.CrearContexto(sim.Turno)));
                }

                Assert.AreEqual(130, sim.Puntos[0] + sim.Puntos[1], "120 de cartas + 10 de ultimas");
            }
        }

        [Test]
        public void ManoCompleta_TerminaConLasManosVaciasYArrastreActivo()
        {
            var rng = new Random(5);
            var estrategias = TodosNormal(rng);
            SimuladorMano sim = IATestHelper.RepartirMano(rng, estrategias);

            IATestHelper.JugarHastaElFinal(sim, estrategias);

            foreach (var mano in sim.Manos)
                Assert.IsEmpty(mano);
            Assert.AreEqual(GamePhase.FaseArrastre, sim.Fase);
            Assert.IsNull(sim.Pinta);
            Assert.IsEmpty(sim.Mazo);
        }

        [Test]
        public void ManoCompleta_ConCantes_LosPuntosNuncaBajanDe130()
        {
            var rng = new Random(77);
            for (int i = 0; i < 40; i++)
            {
                var estrategias = TodosNormal(rng);
                SimuladorMano sim = IATestHelper.RepartirMano(rng, estrategias);

                IATestHelper.JugarHastaElFinal(sim, estrategias);

                int total = sim.Puntos[0] + sim.Puntos[1];
                Assert.GreaterOrEqual(total, 130);
                Assert.AreEqual(0, (total - 130) % 20, "Los cantes suman de 20 en 20 (o 40)");
                Assert.LessOrEqual(sim.PalosCantados.Count, 4);
            }
        }

        [Test]
        public void Robo_ElUltimoEnRobarSeLlevaLaPinta()
        {
            // Mazo con 3 cartas: tras la baza, 3 roban del mazo y el cuarto se lleva la pinta
            var pinta = new Carta(Palo.Oros, Rango.As);
            var manos = new List<Carta>[4];
            for (int i = 0; i < 4; i++)
                manos[i] = new List<Carta> { new Carta(Palo.Copas, (Rango)i), new Carta(Palo.Espadas, (Rango)i) };

            var mazo = new List<Carta>
            {
                new Carta(Palo.Bastos, Rango.Dos),
                new Carta(Palo.Bastos, Rango.Cuatro),
                new Carta(Palo.Bastos, Rango.Cinco),
            };

            var sim = new SimuladorMano(manos, mazo, pinta, Palo.Oros, GamePhase.FaseRobo, new EstadoBaza(), 0);
            for (int j = 0; j < 4; j++)
                sim.Jugar(sim.Manos[sim.Turno][0]);

            Assert.AreEqual(GamePhase.FaseArrastre, sim.Fase, "Mazo y pinta agotados");
            Assert.IsNull(sim.Pinta);
            foreach (var mano in sim.Manos)
                Assert.AreEqual(2, mano.Count);
        }

        [Test]
        public void Cante_ElCompaneroDelQueGanaLaBazaTambienPuedeCantar()
        {
            var manos = new List<Carta>[4];
            manos[0] = new List<Carta> { new Carta(Palo.Espadas, Rango.As), new Carta(Palo.Bastos, Rango.Dos), new Carta(Palo.Bastos, Rango.Cuatro) };
            manos[1] = new List<Carta> { new Carta(Palo.Espadas, Rango.Cinco), new Carta(Palo.Bastos, Rango.Cinco), new Carta(Palo.Bastos, Rango.Seis) };
            manos[2] = new List<Carta> { new Carta(Palo.Espadas, Rango.Dos), new Carta(Palo.Copas, Rango.Rey), new Carta(Palo.Copas, Rango.Sota) };
            manos[3] = new List<Carta> { new Carta(Palo.Espadas, Rango.Seis), new Carta(Palo.Bastos, Rango.Siete), new Carta(Palo.Bastos, Rango.Caballo) };

            var mazo = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.Dos), new Carta(Palo.Oros, Rango.Cuatro),
                new Carta(Palo.Oros, Rango.Cinco), new Carta(Palo.Oros, Rango.Seis),
            };
            var estrategias = new IEstrategiaIA[]
            {
                new IANormal(new Random(1)), new IANormal(new Random(1)), new IANormal(new Random(1)), new IANormal(new Random(1)),
            };
            var sim = new SimuladorMano(manos, mazo, new Carta(Palo.Oros, Rango.Tres), Palo.Oros,
                GamePhase.FaseRobo, new EstadoBaza(), 0) { Estrategias = estrategias };

            // Gana el jugador 0 (As de Espadas); es su compañero, el 2, quien tiene Rey y Sota de Copas
            for (int i = 0; i < 4; i++)
                sim.Jugar(sim.Manos[sim.Turno][0]);

            Assert.AreEqual(0, sim.Turno, "Abre quien gano la baza");
            CollectionAssert.Contains(sim.PalosCantados, Palo.Copas);
            Assert.AreEqual(11 + 20, sim.Puntos[0], "11 de la baza + 20 del cante del compañero");
            Assert.AreEqual(1, sim.PalosCantados.Count, "Solo se canta un palo por baza");
        }

        [Test]
        public void Cambio_ConElSieteDeTriunfo_IntercambiaLaPinta()
        {
            var pinta = new Carta(Palo.Oros, Rango.As);
            var siete = new Carta(Palo.Oros, Rango.Siete);

            var manos = new List<Carta>[4];
            manos[0] = new List<Carta> { new Carta(Palo.Copas, Rango.As), siete };
            manos[1] = new List<Carta> { new Carta(Palo.Copas, Rango.Dos), new Carta(Palo.Espadas, Rango.Dos) };
            manos[2] = new List<Carta> { new Carta(Palo.Copas, Rango.Cuatro), new Carta(Palo.Espadas, Rango.Cuatro) };
            manos[3] = new List<Carta> { new Carta(Palo.Copas, Rango.Cinco), new Carta(Palo.Espadas, Rango.Cinco) };

            var mazo = new List<Carta>
            {
                new Carta(Palo.Bastos, Rango.Dos), new Carta(Palo.Bastos, Rango.Cuatro),
                new Carta(Palo.Bastos, Rango.Cinco), new Carta(Palo.Bastos, Rango.Seis),
                new Carta(Palo.Bastos, Rango.Siete),
            };

            var estrategias = new IEstrategiaIA[]
            {
                new IANormal(new Random(1)), new IANormal(new Random(1)),
                new IANormal(new Random(1)), new IANormal(new Random(1)),
            };
            var sim = new SimuladorMano(manos, mazo, pinta, Palo.Oros, GamePhase.FaseRobo, new EstadoBaza(), 0)
            {
                Estrategias = estrategias
            };

            // El As de Copas del jugador 0 gana la baza; su pareja (0 y 2) puede cambiar
            sim.Jugar(manos[0][0]);
            sim.Jugar(manos[1][0]);
            sim.Jugar(manos[2][0]);
            sim.Jugar(manos[3][0]);

            Assert.AreSame(siete, sim.Pinta, "El 7 de triunfo pasa a ser la pinta");
            Assert.IsTrue(sim.Manos[0].Contains(pinta) || sim.Manos[1].Contains(pinta) ||
                          sim.Manos[2].Contains(pinta) || sim.Manos[3].Contains(pinta) ||
                          sim.Mazo.Contains(pinta),
                          "El As de Oros ha salido de la pinta");
        }
    }
}
