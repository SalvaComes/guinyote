using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GuinoteApp.UI
{
    /// <summary>
    /// Pantalla de reglas / tutorial.
    /// Layout: 2 columnas — izquierda con tabla de valores de cartas,
    /// derecha con explicación de cantes y arrastre.
    /// </summary>
    public class PantallaReglas : MonoBehaviour
    {
        // =================================================================
        // Referencias UI (asignar en Inspector)
        // =================================================================

        [Header("Navegación")]
        [SerializeField] private UIManager uiManager;

        [Header("Textos de cabecera")]
        [SerializeField] private TMP_Text textoTitulo;
        [SerializeField] private TMP_Text textoSubtitulo;

        [Header("Columna izquierda — Valores de cartas")]
        [SerializeField] private TMP_Text textoSeccionValores;
        [SerializeField] private TMP_Text[] filasValores;

        [Header("Columna derecha — Cantes")]
        [SerializeField] private TMP_Text textoSeccionCantes;
        [SerializeField] private TMP_Text textoLas20;
        [SerializeField] private TMP_Text textoLas40;
        [SerializeField] private TMP_Text textoArrastre;

        [Header("Acción")]
        [SerializeField] private Button botonVolver;

        // =================================================================
        // Datos de referencia para la tabla de valores
        // =================================================================

        private static readonly (string glyph, string nombre, int puntos)[] ValoresCartas =
        {
            ("A", "As",      11),
            ("3", "Tres",    10),
            ("R", "Rey",      4),
            ("S", "Sota",     3),
            ("C", "Caballo",  2),
            ("·", "7, 6, 5, 4, 2", 0)
        };

        // =================================================================
        // Inicialización
        // =================================================================

        private void Awake()
        {
            // Cabecera
            if (textoTitulo != null)
                textoTitulo.text = "Cómo se juega";
            if (textoSubtitulo != null)
                textoSubtitulo.text = "Dos parejas enfrentadas · gana el primer equipo en llegar a <b>101 puntos</b>";

            // Sección izquierda
            if (textoSeccionValores != null)
                textoSeccionValores.text = "VALOR DE LAS CARTAS";

            RellenarTablaValores();

            // Sección derecha
            if (textoSeccionCantes != null)
                textoSeccionCantes.text = "CANTES";
            if (textoLas20 != null)
                textoLas20.text = "<b>Las 20:</b> Rey y Sota del mismo palo (sin ser triunfo).";
            if (textoLas40 != null)
                textoLas40.text = "<b>Las 40:</b> Rey y Sota del palo de triunfo.";
            if (textoArrastre != null)
                textoArrastre.text = "<b>Arrastre:</b> al agotarse el mazo, es obligatorio asistir al palo y superar la baza si se puede.";

            // Botón volver
            if (botonVolver != null)
                botonVolver.onClick.AddListener(OnClickVolver);
        }

        // =================================================================
        // Tabla de valores
        // =================================================================

        private void RellenarTablaValores()
        {
            if (filasValores == null) return;

            for (int i = 0; i < filasValores.Length && i < ValoresCartas.Length; i++)
            {
                if (filasValores[i] != null)
                {
                    var (glyph, nombre, pts) = ValoresCartas[i];
                    filasValores[i].text = $"<b>{glyph}</b>   {nombre}   —   <color=#d6006c>{pts} pts</color>";
                }
            }
        }

        // =================================================================
        // Acciones
        // =================================================================

        private void OnClickVolver()
        {
            if (uiManager != null)
                uiManager.IrAInicio();
        }
    }
}
