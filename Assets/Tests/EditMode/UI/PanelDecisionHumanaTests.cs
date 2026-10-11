using System;
using System.Collections.Generic;
using GuinoteApp.Core;
using GuinoteApp.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GuinoteApp.UI.Tests
{
    /// <summary>
    /// Pruebas de humo de la ventana con la que el humano decide cantar o cambiar la pinta.
    /// Comprueban que se construye sin escena ni prefab y que cada boton llama a su respuesta.
    /// </summary>
    public class PanelDecisionHumanaTests
    {
        private GameObject canvas;

        [SetUp]
        public void SetUp()
        {
            canvas = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(canvas);
        }

        private static string[] Textos(Button[] botones)
        {
            var res = new string[botones.Length];
            for (int i = 0; i < botones.Length; i++)
                res[i] = botones[i].GetComponentInChildren<TMP_Text>().text;
            return res;
        }

        [Test]
        public void Crear_PanelOcultoHijoDelPadre()
        {
            PanelDecisionHumana panel = PanelDecisionHumana.Crear(canvas.transform);

            Assert.AreSame(canvas.transform, panel.transform.parent);
            Assert.IsFalse(panel.gameObject.activeSelf);
        }

        [Test]
        public void Mostrar_CreaUnBotonPorOpcionYEjecutaLaAccionAlPulsar()
        {
            PanelDecisionHumana panel = PanelDecisionHumana.Crear(canvas.transform);
            string pulsado = null;

            panel.Mostrar("Titulo", "Detalle", new List<(string, Action)>
            {
                ("A", () => pulsado = "A"),
                ("B", () => pulsado = "B"),
            });

            Button[] botones = panel.GetComponentsInChildren<Button>(true);
            Assert.AreEqual(2, botones.Length);
            Assert.IsTrue(panel.gameObject.activeSelf);

            botones[1].onClick.Invoke();

            Assert.AreEqual("B", pulsado);
            Assert.IsFalse(panel.gameObject.activeSelf, "Pulsar una opcion cierra la ventana");
        }

        [Test]
        public void HUD_PedirCante_OfreceUnBotonPorPaloMasNoCantar()
        {
            var hud = new GameObject("HUD", typeof(RectTransform)).AddComponent<HUDMesaDeJuego>();
            hud.transform.SetParent(canvas.transform, false);
            Palo? respuesta = Palo.Copas; // valor centinela distinto de lo esperado

            hud.PedirCante(new List<Palo> { Palo.Oros, Palo.Bastos }, Palo.Bastos, p => respuesta = p);

            Button[] botones = hud.GetComponentsInChildren<Button>(true);
            CollectionAssert.AreEqual(
                new[] { "Las 20 en oros", "Las 40 en bastos", "No cantar" }, Textos(botones));

            botones[1].onClick.Invoke();
            Assert.AreEqual(Palo.Bastos, respuesta);
        }

        [Test]
        public void HUD_PedirCante_NoCantarDevuelveNull()
        {
            var hud = new GameObject("HUD", typeof(RectTransform)).AddComponent<HUDMesaDeJuego>();
            hud.transform.SetParent(canvas.transform, false);
            Palo? respuesta = Palo.Copas;

            hud.PedirCante(new List<Palo> { Palo.Oros }, Palo.Bastos, p => respuesta = p);
            hud.GetComponentsInChildren<Button>(true)[1].onClick.Invoke();

            Assert.IsNull(respuesta);
        }

        [Test]
        public void HUD_PedirCambio_OfreceCambiarOAhoraNo()
        {
            var hud = new GameObject("HUD", typeof(RectTransform)).AddComponent<HUDMesaDeJuego>();
            hud.transform.SetParent(canvas.transform, false);
            bool? respuesta = null;

            hud.PedirCambio(new Carta(Palo.Bastos, Rango.Siete), new Carta(Palo.Bastos, Rango.As), c => respuesta = c);

            Button[] botones = hud.GetComponentsInChildren<Button>(true);
            CollectionAssert.AreEqual(new[] { "Cambiar", "Ahora no" }, Textos(botones));

            botones[0].onClick.Invoke();
            Assert.AreEqual(true, respuesta);
        }
    }
}
