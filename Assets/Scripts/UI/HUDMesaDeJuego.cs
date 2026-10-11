using System;
using System.Collections.Generic;
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

        // Ventana de decisiones del humano (cantes y cambio de la pinta); se crea al usarla
        private PanelDecisionHumana panelDecision;

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
            string nombrePalo = palo.ToString().ToLower();
            MostrarAviso($"¡Las {puntos} en {nombrePalo}!");
        }

        /// <summary>
        /// Pregunta al jugador si canta y en que palo. <paramref name="respuesta"/> recibe el
        /// palo elegido, o null si decide no cantar.
        /// </summary>
        public void PedirCante(IReadOnlyList<Palo> palos, Palo triunfo, Action<Palo?> respuesta)
        {
            var opciones = new List<(string, Action)>();
            foreach (Palo palo in palos)
            {
                Palo elegido = palo;
                int puntos = ReglasEspeciales.PuntosCante(palo, triunfo);
                opciones.Add(($"Las {puntos} en {palo.ToString().ToLower()}", () => respuesta?.Invoke(elegido)));
            }
            opciones.Add(("No cantar", () => respuesta?.Invoke(null)));

            ObtenerPanelDecision().Mostrar(
                "¿Quieres cantar?",
                "Tienes el Rey y la Sota de un mismo palo.",
                opciones);
        }

        /// <summary>
        /// Pregunta al jugador si cambia su 7 (o 2) de triunfo por la pinta visible.
        /// </summary>
        public void PedirCambio(Carta cartaPropia, Carta pinta, Action<bool> respuesta)
        {
            ObtenerPanelDecision().Mostrar(
                "¿Cambias la pinta?",
                $"Entregas el {cartaPropia} y recibes el {pinta}.",
                new List<(string, Action)>
                {
                    ("Cambiar", () => respuesta?.Invoke(true)),
                    ("Ahora no", () => respuesta?.Invoke(false))
                });
        }

        private PanelDecisionHumana ObtenerPanelDecision()
        {
            if (panelDecision == null)
                panelDecision = PanelDecisionHumana.Crear(transform);
            return panelDecision;
        }

        /// <summary>
        /// Marcador del Coto: puntos de cada equipo en Malas/Buenas (por ejemplo "15 Buenas") y
        /// Juegos ganados, que se anaden a la etiqueta del equipo.
        /// </summary>
        public void ActualizarMarcadorCoto(string puntosNosotros, string puntosEllos, int juegosNosotros, int juegosEllos)
        {
            if (textoNosotros != null)
                textoNosotros.text = $"Nosotros · {juegosNosotros}";
            if (textoEllos != null)
                textoEllos.text = $"Ellos · {juegosEllos}";
            if (textoPuntosNosotros != null)
                textoPuntosNosotros.text = puntosNosotros;
            if (textoPuntosEllos != null)
                textoPuntosEllos.text = puntosEllos;
        }

        /// <summary>Ventana con un unico boton para pasar a la siguiente mano o al siguiente Juego.</summary>
        public void PedirContinuar(string titulo, string detalle, string textoBoton, Action alContinuar)
        {
            ObtenerPanelDecision().Mostrar(titulo, detalle,
                new List<(string, Action)> { (textoBoton, () => alContinuar?.Invoke()) });
        }

        /// <summary>Pantalla final del Coto con el resultado y los Juegos de cada equipo.</summary>
        public void MostrarFinCoto(bool ganamos, int juegosNosotros, int juegosEllos)
        {
            if (panelFinPartida == null) return;

            panelFinPartida.SetActive(true);
            if (textoResultado != null)
                textoResultado.text = ganamos ? "¡Victoria!" : "Derrota";
            if (textoPuntosFinales != null)
                textoPuntosFinales.text = $"Juegos: Nosotros {juegosNosotros} — Ellos {juegosEllos}";
        }

        /// <summary>Muestra un mensaje breve (3 s) en la etiqueta central de la barra superior:
        /// cantes, sorteo de salida, ultima ronda de robo, arrastre...</summary>
        public void MostrarAviso(string texto)
        {
            if (tagCanteContainer == null || textoCanteActivo == null) return;

            tagCanteContainer.SetActive(true);
            textoCanteActivo.text = texto;

            CancelInvoke(nameof(OcultarCante));
            Invoke(nameof(OcultarCante), 3f);
        }

        /// <summary>Actualiza el contador de cartas restantes en el mazo.
        /// ultimaRonda: el proximo robo es el ultimo (la pinta incluida).</summary>
        public void ActualizarMazo(int cartasRestantes, bool ultimaRonda = false)
        {
            if (textoMazo == null) return;

            if (cartasRestantes <= 0)
                textoMazo.text = "Arrastre";
            else if (ultimaRonda)
                textoMazo.text = $"Últimas: {cartasRestantes}+pinta";
            else
                textoMazo.text = $"Mazo: {cartasRestantes}";
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
            if (panelDecision != null)
                panelDecision.Ocultar();
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
