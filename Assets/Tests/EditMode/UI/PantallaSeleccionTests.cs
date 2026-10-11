using System.Reflection;
using GuinoteApp.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GuinoteApp.UI.Tests
{
    /// <summary>
    /// Pruebas del selector de nivel de IA por jugador de la pantalla de seleccion.
    /// En EditMode Unity no ejecuta Awake, asi que se invoca por reflexion.
    /// </summary>
    public class PantallaSeleccionTests
    {
        private GameObject raiz;
        private PantallaSeleccion pantalla;

        [SetUp]
        public void SetUp()
        {
            raiz = new GameObject("Panel_Seleccion", typeof(RectTransform));
            foreach (string nombre in new[] { "Card_Rival1", "Card_Companero", "Card_Rival2" })
            {
                var tarjeta = new GameObject(nombre, typeof(RectTransform));
                tarjeta.transform.SetParent(raiz.transform, false);
            }

            pantalla = raiz.AddComponent<PantallaSeleccion>();
            Invocar("Awake");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(raiz);
        }

        private void Invocar(string metodo)
        {
            typeof(PantallaSeleccion).GetMethod(metodo, BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(pantalla, null);
        }

        private Button[] Selectores() =>
            System.Array.FindAll(raiz.transform.GetComponentsInChildren<Button>(true), b => b.name == "Btn_Nivel");

        [Test]
        public void Awake_CreaUnSelectorDeNivelPorCadaJugadorIA()
        {
            Assert.AreEqual(3, Selectores().Length);
            foreach (Button b in Selectores())
                Assert.AreEqual("Btn_Nivel", b.name);
        }

        [Test]
        public void Awake_CreaElBotonDelCotoQueAlternaEntre3Y5Juegos()
        {
            int inicial = MenuConfiguracion.JuegosParaGanarPartida;
            try
            {
                Button boton = System.Array.Find(
                    raiz.transform.GetComponentsInChildren<Button>(true), b => b.name == "Btn_Coto");
                Assert.IsNotNull(boton);

                boton.onClick.Invoke();
                int tras1 = MenuConfiguracion.JuegosParaGanarPartida;
                boton.onClick.Invoke();
                int tras2 = MenuConfiguracion.JuegosParaGanarPartida;

                Assert.AreNotEqual(tras1, tras2);
                Assert.Contains(tras1, new[] { 3, 5 });
                Assert.AreEqual(inicial, tras2);
                StringAssert.Contains($"{tras2} juegos", boton.GetComponentInChildren<TMP_Text>().text);
            }
            finally
            {
                MenuConfiguracion.Establecer(inicial);
            }
        }

        [Test]
        public void PorDefecto_TodosEnNormal()
        {
            CollectionAssert.AreEqual(
                new[] { DificultadIA.Normal, DificultadIA.Normal, DificultadIA.Normal }, pantalla.NivelesJugador);
        }

        [Test]
        public void PulsarElSelectorDeUnJugador_SoloCambiaElNivelDeEseJugador()
        {
            // Normal -> Difícil en el compañero (tarjeta 2)
            Selectores()[1].onClick.Invoke();

            CollectionAssert.AreEqual(
                new[] { DificultadIA.Normal, DificultadIA.Dificil, DificultadIA.Normal }, pantalla.NivelesJugador);
            Assert.AreEqual("◀  Difícil  ▶", Selectores()[1].GetComponentInChildren<TMP_Text>().text);
            Assert.AreEqual("◀  Normal  ▶", Selectores()[0].GetComponentInChildren<TMP_Text>().text);
        }

        [Test]
        public void ElNivelDaLaVueltaDespuesDeExperto()
        {
            pantalla.AsignarNivel(0, DificultadIA.Experto);

            pantalla.CambiarNivelSiguiente(0);

            Assert.AreEqual(DificultadIA.Facil, pantalla.NivelesJugador[0]);
        }

        [Test]
        public void AsignarNivelATodos_CambiaALosTres()
        {
            pantalla.AsignarNivelATodos(DificultadIA.Experto);

            CollectionAssert.AreEqual(
                new[] { DificultadIA.Experto, DificultadIA.Experto, DificultadIA.Experto }, pantalla.NivelesJugador);
        }

        [Test]
        public void Comenzar_EntregaLosTresNivelesEnOrdenJ2J3J4()
        {
            pantalla.AsignarNivel(0, DificultadIA.Facil);
            pantalla.AsignarNivel(1, DificultadIA.Experto);
            pantalla.AsignarNivel(2, DificultadIA.Dificil);
            DificultadIA[] recibidos = null;
            pantalla.OnComenzarPartida += n => recibidos = n;

            Invocar("OnClickComenzar");

            CollectionAssert.AreEqual(
                new[] { DificultadIA.Facil, DificultadIA.Experto, DificultadIA.Dificil }, recibidos);
        }

        [Test]
        public void AsignarNivel_IndiceFueraDeRango_NoHaceNada()
        {
            pantalla.AsignarNivel(7, DificultadIA.Experto);
            pantalla.AsignarNivel(-1, DificultadIA.Experto);

            CollectionAssert.AreEqual(
                new[] { DificultadIA.Normal, DificultadIA.Normal, DificultadIA.Normal }, pantalla.NivelesJugador);
        }
    }
}
