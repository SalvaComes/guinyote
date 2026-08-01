using System.Collections;
using UnityEngine;
using GuinoteApp.Core;

namespace GuinoteApp.View
{
    /// <summary>
    /// Vista de una carta individual. Se adjunta al Prefab de carta.
    /// Controla la representacion visual (cara/dorso) y las animaciones.
    /// No contiene logica de reglas: eso pertenece al Core.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class CardView : MonoBehaviour
    {
        // --- Datos ---
        private Carta cartaData;
        public Carta CartaData => cartaData;

        // --- Referencias de componentes ---
        private SpriteRenderer spriteRenderer;
        private BoxCollider2D col2D;

        [Header("Sprites")]
        [SerializeField] private Sprite spriteCara;
        [SerializeField] private Sprite spriteDorso;

        [Header("Estado")]
        [SerializeField] private bool mostrandoCara = true;
        [SerializeField] private bool seleccionada = false;
        [SerializeField] private bool interactuable = true;

        [Header("Animacion")]
        [SerializeField] private float alturaSeleccion = 0.4f;
        [SerializeField] private float velocidadAnimacion = 10f;
        [SerializeField] private float escalaHover = 1.08f;

        private Vector3 posicionBase;
        private Vector3 posicionObjetivo;
        private float rotacionBase = 0f;

        // Evento: notifica al gestor cuando el jugador hace click
        public System.Action<CardView> OnCartaSeleccionada;

        // =====================================================================
        // Inicializacion
        // =====================================================================

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            col2D = GetComponent<BoxCollider2D>();
        }

        /// <summary>
        /// Inicializa la carta con sus datos de logica y sus sprites.
        /// Llamar siempre despues de instanciar el prefab.
        /// </summary>
        public void Inicializar(Carta carta, Sprite cara, Sprite dorso)
        {
            cartaData = carta;
            spriteCara = cara;
            spriteDorso = dorso;
            posicionBase = transform.position;
            posicionObjetivo = posicionBase;
            MostrarCara();
        }

        // =====================================================================
        // API Publica
        // =====================================================================

        public void MostrarCara()
        {
            mostrandoCara = true;
            spriteRenderer.sprite = spriteCara;
        }

        public void MostrarDorso()
        {
            mostrandoCara = false;
            spriteRenderer.sprite = spriteDorso;
        }

        /// <summary>
        /// Marca/desmarca la carta como seleccionada (la eleva visualmente).
        /// </summary>
        public void Seleccionar(bool valor)
        {
            seleccionada = valor;
            ActualizarPosicionObjetivo();
        }

        /// <summary>
        /// Permite o bloquea la interaccion del jugador con esta carta.
        /// </summary>
        public void SetInteractuable(bool valor)
        {
            interactuable = valor;
            col2D.enabled = valor;
        }

        /// <summary>
        /// Mueve la carta suavemente a una nueva posicion base.
        /// </summary>
        public void MoverA(Vector3 nuevaPosicion, int orderInLayer = 0)
        {
            posicionBase = nuevaPosicion;
            spriteRenderer.sortingOrder = orderInLayer;
            ActualizarPosicionObjetivo();
        }

        /// <summary>
        /// Establece la rotacion Z de la carta en grados (para el abanico de la mano).
        /// </summary>
        public void SetRotacion(float anguloZ)
        {
            rotacionBase = anguloZ;
            transform.rotation = Quaternion.Euler(0f, 0f, rotacionBase);
        }

        /// <summary>
        /// Establece la escala de la carta (para diferentes tamaños según la zona de mesa).
        /// </summary>
        public void SetEscala(Vector3 escala)
        {
            transform.localScale = escala;
        }

        // =====================================================================
        // Input (Mouse y Touch)
        // =====================================================================

        private void OnMouseEnter()
        {
            if (!interactuable || seleccionada) return;
            transform.localScale = Vector3.one * escalaHover;
            ActualizarPosicionObjetivo();
        }

        private void OnMouseExit()
        {
            if (!interactuable) return;
            transform.localScale = Vector3.one;
            ActualizarPosicionObjetivo();
        }

        private void OnMouseDown()
        {
            if (!interactuable) return;
            Seleccionar(!seleccionada);
            OnCartaSeleccionada?.Invoke(this);
        }

        // =====================================================================
        // Animacion
        // =====================================================================

        private void ActualizarPosicionObjetivo()
        {
            float elevacion = seleccionada ? alturaSeleccion : 0f;
            posicionObjetivo = posicionBase + Vector3.up * elevacion;
        }

        private void Update()
        {
            if (Vector3.Distance(transform.position, posicionObjetivo) > 0.001f)
            {
                transform.position = Vector3.Lerp(
                    transform.position,
                    posicionObjetivo,
                    Time.deltaTime * velocidadAnimacion
                );
            }
        }

        public override string ToString() =>
            cartaData != null ? $"CardView[{cartaData}]" : "CardView[sin inicializar]";
    }
}
