using GuinoteApp.Core;
using NUnit.Framework;

namespace GuinoteApp.Core.Tests
{
    public class MarcadorEquipoTests
    {
        [Test]
        public void SinPuntos_EstaEnMalasConCero()
        {
            var m = new MarcadorEquipo();

            Assert.AreEqual(0, m.PuntosTotales);
            Assert.IsFalse(m.EstaEnBuenas);
            Assert.AreEqual(0, m.PuntosMostrados);
            Assert.AreEqual("0 Malas", m.ToString());
        }

        [Test]
        public void HastaCuarentaYNueve_SonMalasConLosPuntosTalCual()
        {
            var m = new MarcadorEquipo();
            m.AnotarPuntosRonda(49);

            Assert.IsFalse(m.EstaEnBuenas);
            Assert.AreEqual(49, m.PuntosMostrados);
            Assert.AreEqual("49 Malas", m.ToString());
        }

        [Test]
        public void Cincuenta_EntraEnBuenas()
        {
            var m = new MarcadorEquipo();
            m.AnotarPuntosRonda(50);

            Assert.IsTrue(m.EstaEnBuenas);
            Assert.AreEqual(0, m.PuntosMostrados);
        }

        [Test]
        public void SesentaYCinco_SonQuinceBuenas()
        {
            var m = new MarcadorEquipo();
            m.AnotarPuntosRonda(65);

            Assert.AreEqual(15, m.PuntosMostrados);
            Assert.AreEqual("15 Buenas", m.ToString());
        }

        [Test]
        public void LosPuntosSeAcumulanRondaTrasRonda_EjemploDelEnunciado()
        {
            var m = new MarcadorEquipo();
            m.AnotarPuntosRonda(65);
            Assert.IsFalse(m.HaGanadoJuego());

            m.AnotarPuntosRonda(40);

            Assert.AreEqual(105, m.PuntosTotales);
            Assert.AreEqual(55, m.PuntosMostrados);
            Assert.AreEqual("55 Buenas", m.ToString());
            Assert.IsTrue(m.HaGanadoJuego());
        }

        [TestCase(99, false)]
        [TestCase(100, false)]
        [TestCase(101, true)]
        [TestCase(130, true)]
        public void HaGanadoJuego_SoloSiSuperaLos100(int puntos, bool esperado)
        {
            var m = new MarcadorEquipo();
            m.AnotarPuntosRonda(puntos);

            Assert.AreEqual(esperado, m.HaGanadoJuego());
        }

        [Test]
        public void Reiniciar_VuelveACero()
        {
            var m = new MarcadorEquipo();
            m.AnotarPuntosRonda(120);

            m.Reiniciar();

            Assert.AreEqual(0, m.PuntosTotales);
            Assert.IsFalse(m.EstaEnBuenas);
        }
    }

    public class CotoPartidaTests
    {
        [Test]
        public void ManoSinGanador_AcumulaPuntosYNoCierraElJuego()
        {
            var coto = new CotoPartida(3);

            ResultadoRonda r = coto.EvaluarFinDeRonda(65, 65, 1);

            Assert.AreEqual(0, r.GanadorJuego);
            Assert.AreEqual(0, r.GanadorCoto);
            Assert.AreEqual(65, coto.Equipo1.PuntosTotales);
            Assert.AreEqual("Equipo 1: 15 Buenas | Equipo 2: 15 Buenas", r.Resumen);
        }

        [Test]
        public void ResumenEjemploDelEnunciado()
        {
            var coto = new CotoPartida(3);

            ResultadoRonda r = coto.EvaluarFinDeRonda(65, 40, 1);

            Assert.AreEqual("Equipo 1: 15 Buenas | Equipo 2: 40 Malas", r.Resumen);
        }

        [Test]
        public void EquipoQueSuperaLos100_GanaElJuegoYSeReinician()
        {
            var coto = new CotoPartida(3);
            coto.EvaluarFinDeRonda(65, 30, 1);

            ResultadoRonda r = coto.EvaluarFinDeRonda(40, 50, 1);

            Assert.AreEqual(1, r.GanadorJuego);
            Assert.AreEqual(1, coto.JuegosGanadosEquipo1);
            Assert.AreEqual(0, coto.JuegosGanadosEquipo2);
            Assert.AreEqual(0, coto.Equipo1.PuntosTotales, "El marcador vuelve a 0 para el siguiente Juego");
            Assert.AreEqual(0, coto.Equipo2.PuntosTotales);
            Assert.AreEqual(105, r.PuntosTotalesEquipo1, "El resultado conserva los puntos antes del reinicio");
            Assert.AreEqual(0, r.GanadorCoto);
        }

        [Test]
        public void AmbosSuperanLos100_GanaElQueTengaMasPuntos()
        {
            var coto = new CotoPartida(3);
            coto.EvaluarFinDeRonda(60, 60, 1);

            ResultadoRonda r = coto.EvaluarFinDeRonda(45, 50, 1);

            Assert.AreEqual(2, r.GanadorJuego, "110 puntos del equipo 2 frente a 105 del equipo 1");
        }

        [Test]
        public void AmbosSuperanLos100ConIgualesPuntos_GanaQuienHizoLasUltimas()
        {
            var coto = new CotoPartida(3);
            coto.EvaluarFinDeRonda(60, 60, 1);

            Assert.AreEqual(2, new CotoPartida(3).EvaluarFinDeRondaDosVeces(60, 60, 45, 45, 2));
            ResultadoRonda r = coto.EvaluarFinDeRonda(45, 45, 1);

            Assert.AreEqual(1, r.GanadorJuego);
        }

        [Test]
        public void IdasAmbosSuperan100_GanaQuienHizoLasUltimasAunqueTengaMenosPuntos()
        {
            var coto = new CotoPartida(3);

            ResultadoRonda r = coto.EvaluarFinDeRonda(105, 110, 1);

            Assert.AreEqual(1, r.GanadorJuego);
        }

        [Test]
        public void IdasAmbosSuperan100_QuienNoLlegaA30SinCantesPierde()
        {
            var coto = new CotoPartida(3);

            ResultadoRonda r = coto.EvaluarFinDeRonda(105, 110, 1, puntosBazasEq1: 20, puntosBazasEq2: 60);

            Assert.AreEqual(2, r.GanadorJuego);
        }

        [Test]
        public void EsManoDeVueltas_FalsaAlEmpezarYVerdaderaTrasUnaManoSinGanador()
        {
            var coto = new CotoPartida(3);
            Assert.IsFalse(coto.EsManoDeVueltas);

            coto.EvaluarFinDeRonda(40, 40, 1);
            Assert.IsTrue(coto.EsManoDeVueltas);

            coto.EvaluarFinDeRonda(70, 10, 1);
            Assert.IsFalse(coto.EsManoDeVueltas, "Tras ganar un Juego vuelven las Idas");
        }

        [Test]
        public void ElPrimeroEnLlegarAlNumeroDeJuegosGanaElCoto()
        {
            var coto = new CotoPartida(2);

            coto.EvaluarFinDeRonda(101, 20, 1);
            Assert.AreEqual(0, coto.GanadorCoto);

            ResultadoRonda r = coto.EvaluarFinDeRonda(30, 105, 2);
            Assert.AreEqual(2, r.GanadorJuego);
            Assert.AreEqual(0, r.GanadorCoto, "1 juego cada uno");

            r = coto.EvaluarFinDeRonda(110, 40, 1);
            Assert.AreEqual(1, r.GanadorJuego);
            Assert.AreEqual(1, r.GanadorCoto);
            Assert.AreEqual(2, r.JuegosEquipo1);
            Assert.AreEqual(1, r.JuegosEquipo2);
        }

        [Test]
        public void GanadorDelJuego_NadieSupera100_DevuelveCero()
        {
            var a = new MarcadorEquipo();
            var b = new MarcadorEquipo();
            a.AnotarPuntosRonda(100);
            b.AnotarPuntosRonda(80);

            Assert.AreEqual(0, CotoPartida.GanadorDelJuego(a, b, 1));
        }

        [Test]
        public void JuegosParaGanarMenorQueUno_SeAjustaAUno()
        {
            Assert.AreEqual(1, new CotoPartida(0).JuegosParaGanar);
        }
    }

    internal static class CotoPartidaTestExtensions
    {
        /// <summary>Juega dos manos seguidas y devuelve el ganador del Juego de la segunda.</summary>
        public static int EvaluarFinDeRondaDosVeces(
            this CotoPartida coto, int a1, int b1, int a2, int b2, int equipoUltimas)
        {
            coto.EvaluarFinDeRonda(a1, b1, equipoUltimas);
            return coto.EvaluarFinDeRonda(a2, b2, equipoUltimas).GanadorJuego;
        }
    }
}
