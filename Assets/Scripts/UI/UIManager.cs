using UnityEngine;

namespace GuinoteApp.UI
{
    /// <summary>
    /// Gestiona la navegación entre las pantallas del juego.
    /// Todas las pantallas son paneles hijos de un único Canvas.
    /// La navegación se realiza activando/desactivando paneles con transiciones de fade.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        // =================================================================
        // Paneles de pantalla (asignar en Inspector)
        // =================================================================

        [Header("Paneles de pantalla")]
        [SerializeField] private GameObject panelInicio;
        [SerializeField] private GameObject panelSeleccion;
        [SerializeField] private GameObject panelReglas;
        [SerializeField] private GameObject panelMesaDeJuego;

        [Header("Configuración")]
        [SerializeField] private float duracionTransicion = 0.25f;

        // =================================================================
        // Estado
        // =================================================================

        private GameObject panelActual;
        private CanvasGroup canvasGroupActual;

        /// <summary>Pantalla que se está mostrando actualmente.</summary>
        public PantallaID PantallaActual { get; private set; }

        // =================================================================
        // Ciclo de vida Unity
        // =================================================================

        private void Awake()
        {
            // Asegurar que todos los paneles tienen CanvasGroup para transiciones
            AsegurarCanvasGroup(panelInicio);
            AsegurarCanvasGroup(panelSeleccion);
            AsegurarCanvasGroup(panelReglas);
            AsegurarCanvasGroup(panelMesaDeJuego);
        }

        private void Start()
        {
            // Ocultar todos y mostrar el inicio
            OcultarTodos();
            MostrarPantalla(PantallaID.Inicio);
        }

        // =================================================================
        // API pública de navegación
        // =================================================================

        /// <summary>
        /// Navega a la pantalla indicada con transición de fade.
        /// </summary>
        public void MostrarPantalla(PantallaID pantalla)
        {
            GameObject nuevoPanelGO = ObtenerPanel(pantalla);
            if (nuevoPanelGO == null)
            {
                Debug.LogWarning($"[UIManager] Panel no asignado para pantalla: {pantalla}");
                return;
            }

            // Ocultar panel actual si hay uno
            if (panelActual != null && panelActual != nuevoPanelGO)
            {
                CanvasGroup cgAnterior = panelActual.GetComponent<CanvasGroup>();
                if (cgAnterior != null)
                {
                    cgAnterior.alpha = 0f;
                    cgAnterior.interactable = false;
                    cgAnterior.blocksRaycasts = false;
                }
                panelActual.SetActive(false);
            }

            // Mostrar nuevo panel
            nuevoPanelGO.SetActive(true);
            CanvasGroup cg = nuevoPanelGO.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
                StartCoroutine(FadeIn(cg));
            }

            panelActual = nuevoPanelGO;
            PantallaActual = pantalla;
            Debug.Log($"[UIManager] Pantalla activa: {pantalla}");
        }

        /// <summary>Accesos directos para navegación desde botones (UnityEvent).</summary>
        public void IrAInicio()     => MostrarPantalla(PantallaID.Inicio);
        public void IrASeleccion()  => MostrarPantalla(PantallaID.Seleccion);
        public void IrAReglas()     => MostrarPantalla(PantallaID.Reglas);
        public void IrAMesaDeJuego() => MostrarPantalla(PantallaID.MesaDeJuego);

        // =================================================================
        // Transiciones
        // =================================================================

        private System.Collections.IEnumerator FadeIn(CanvasGroup cg)
        {
            float tiempo = 0f;
            while (tiempo < duracionTransicion)
            {
                tiempo += Time.deltaTime;
                cg.alpha = Mathf.Clamp01(tiempo / duracionTransicion);
                yield return null;
            }
            cg.alpha = 1f;
        }

        // =================================================================
        // Utilidades internas
        // =================================================================

        private void OcultarTodos()
        {
            DesactivarPanel(panelInicio);
            DesactivarPanel(panelSeleccion);
            DesactivarPanel(panelReglas);
            DesactivarPanel(panelMesaDeJuego);
        }

        private void DesactivarPanel(GameObject panel)
        {
            if (panel == null) return;
            panel.SetActive(false);
            CanvasGroup cg = panel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }
        }

        private GameObject ObtenerPanel(PantallaID id)
        {
            switch (id)
            {
                case PantallaID.Inicio:       return panelInicio;
                case PantallaID.Seleccion:    return panelSeleccion;
                case PantallaID.Reglas:        return panelReglas;
                case PantallaID.MesaDeJuego:  return panelMesaDeJuego;
                default:                       return null;
            }
        }

        private void AsegurarCanvasGroup(GameObject panel)
        {
            if (panel == null) return;
            if (panel.GetComponent<CanvasGroup>() == null)
                panel.AddComponent<CanvasGroup>();
        }
    }

    // =====================================================================
    // Enum de pantallas
    // =====================================================================

    public enum PantallaID
    {
        Inicio,
        Seleccion,
        Reglas,
        MesaDeJuego
    }
}
