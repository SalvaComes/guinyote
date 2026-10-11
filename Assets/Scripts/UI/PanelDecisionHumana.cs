using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GuinoteApp.UI
{
    /// <summary>
    /// Ventana modal sencilla que pregunta al jugador humano una decision con 2-3 opciones
    /// (cantar un palo, cambiar la pinta). Se construye por codigo, sin prefab ni escena,
    /// como hijo del panel del HUD.
    /// </summary>
    public class PanelDecisionHumana : MonoBehaviour
    {
        private const float Ancho = 480f;
        private const float Alto = 210f;

        /// <summary>Por encima de cualquier carta (las cartas usan ordenes de dibujado bajos, hasta unos cientos).</summary>
        private const int OrdenDeDibujado = 5000;

        private TMP_Text textoTitulo;
        private TMP_Text textoDetalle;
        private RectTransform filaBotones;
        private readonly List<GameObject> botonesActuales = new List<GameObject>();

        /// <summary>Crea el panel (oculto) como hijo de <paramref name="padre"/>.</summary>
        public static PanelDecisionHumana Crear(Transform padre)
        {
            var go = new GameObject("Panel_DecisionHumana", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(padre, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(Ancho, Alto);
            rt.anchoredPosition = Vector2.zero;

            go.GetComponent<Image>().color = DesignTokens.Neutral100;

            // Las cartas son sprites del mundo y el Canvas es Screen Space - Camera: sin un
            // Canvas propio con orden alto, la ventana quedaria detras de la pinta y el mazo.
            var lienzo = go.AddComponent<Canvas>();
            lienzo.overrideSorting = true;
            lienzo.sortingOrder = OrdenDeDibujado;
            go.AddComponent<GraphicRaycaster>();

            var panel = go.AddComponent<PanelDecisionHumana>();
            panel.Construir();
            go.SetActive(false);
            return panel;
        }

        private void Construir()
        {
            textoTitulo = CrearTexto("Titulo", 28f, FontStyles.Bold,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -22f), new Vector2(-32f, 44f));
            textoDetalle = CrearTexto("Detalle", 20f, FontStyles.Normal,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -78f), new Vector2(-32f, 56f));

            var fila = new GameObject("Botones", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            fila.transform.SetParent(transform, false);
            filaBotones = (RectTransform)fila.transform;
            filaBotones.anchorMin = new Vector2(0f, 0f);
            filaBotones.anchorMax = new Vector2(1f, 0f);
            filaBotones.pivot = new Vector2(0.5f, 0f);
            filaBotones.sizeDelta = new Vector2(-32f, 56f);
            filaBotones.anchoredPosition = new Vector2(0f, 18f);

            var layout = fila.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
        }

        private TMP_Text CrearTexto(
            string nombre, float tamano, FontStyles estilo,
            Vector2 anclaMin, Vector2 anclaMax, Vector2 posicion, Vector2 tamanoRect)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = anclaMin;
            rt.anchorMax = anclaMax;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = posicion;
            rt.sizeDelta = tamanoRect;

            var texto = go.GetComponent<TextMeshProUGUI>();
            texto.fontSize = tamano;
            texto.fontStyle = estilo;
            texto.color = DesignTokens.ColorText;
            texto.alignment = TextAlignmentOptions.Center;
            texto.textWrappingMode = TextWrappingModes.Normal;
            texto.raycastTarget = false;
            return texto;
        }

        /// <summary>Muestra la pregunta con las opciones indicadas. Cada opcion cierra el panel y ejecuta su accion.</summary>
        public void Mostrar(string titulo, string detalle, IList<(string texto, Action accion)> opciones)
        {
            foreach (GameObject b in botonesActuales)
                Destroy(b);
            botonesActuales.Clear();

            textoTitulo.text = titulo;
            textoDetalle.text = detalle;

            for (int i = 0; i < opciones.Count; i++)
            {
                Action accion = opciones[i].accion;
                bool principal = i == 0;
                botonesActuales.Add(CrearBoton(opciones[i].texto, principal, () =>
                {
                    Ocultar();
                    accion?.Invoke();
                }));
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Ocultar()
        {
            gameObject.SetActive(false);
        }

        private GameObject CrearBoton(string texto, bool principal, Action alPulsar)
        {
            var go = new GameObject("Btn_" + texto, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(filaBotones, false);

            var imagen = go.GetComponent<Image>();
            imagen.color = principal ? DesignTokens.ColorAccent : DesignTokens.Neutral300;

            go.GetComponent<Button>().onClick.AddListener(() => alPulsar());

            var hijo = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
            hijo.transform.SetParent(go.transform, false);
            var rt = (RectTransform)hijo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(6f, 4f);
            rt.offsetMax = new Vector2(-6f, -4f);

            var t = hijo.GetComponent<TextMeshProUGUI>();
            t.text = texto;
            t.fontSize = 18f;
            t.fontStyle = FontStyles.Bold;
            t.color = principal ? DesignTokens.ColorBg : DesignTokens.ColorText;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;

            return go;
        }
    }
}
