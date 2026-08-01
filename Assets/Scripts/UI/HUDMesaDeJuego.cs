using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GuinoteApp.Core;

namespace GuinoteApp.UI
{
    /// <summary>
    /// HUD superpuesto durante la partida en la mesa de juego.
    /// Muestra: marcador en vivo (Nosotros/Ellos), cante activo,
    /// cartas restantes en mazo, y fase actual.
    /// Se suscribe a eventos del TurnManager vía GameManager.
    /// </summary>
    public class HUDMesaDeJuego : MonoBehaviour
    {
        // =================================================================
        // Referencias UI (asignar en Inspector)
        // =================================================================

        [Header("Marcador")]
        [SerializeField] private TMP_Text textoNosotros;
        [SerializeField] private TMP_Text textoEllos;
        [SerializeField] private TMP_Text textoPuntosNosotros;
        [SerializeField] private TMP_Text textoPuntosEllos;

        [Header("Tags de estado")]
        [SerializeField] private GameObject tagCanteContainer;
        [SerializeField] private TMP_Text textoCanteActivo;
        [SerializeField] private TMP_Text textoMazo;

        [Header("Fin de partida")]
        [SerializeField] private GameObject panelFinPartida;
        [SerializeField] private TMP_Text textoResultado;
        [SerializeField] private TMP_Text textoPuntosFinales;
        [SerializeField] private Button botonVolverAlMenu;

        [Header("Navegación")]
        [SerializeField] private UIManager uiManager;

        // =================================================================
        // Estado
        // =================================================================

        private int puntosA = 0;
        private int puntosB = 0;

        // =================================================================
        // Inicialización
        // =================================================================

        private void Awake()
        {
            // Textos fijos
            if (textoNosotros != null)
                textoNosotros.text = "Nosotros";
            if (textoEllos != null)
                textoEllos.text = "Ellos";

            // Ocultar cante y fin de partida inicialmente
            if (tagCanteContainer != null)
                tagCanteContainer.SetActive(false);
            if (panelFinPartida != null)
                panelFinPartida.SetActive(false);

            // Botón volver
            if (botonVolverAlMenu != null)
                botonVolverAlMenu.onClick.AddListener(OnVolverAlMenu);

            ActualizarMarcador(0, 0);
            ActualizarMazo(15); // 40 - 24 (repartidas) - 1 (triunfo)
        }

        // =================================================================
        // API pública — llamar desde GameManager al recibir eventos
        // =================================================================

        /// <summary>Actualiza el marcador de ambas parejas.</summary>
        public void ActualizarMarcador(int ptsNosotros, int ptsEllos)
        {
            puntosA = ptsNosotros;
            puntosB = ptsEllos;

            if (textoPuntosNosotros != null)
                textoPuntosNosotros.text = ptsNosotros.ToString();
            if (textoPuntosEllos != null)
                textoPuntosEllos.text = ptsEllos.ToString();
        }

        /// <summary>Muestra el tag de cante activo (ej: "¡Las 40 en bastos!").</summary>
        public void MostrarCante(Palo palo, int puntos)
        {
            if (tagCanteContainer == null || textoCanteActivo == null) return;

            tagCanteContainer.SetActive(true);
            string nombrePalo = palo.ToString().ToLower();
            textoCanteActivo.text = $"¡Las {puntos} en {nombrePalo}!";

            // Ocultar el cante después de 3 segundos
            CancelInvoke(nameof(OcultarCante));
            Invoke(nameof(OcultarCante), 3f);
        }

        /// <summary>Actualiza el contador de cartas restantes en el mazo.</summary>
        public void ActualizarMazo(int cartasRestantes)
        {
            if (textoMazo != null)
            {
                if (cartasRestantes > 0)
                    textoMazo.text = $"Mazo: {cartasRestantes}";
                else
                    textoMazo.text = "Arrastre";
            }
        }

        /// <summary>Muestra la pantalla de fin de partida con el resultado.</summary>
        public void MostrarFinPartida(int ptsA, int ptsB)
        {
            if (panelFinPartida == null) return;

            panelFinPartida.SetActive(true);

            string resultado;
            if (ptsA > ptsB)
                resultado = "¡Victoria!";
            else if (ptsB > ptsA)
                resultado = "Derrota";
            else
                resultado = "Empate";

            if (textoResultado != null)
                textoResultado.text = resultado;
            if (textoPuntosFinales != null)
                textoPuntosFinales.text = $"Nosotros {ptsA} — Ellos {ptsB}";
        }

        /// <summary>Resetea el HUD para una nueva partida.</summary>
        public void Resetear()
        {
            ActualizarMarcador(0, 0);
            ActualizarMazo(15);
            OcultarCante();
            if (panelFinPartida != null)
                panelFinPartida.SetActive(false);
        }

        // =================================================================
        // Acciones internas
        // =================================================================

        private void OcultarCante()
        {
            if (tagCanteContainer != null)
                tagCanteContainer.SetActive(false);
        }

        private void OnVolverAlMenu()
        {
            if (uiManager != null)
                uiManager.IrAInicio();
        }
    }
}
