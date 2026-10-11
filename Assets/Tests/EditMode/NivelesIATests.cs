using System;
using System.Collections.Generic;
using GuinoteApp.Core;
using NUnit.Framework;

namespace GuinoteApp.Core.Tests
{
    /// <summary>
    /// Pruebas de nivel: enfrentan a los niveles de dificultad en manos completas simuladas
    /// (con cantes y cambios) y comprueban que cada nivel gana de forma consistente al inferior.
    /// Las semillas son fijas, asi que los resultados son reproducibles.
    /// </summary>
    [Category("Simulacion")]
    public class NivelesIATests
    {
        [Test]
        public void Normal_GanaClaramenteAlFacil()
        {
            double pct = IATestHelper.PorcentajeVictorias(NivelDificultadIA.Normal, NivelDificultadIA.Facil, 400, 1);

            Assert.GreaterOrEqual(pct, 65.0, $"Normal gano {pct:F1}% de las manos al Facil");
        }

        [Test]
        public void Dificil_GanaClaramenteAlFacil()
        {
            double pct = IATestHelper.PorcentajeVictorias(NivelDificultadIA.Dificil, NivelDificultadIA.Facil, 400, 2);

            Assert.GreaterOrEqual(pct, 65.0, $"Dificil gano {pct:F1}% de las manos al Facil");
        }

        [Test]
        public void Dificil_GanaAlNormal()
        {
            double pct = IATestHelper.PorcentajeVictorias(NivelDificultadIA.Dificil, NivelDificultadIA.Normal, 800, 3);

            Assert.GreaterOrEqual(pct, 53.0, $"Dificil gano {pct:F1}% de las manos al Normal");
        }

        [Test]
        public void Experto_GanaClaramenteAlFacil()
        {
            double pct = IATestHelper.PorcentajeVictorias(NivelDificultadIA.Experto, NivelDificultadIA.Facil, 40, 4);

            Assert.GreaterOrEqual(pct, 65.0, $"Experto gano {pct:F1}% de las manos al Facil");
        }

        [Test]
        public void Experto_GanaAlNormal()
        {
            double pct = IATestHelper.PorcentajeVictorias(NivelDificultadIA.Experto, NivelDificultadIA.Normal, 60, 5);

            Assert.GreaterOrEqual(pct, 52.0, $"Experto gano {pct:F1}% de las manos al Normal");
        }

        [Test]
        public void MismoNivel_NoFavoreceAUnAsiento()
        {
            // Control del banco de pruebas: dos parejas del mismo nivel deben repartirse las manos
            double pct = IATestHelper.PorcentajeVictorias(NivelDificultadIA.Normal, NivelDificultadIA.Normal, 800, 6);

            Assert.That(pct, Is.InRange(43.0, 57.0));
        }

        [TestCase(NivelDificultadIA.Facil)]
        [TestCase(NivelDificultadIA.Normal)]
        [TestCase(NivelDificultadIA.Dificil)]
        [TestCase(NivelDificultadIA.Experto)]
        public void Factory_CreaLaEstrategiaDelNivelPedido(NivelDificultadIA nivel)
        {
            IEstrategiaIA ia = IAFactory.Crear(nivel, new Random(1));

            switch (nivel)
            {
                case NivelDificultadIA.Facil:   Assert.IsInstanceOf<IAFacil>(ia); break;
                case NivelDificultadIA.Normal:  Assert.IsInstanceOf<IANormal>(ia); break;
                case NivelDificultadIA.Dificil: Assert.IsInstanceOf<IADificil>(ia); break;
                default:                        Assert.IsInstanceOf<IAExperto>(ia); break;
            }
        }
    }
}
