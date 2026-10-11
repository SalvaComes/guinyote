using System;
using System.Collections.Generic;
using GuinoteApp.Core;
using NUnit.Framework;

namespace GuinoteApp.Core.Tests
{
    public class IADificilTests
    {
        private static readonly IReadOnlyList<Carta> SinHistorial = IATestHelper.SinHistorial;

        /// <summary>Dificil que decide solo con reglas (sin la busqueda del Arrastre), para probar la heuristica.</summary>
        private static IADificil Heuristica() => new IADificil(new Random(1), busquedaEnArrastre: false);

        [Test]
        public void ElegirCarta_ManoVacia_DevuelveNull()
        {
            var ia = new IADificil(new Random(1));

            Carta resultado = ia.ElegirCarta(1, new List<Carta>(), new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos, SinHistorial);

            Assert.IsNull(resultado);
        }

        // =====================================================================
        // Fase de Robo
        // =====================================================================

        [Test]
        public void FaseRobo_AlAbrir_SaleConLaCartaMasBarata()
        {
            var ia = new IADificil(new Random(1));
            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.As),   // 11 pts
                new Carta(Palo.Copas, Rango.Dos), // 0 pts, la mas barata
            };

            Carta elegida = ia.ElegirCarta(1, mano, new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Rango.Dos, elegida.Rango, "Debe reservar el As para el Arrastre, no abrir con el");
        }

        [Test]
        public void FaseRobo_AlAbrir_NoRompeUnaParejaDeCante()
        {
            var ia = new IADificil(new Random(1));
            var mano = new List<Carta>
            {
                new Carta(Palo.Copas, Rango.Rey),
                new Carta(Palo.Copas, Rango.Sota),   // sola seria la mas barata (3 pts)
                new Carta(Palo.Espadas, Rango.Rey),  // 4 pts, pero no forma pareja
            };

            Carta elegida = ia.ElegirCarta(1, mano, new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Palo.Espadas, elegida.Palo, "Prefiere soltar el Rey suelto antes que romper Rey+Sota de Copas");
        }

        [Test]
        public void FaseRobo_AlAbrir_ReservaElSieteDeTriunfoSiLaPintaEsValiosa()
        {
            var ia = new IADificil(new Random(1));
            var mano = new List<Carta>
            {
                new Carta(Palo.Bastos, Rango.Siete), // permite cambiar la pinta
                new Carta(Palo.Bastos, Rango.Rey),
            };
            var ctx = IATestHelper.Contexto(1, mano, new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos,
                pinta: new Carta(Palo.Bastos, Rango.As), cartasEnMazo: 10);

            Carta elegida = ia.ElegirCarta(ctx);

            Assert.AreEqual(Rango.Rey, elegida.Rango, "El 7 vale mas como carta de cambio que como triunfo");
        }

        [Test]
        public void FaseRobo_CompaneroGanandoYPuedenCortarle_TiraBarato()
        {
            // Jugador 1 (idx=1), compañero = idx 3. Aun falta un rival por jugar y quedan triunfos fuera.
            var ia = new IADificil(new Random(1));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.Dos), Palo.Bastos);
            baza.RegistrarJugada(3, new Carta(Palo.Oros, Rango.As), Palo.Bastos);

            var mano = new List<Carta>
            {
                new Carta(Palo.Copas, Rango.As),
                new Carta(Palo.Espadas, Rango.Dos),
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseRobo, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Rango.Dos, elegida.Rango, "Sin garantias de que la baza se mantenga, no arriesga sus cartas de valor");
        }

        [Test]
        public void FaseRobo_CompaneroGanandoYEsElUltimoEnJugar_CargaPuntos()
        {
            var ia = new IADificil(new Random(1));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.Dos), Palo.Bastos);
            baza.RegistrarJugada(3, new Carta(Palo.Oros, Rango.As), Palo.Bastos); // compañero (idx 3) gana
            baza.RegistrarJugada(2, new Carta(Palo.Copas, Rango.Cuatro), Palo.Bastos);

            var mano = new List<Carta>
            {
                new Carta(Palo.Espadas, Rango.As),   // 11 pts, la mas valiosa
                new Carta(Palo.Espadas, Rango.Dos),  // 0 pts
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseRobo, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Rango.As, elegida.Rango, "Siendo el ultimo en jugar y con la baza asegurada, debe sumar puntos");
        }

        [Test]
        public void FaseRobo_CompaneroGanandoYEsElUltimo_NoCargaTriunfos()
        {
            var ia = new IADificil(new Random(1));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.Dos), Palo.Bastos);
            baza.RegistrarJugada(3, new Carta(Palo.Oros, Rango.As), Palo.Bastos);
            baza.RegistrarJugada(2, new Carta(Palo.Copas, Rango.Cuatro), Palo.Bastos);

            var mano = new List<Carta>
            {
                new Carta(Palo.Bastos, Rango.As),    // triunfo valioso: se guarda
                new Carta(Palo.Espadas, Rango.Rey),  // 4 pts, no triunfo
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseRobo, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Palo.Espadas, elegida.Palo, "Suma puntos sin gastar el As de triunfo");
        }

        [Test]
        public void FaseRobo_RivalGanandoYCompensaGanar_GanaConLaCartaMinima()
        {
            var ia = new IADificil(new Random(1));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(2, new Carta(Palo.Oros, Rango.As), Palo.Bastos); // rival, 11 pts en mesa

            var mano = new List<Carta>
            {
                new Carta(Palo.Bastos, Rango.Rey),  // triunfo alto, gana
                new Carta(Palo.Bastos, Rango.Dos),  // triunfo bajo, tambien gana
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseRobo, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Rango.Dos, elegida.Rango, "Debe ganar con el triunfo mas barato posible, no malgastar el Rey");
        }

        [Test]
        public void FaseRobo_RivalGanandoSinPuntos_NoGastaCartasParaGanar()
        {
            var ia = new IADificil(new Random(1));
            var baza = new EstadoBaza();
            baza.RegistrarJugada(2, new Carta(Palo.Oros, Rango.Dos), Palo.Bastos);

            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.As),
                new Carta(Palo.Copas, Rango.Cuatro),
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseRobo, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Rango.Cuatro, elegida.Rango);
        }

        // =====================================================================
        // Fase de Arrastre — reglas de apoyo (sin busqueda)
        // =====================================================================

        [Test]
        public void FaseArrastre_AlAbrirConTriunfosRivalesPendientes_TanteaConCartaBarataNoTriunfo()
        {
            var ia = Heuristica();
            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.As),    // 11 pts, no triunfo
                new Carta(Palo.Copas, Rango.Dos),  // 0 pts, no triunfo
                new Carta(Palo.Bastos, Rango.Rey), // triunfo
            };

            Carta elegida = ia.ElegirCarta(1, mano, new EstadoBaza(), GamePhase.FaseArrastre, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Palo.Copas, elegida.Palo);
            Assert.AreEqual(Rango.Dos, elegida.Rango);
        }

        [Test]
        public void FaseArrastre_AlAbrirSinTriunfosRivalesPendientes_SaleConSuTriunfoMaximo()
        {
            var ia = Heuristica();

            // Ya han salido 9 triunfos de Bastos
            var historial = new List<Carta>();
            foreach (Rango r in new[] { Rango.Dos, Rango.Cuatro, Rango.Cinco, Rango.Seis, Rango.Siete,
                                          Rango.Caballo, Rango.Sota, Rango.Rey, Rango.Tres })
            {
                historial.Add(new Carta(Palo.Bastos, r));
            }

            var mano = new List<Carta>
            {
                new Carta(Palo.Bastos, Rango.As), // el ultimo triunfo posible, lo tiene el
                new Carta(Palo.Oros, Rango.Dos),
            };

            Carta elegida = ia.ElegirCarta(1, mano, new EstadoBaza(), GamePhase.FaseArrastre, Palo.Bastos, historial);

            Assert.AreEqual(Rango.As, elegida.Rango, "Sin triunfos rivales pendientes, su triunfo maximo gana seguro");
        }

        [Test]
        public void FaseArrastre_AlAbrirSinTriunfosRivalesYConUnAsMaestro_AbreConElAs()
        {
            var ia = Heuristica();

            // Todos los triunfos han salido
            var historial = new List<Carta>();
            foreach (Rango r in Enum.GetValues(typeof(Rango)))
                historial.Add(new Carta(Palo.Bastos, r));

            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.As),   // el mayor de Oros: nadie puede superarlo ni cortarlo
                new Carta(Palo.Copas, Rango.Dos),
            };

            Carta elegida = ia.ElegirCarta(1, mano, new EstadoBaza(), GamePhase.FaseArrastre, Palo.Bastos, historial);

            Assert.AreEqual(Palo.Oros, elegida.Palo);
            Assert.AreEqual(Rango.As, elegida.Rango);
        }

        [Test]
        public void FaseArrastre_SiPuedeGanar_UsaLaCartaMinimaSuficiente()
        {
            var ia = Heuristica();
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.Sota), Palo.Bastos);

            var mano = new List<Carta>
            {
                new Carta(Palo.Oros, Rango.Rey), // supera a la Sota
                new Carta(Palo.Oros, Rango.As),  // tambien supera, pero es mas valiosa
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseArrastre, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Rango.Rey, elegida.Rango, "Debe ganar con la carta minima suficiente (el Rey), reservando el As");
        }

        [Test]
        public void FaseArrastre_NoPuedeGanarYCompaneroGanaConCartaSegura_AportaLaCartaDeMayorValor()
        {
            var ia = Heuristica();
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.Rey), Palo.Bastos);
            baza.RegistrarJugada(3, new Carta(Palo.Oros, Rango.As), Palo.Bastos); // compañero (idx 3 de J1) va ganando

            // J1 (idx 1) no tiene Oros ni Bastos: jugada libre
            var mano = new List<Carta>
            {
                new Carta(Palo.Copas, Rango.As),   // 11 pts
                new Carta(Palo.Espadas, Rango.Dos), // 0 pts
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseArrastre, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Rango.As, elegida.Rango, "Con el compañero ya ganando, debe cargar puntos");
        }

        [Test]
        public void FaseArrastre_NoPuedeGanarYRivalGana_DescartaLaCartaDeMenorValor()
        {
            var ia = Heuristica();
            var baza = new EstadoBaza();
            baza.RegistrarJugada(2, new Carta(Palo.Oros, Rango.Rey), Palo.Bastos); // rival (idx 2) va ganando

            var mano = new List<Carta>
            {
                new Carta(Palo.Copas, Rango.As),   // 11 pts
                new Carta(Palo.Espadas, Rango.Dos), // 0 pts
            };

            Carta elegida = ia.ElegirCarta(1, mano, baza, GamePhase.FaseArrastre, Palo.Bastos, SinHistorial);

            Assert.AreEqual(Rango.Dos, elegida.Rango, "Con el rival ganando, debe minimizar la perdida");
        }

        // =====================================================================
        // Fase de Arrastre — busqueda con informacion casi completa
        // =====================================================================

        [Test]
        public void FaseArrastre_ConBusqueda_GuardaElTriunfoMaximoParaLaUltimaBaza()
        {
            // Quedan 2 bazas y se conocen todas las cartas ajenas (aunque no quien tiene cual).
            // Salir con el As de triunfo ahora regala la ultima baza (+10) al rival que tiene el As de Oros.
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

            Carta elegida = new IADificil(new Random(3)).ElegirCarta(ctx);

            Assert.AreSame(dosOros, elegida, "Debe salir con el Dos de Oros y reservar el As de triunfo");
        }

        // =====================================================================
        // Cantes y cambios
        // =====================================================================

        [Test]
        public void DecidirCante_PrefiereElPaloDeTriunfo()
        {
            var ia = new IADificil(new Random(1));
            var ctx = IATestHelper.Contexto(1, new List<Carta>(), new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos);

            Assert.AreEqual(Palo.Bastos, ia.DecidirCante(ctx, new List<Palo> { Palo.Copas, Palo.Bastos }));
        }

        [Test]
        public void DecidirCambio_SiempreCambia()
        {
            var ia = new IADificil(new Random(1));
            var ctx = IATestHelper.Contexto(1, new List<Carta>(), new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos);

            Assert.IsTrue(ia.DecidirCambio(ctx, new Carta(Palo.Bastos, Rango.Siete)));
        }
    }
}
