using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GuinoteApp.UI
{
    /// <summary>
    /// Pantalla de selección de mesa: modo de juego, disposición de jugadores,
    /// dificultad de IA y botón de comenzar partida.
    /// Layout: grid 3×2 con 4 posiciones de jugador + selector de dificultad.
    /// </summary>
    public class PantallaSeleccion : MonoBehaviour
    {
        // =================================================================
        // Referencias UI (asignar en Inspector)
        // =================================================================

        [Header("Navegación")]
        [SerializeField] private UIManager uiManager;

        [Header("Textos")]
        [SerializeField] private TMP_Text textoTitulo;

        [Header("Modo de juego (Segmented Control)")]
        [SerializeField] private Button botonContraIA;
        [SerializeField] private Button botonMultijugador;
        [SerializeField] private Image  fondoContraIA;
        [SerializeField] private Image  fondoMultijugador;

        [Header("Avatares de jugadores")]
        [SerializeField] private TMP_Text textoRival1;
        [SerializeField] private TMP_Text textoCompanero;
        [SerializeField] private TMP_Text textoRival2;
        [SerializeField] private TMP_Text textoTu;

        [Header("Dificultad (Segmented Control)")]
        [SerializeField] private Button botonFacil;
        [SerializeField] private Button botonNormal;
        [SerializeField] private Button botonDificil;
        [SerializeField] private Image  fondoFacil;
        [SerializeField] private Image  fondoNormal;
        [SerializeField] private Image  fondoDificil;

        [Header("Acción")]
        [SerializeField] private Button botonComenzar;
        [SerializeField] private Button botonVolver;

        // =================================================================
        // Estado
        // =================================================================

        private DificultadIA dificultadSeleccionada = DificultadIA.Normal;
        private bool modoContraIA = true;

        /// <summary>Dificultad elegida por el jugador.</summary>
        public DificultadIA DificultadSeleccionada => dificultadSeleccionada;

        // Evento para cuando se inicia la partida
        public event System.Action<DificultadIA> OnComenzarPartida;

        // =================================================================
        // Inicialización
        // =================================================================

        private void Awake()
        {
            if (textoTitulo != null)
                textoTitulo.text = "Elige tu mesa";

            // Textos de jugadores
            if (textoRival1 != null) textoRival1.text = "Rival · Normal";
            if (textoCompanero != null) textoCompanero.text = "Compañero IA";
            if (textoRival2 != null) textoRival2.text = "Rival · Normal";
            if (textoTu != null) textoTu.text = "Tú";

            // Modo de juego
            if (botonContraIA != null)
                botonContraIA.onClick.AddListener(() => SeleccionarModo(true));
            if (botonMultijugador != null)
            {
                botonMultijugador.onClick.AddListener(() => SeleccionarModo(false));
                botonMultijugador.interactable = false;
            }

            // Dificultad
            if (botonFacil != null)
                botonFacil.onClick.AddListener(() => SeleccionarDificultad(DificultadIA.Facil));
            if (botonNormal != null)
                botonNormal.onClick.AddListener(() => SeleccionarDificultad(DificultadIA.Normal));
            if (botonDificil != null)
                botonDificil.onClick.AddListener(() => SeleccionarDificultad(DificultadIA.Dificil));

            // Acciones
            if (botonComenzar != null)
                botonComenzar.onClick.AddListener(OnClickComenzar);
            if (botonVolver != null)
                botonVolver.onClick.AddListener(OnClickVolver);

            // Estado visual inicial
            SeleccionarModo(true);
            SeleccionarDificultad(DificultadIA.Normal);
        }

        // =================================================================
        // Selección de modo
        // =================================================================

        private void SeleccionarModo(bool contraIA)
        {
            modoContraIA = contraIA;
            ActualizarSegmentedControl(fondoContraIA, fondoMultijugador, contraIA);
        }

        // =================================================================
        // Selección de dificultad
        // =================================================================

        private void SeleccionarDificultad(DificultadIA dificultad)
        {
            dificultadSeleccionada = dificultad;

            // Visual: destacar la opción seleccionada
            bool esFacil   = dificultad == DificultadIA.Facil;
            bool esNormal  = dificultad == DificultadIA.Normal;
            bool esDificil = dificultad == DificultadIA.Dificil;

            if (fondoFacil != null)
                fondoFacil.color = esFacil ? DesignTokens.ColorAccent : Color.clear;
            if (fondoNormal != null)
                fondoNormal.color = esNormal ? DesignTokens.ColorAccent : Color.clear;
            if (fondoDificil != null)
                fondoDificil.color = esDificil ? DesignTokens.ColorAccent : Color.clear;

            // Actualizar textos de rivales
            string nivel = dificultad.ToString();
            if (textoRival1 != null) textoRival1.text = $"Rival · {nivel}";
            if (textoRival2 != null) textoRival2.text = $"Rival · {nivel}";

            Debug.Log($"[PantallaSeleccion] Dificultad: {dificultad}");
        }

        // =================================================================
        // Acciones
        // =================================================================

        private void OnClickComenzar()
        {
            Debug.Log($"[PantallaSeleccion] Comenzar partida — Dificultad: {dificultadSeleccionada}");
            OnComenzarPartida?.Invoke(dificultadSeleccionada);

            if (uiManager != null)
                uiManager.IrAMesaDeJuego();
        }

        private void OnClickVolver()
        {
            if (uiManager != null)
                uiManager.IrAInicio();
        }

        // =================================================================
        // Utilidades de UI
        // =================================================================

        private void ActualizarSegmentedControl(Image fondoA, Image fondoB, bool aSeleccionado)
        {
            if (fondoA != null)
                fondoA.color = aSeleccionado ? DesignTokens.ColorAccent : Color.clear;
            if (fondoB != null)
                fondoB.color = !aSeleccionado ? DesignTokens.ColorAccent : Color.clear;
        }
    }

    // =====================================================================
    // Enum de dificultad
    // =====================================================================

    public enum DificultadIA
    {
        Facil,
        Normal,
        Dificil
    }
}
