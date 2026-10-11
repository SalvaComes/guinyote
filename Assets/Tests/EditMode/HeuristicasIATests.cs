using System.Collections.Generic;
using GuinoteApp.Core;
using NUnit.Framework;

namespace GuinoteApp.Core.Tests
{
    public class HeuristicasIATests
    {
        // =====================================================================
        // Jugadas validas
        // =====================================================================

        [Test]
        public void JugadasValidas_FaseRobo_TodasLasCartas()
        {
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.Rey), Palo.Bastos);
            var mano = new List<Carta> { new Carta(Palo.Oros, Rango.Dos), new Carta(Palo.Copas, Rango.As) };
            var ctx = IATestHelper.Contexto(1, mano, baza, GamePhase.FaseRobo, Palo.Bastos);

            Assert.AreEqual(2, HeuristicasIA.JugadasValidas(ctx).Count);
        }

        [Test]
        public void JugadasValidas_Arrastre_AplicaAsistirYMontar()
        {
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.Caballo), Palo.Bastos);
            var rey = new Carta(Palo.Oros, Rango.Rey);
            var mano = new List<Carta> { rey, new Carta(Palo.Oros, Rango.Cuatro), new Carta(Palo.Copas, Rango.As) };
            var ctx = IATestHelper.Contexto(1, mano, baza, GamePhase.FaseArrastre, Palo.Bastos);

            List<Carta> validas = HeuristicasIA.JugadasValidas(ctx);

            CollectionAssert.AreEqual(new[] { rey }, validas, "Debe montar con el Rey");
        }

        // =====================================================================
        // Memoria de cartas
        // =====================================================================

        [Test]
        public void CartasVistas_IncluyeManoHistorialBazaYPinta()
        {
            var baza = new EstadoBaza();
            baza.RegistrarJugada(0, new Carta(Palo.Oros, Rango.Rey), Palo.Bastos);
            var mano = new List<Carta> { new Carta(Palo.Copas, Rango.As) };
            var historial = new List<Carta> { new Carta(Palo.Espadas, Rango.Tres) };
            var ctx = IATestHelper.Contexto(1, mano, baza, GamePhase.FaseRobo, Palo.Bastos,
                historial, pinta: new Carta(Palo.Bastos, Rango.Siete));

            bool[] vistas = HeuristicasIA.CartasVistas(ctx);

            Assert.IsTrue(vistas[HeuristicasIA.Clave(new Carta(Palo.Copas, Rango.As))]);
            Assert.IsTrue(vistas[HeuristicasIA.Clave(new Carta(Palo.Oros, Rango.Rey))]);
            Assert.IsTrue(vistas[HeuristicasIA.Clave(new Carta(Palo.Espadas, Rango.Tres))]);
            Assert.IsTrue(vistas[HeuristicasIA.Clave(new Carta(Palo.Bastos, Rango.Siete))]);
            Assert.IsFalse(vistas[HeuristicasIA.Clave(new Carta(Palo.Oros, Rango.As))]);
        }

        [Test]
        public void DesconocidasDelPalo_CuentaLasQueNoSeHanVisto()
        {
            var mano = new List<Carta> { new Carta(Palo.Bastos, Rango.As) };
            var historial = new List<Carta> { new Carta(Palo.Bastos, Rango.Tres), new Carta(Palo.Bastos, Rango.Rey) };
            var ctx = IATestHelper.Contexto(1, mano, new EstadoBaza(), GamePhase.FaseArrastre, Palo.Bastos, historial);

            bool[] vistas = HeuristicasIA.CartasVistas(ctx);

            Assert.AreEqual(7, HeuristicasIA.DesconocidasDelPalo(vistas, Palo.Bastos));
            Assert.AreEqual(10, HeuristicasIA.DesconocidasDelPalo(vistas, Palo.Oros));
        }

        [Test]
        public void EsMaestra_SiNoQuedaNingunaMayorSinVer()
        {
            var historial = new List<Carta> { new Carta(Palo.Oros, Rango.As) };
            var ctx = IATestHelper.Contexto(1, new List<Carta>(), new EstadoBaza(), GamePhase.FaseArrastre, Palo.Bastos, historial);
            bool[] vistas = HeuristicasIA.CartasVistas(ctx);

            Assert.IsTrue(HeuristicasIA.EsMaestra(new Carta(Palo.Oros, Rango.As), vistas));
            Assert.IsFalse(HeuristicasIA.EsMaestra(new Carta(Palo.Oros, Rango.Rey), vistas), "Falta el Tres por ver");
        }

        // =====================================================================
        // Probabilidad de ser superada
        // =====================================================================

        [Test]
        public void ProbSuperada_SinJugadoresPorResponder_EsCero()
        {
            var ctx = IATestHelper.Contexto(1, new List<Carta> { new Carta(Palo.Oros, Rango.Dos) },
                new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos);
            bool[] vistas = HeuristicasIA.CartasVistas(ctx);

            double p = HeuristicasIA.ProbSuperada(ctx, vistas, new Carta(Palo.Oros, Rango.Dos), Palo.Oros, 0);

            Assert.AreEqual(0.0, p, 1e-9);
        }

        [Test]
        public void ProbSuperada_UnTriunfoMaestro_EsCero()
        {
            var historial = new List<Carta>();
            foreach (Rango r in new[] { Rango.Dos, Rango.Cuatro, Rango.Cinco, Rango.Seis, Rango.Siete,
                                          Rango.Caballo, Rango.Sota, Rango.Rey, Rango.Tres })
                historial.Add(new Carta(Palo.Bastos, r));
            var ctx = IATestHelper.Contexto(1, new List<Carta> { new Carta(Palo.Bastos, Rango.As) },
                new EstadoBaza(), GamePhase.FaseArrastre, Palo.Bastos, historial);
            bool[] vistas = HeuristicasIA.CartasVistas(ctx);

            double p = HeuristicasIA.ProbSuperada(ctx, vistas, new Carta(Palo.Bastos, Rango.As), Palo.Bastos, 3);

            Assert.AreEqual(0.0, p, 1e-9);
        }

        [Test]
        public void ProbSuperada_CartaBajaConMuchosRivales_EsMayorQueCartaAlta()
        {
            var ctx = IATestHelper.Contexto(1,
                new List<Carta> { new Carta(Palo.Oros, Rango.Dos), new Carta(Palo.Oros, Rango.As) },
                new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos);
            bool[] vistas = HeuristicasIA.CartasVistas(ctx);

            double baja = HeuristicasIA.ProbSuperada(ctx, vistas, new Carta(Palo.Oros, Rango.Dos), Palo.Oros, 3);
            double alta = HeuristicasIA.ProbSuperada(ctx, vistas, new Carta(Palo.Oros, Rango.As), Palo.Oros, 3);

            Assert.Greater(baja, alta);
            Assert.GreaterOrEqual(baja, 0.0);
            Assert.LessOrEqual(baja, 1.0);
        }

        [Test]
        public void ProbSuperada_MasJugadoresPorResponder_NuncaEsMenor()
        {
            var ctx = IATestHelper.Contexto(1, new List<Carta> { new Carta(Palo.Oros, Rango.Rey) },
                new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos);
            bool[] vistas = HeuristicasIA.CartasVistas(ctx);
            var carta = new Carta(Palo.Oros, Rango.Rey);

            double uno = HeuristicasIA.ProbSuperada(ctx, vistas, carta, Palo.Oros, 1);
            double tres = HeuristicasIA.ProbSuperada(ctx, vistas, carta, Palo.Oros, 3);

            Assert.GreaterOrEqual(tres, uno);
        }

        // =====================================================================
        // Comparadores
        // =====================================================================

        [Test]
        public void MenorCoste_PrefiereCartasSinPuntosYSinTriunfo()
        {
            var cartas = new List<Carta>
            {
                new Carta(Palo.Bastos, Rango.Dos),  // triunfo sin puntos
                new Carta(Palo.Oros, Rango.Rey),    // 4 pts
                new Carta(Palo.Copas, Rango.Cuatro), // 0 pts, no triunfo
            };

            Carta res = HeuristicasIA.MenorCoste(cartas, Palo.Bastos);

            Assert.AreEqual(Palo.Copas, res.Palo);
        }

        [Test]
        public void MayorValor_PrefiereMasPuntosYAIgualdadElNoTriunfo()
        {
            var asOros = new Carta(Palo.Oros, Rango.As);
            var asBastos = new Carta(Palo.Bastos, Rango.As);

            Assert.AreSame(asOros, HeuristicasIA.MayorValor(new List<Carta> { asBastos, asOros }, Palo.Bastos));
            Assert.AreSame(asBastos, HeuristicasIA.MayorValor(
                new List<Carta> { new Carta(Palo.Oros, Rango.Rey), asBastos }, Palo.Bastos));
        }

        [Test]
        public void GanadoraMasDebil_PrefiereNoTriunfoYMenorRango()
        {
            var rey = new Carta(Palo.Oros, Rango.Rey);
            var as_ = new Carta(Palo.Oros, Rango.As);
            var triunfo = new Carta(Palo.Bastos, Rango.Dos);

            Assert.AreSame(rey, HeuristicasIA.GanadoraMasDebil(new List<Carta> { as_, triunfo, rey }, Palo.Bastos));
            Assert.AreSame(triunfo, HeuristicasIA.GanadoraMasDebil(
                new List<Carta> { new Carta(Palo.Bastos, Rango.Rey), triunfo }, Palo.Bastos));
        }

        [Test]
        public void CosteEstrategico_ParejaDeCantePesaMasQueUnaCartaSuelta()
        {
            var mano = new List<Carta>
            {
                new Carta(Palo.Copas, Rango.Rey), new Carta(Palo.Copas, Rango.Sota), new Carta(Palo.Espadas, Rango.Rey)
            };
            var ctx = IATestHelper.Contexto(1, mano, new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos);

            int sotaConPareja = HeuristicasIA.CosteEstrategico(ctx, mano[1]);
            int reySuelto = HeuristicasIA.CosteEstrategico(ctx, mano[2]);

            Assert.Greater(sotaConPareja, reySuelto);
        }

        [Test]
        public void CosteEstrategico_ParejaYaCantada_NoSeProtege()
        {
            var mano = new List<Carta> { new Carta(Palo.Copas, Rango.Rey), new Carta(Palo.Copas, Rango.Sota) };
            var ctx = IATestHelper.Contexto(1, mano, new EstadoBaza(), GamePhase.FaseRobo, Palo.Bastos,
                palosCantados: new List<Palo> { Palo.Copas });

            Assert.AreEqual(HeuristicasIA.Coste(mano[1], Palo.Bastos), HeuristicasIA.CosteEstrategico(ctx, mano[1]));
        }

        [Test]
        public void MejorCante_PrefiereTriunfoYDevuelveNullSiNoHayOpciones()
        {
            Assert.AreEqual(Palo.Bastos, HeuristicasIA.MejorCante(new List<Palo> { Palo.Oros, Palo.Bastos }, Palo.Bastos));
            Assert.AreEqual(Palo.Oros, HeuristicasIA.MejorCante(new List<Palo> { Palo.Oros, Palo.Copas }, Palo.Bastos));
            Assert.IsNull(HeuristicasIA.MejorCante(new List<Palo>(), Palo.Bastos));
        }
    }
}
