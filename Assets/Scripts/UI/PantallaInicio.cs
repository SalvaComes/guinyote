using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GuinoteApp.UI
{
    /// <summary>
    /// Pantalla de inicio / bienvenida.
    /// Layout según diseño: header con logo, título grande "El juego de cartas de siempre",
    /// botones de acción y abanico de 3 cartas decorativas a la derecha.
    /// </summary>
    public class PantallaInicio : MonoBehaviour
    {
        // =================================================================
        // Referencias UI (asignar en Inspector)
        // =================================================================

        [Header("Navegación")]
        [SerializeField] private UIManager uiManager;

        [Header("Textos")]
        [SerializeField] private TMP_Text textoLogo;
        [SerializeField] private TMP_Text textoEdicion;
        [SerializeField] private TMP_Text textoJugador;
        [SerializeField] private TMP_Text textoTitulo;

        [Header("Botones")]
        [SerializeField] private Button botonJugarIA;
        [SerializeField] private Button botonMultijugador;
        [SerializeField] private Button botonComoSeJuega;

        [Header("Cartas decorativas (3 Images en abanico)")]
        [SerializeField] private Image cartaHero1;
        [SerializeField] private Image cartaHero2;
        [SerializeField] private Image cartaHero3;

        [Header("Configuración")]
        [SerializeField] private string nombreJugador = "Jugador";

        // =================================================================
        // Inicialización
        // =================================================================

        private void Awake()
        {
            // Textos fijos
            if (textoLogo != null)
                textoLogo.text = "GUIÑOTE";

            if (textoEdicion != null)
                textoEdicion.text = "Edición familiar";

            if (textoJugador != null)
                textoJugador.text = $"Jugador: {nombreJugador} · Baraja española";

            if (textoTitulo != null)
                textoTitulo.text = "El juego de\ncartas de\nsiempre";

            // Botones
            if (botonJugarIA != null)
                botonJugarIA.onClick.AddListener(OnJugarIA);

            if (botonMultijugador != null)
            {
                botonMultijugador.onClick.AddListener(OnMultijugador);
                botonMultijugador.interactable = false; // Placeholder: no implementado
            }

            if (botonComoSeJuega != null)
                botonComoSeJuega.onClick.AddListener(OnComoSeJuega);

            // Cartas en abanico: rotaciones del diseño (-8°, 2°, 11°)
            ConfigurarCartaHero(cartaHero1, -8f);
            ConfigurarCartaHero(cartaHero2,  2f);
            ConfigurarCartaHero(cartaHero3, 11f);
        }

        // =================================================================
        // Acciones de botón
        // =================================================================

        private void OnJugarIA()
        {
            Debug.Log("[PantallaInicio] Jugar con la IA");
            if (uiManager != null)
                uiManager.IrASeleccion();
        }

        private void OnMultijugador()
        {
            Debug.Log("[PantallaInicio] Multijugador (no implementado)");
        }

        private void OnComoSeJuega()
        {
            Debug.Log("[PantallaInicio] Cómo se juega");
            if (uiManager != null)
                uiManager.IrAReglas();
        }

        // =================================================================
        // Utilidades
        // =================================================================

        private void ConfigurarCartaHero(Image carta, float rotacionZ)
        {
            if (carta == null) return;
            carta.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotacionZ);
        }
    }
}
