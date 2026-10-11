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
        public bool MostrandoCara => mostrandoCara;
        [SerializeField] private bool seleccionada = false;
        [SerializeField] private bool interactuable = true;

        [Header("Animacion")]
        [SerializeField] private float alturaSeleccion = 0.4f;
        [SerializeField] private float velocidadAnimacion = 10f;
        [SerializeField] private float escalaHover = 1.08f;

        private Vector3 posicionBase;
        private Vector3 posicionObjetivo;
        private float rotacionBase = 0f;

        // Tamaño en unidades de mundo que ocupa la carta, independiente de los pixeles/PPU
        // del sprite (caras y dorso se importan con tamaños distintos). La escala real se
        // recalcula al cambiar de sprite; el hover solo la multiplica.
        private Vector2 tamanoMundo = new Vector2(1.03f, 1.47f);
        private Vector3 escalaBase = Vector3.one;

        [Header("Arrastre")]
        [SerializeField] private float umbralArrastreMundo = 0.12f;

        [SerializeField] private float escalaArrastre = 1.12f;
        [SerializeField] private float seguimientoArrastre = 22f;

        private bool arrastrando = false;
        private bool rotacionSuave = false;
        private Vector3 posicionCursorArrastre;
        private Vector3 inicioArrastreMundo;
        private int ordenDibujadoBase = 0;
        private static Camera camaraPrincipal;

        // Evento: notifica al gestor cuando el jugador hace click (seleccionar/jugar)
        public System.Action<CardView> OnCartaSeleccionada;

        // Evento: notifica cuando el jugador suelta la carta tras arrastrarla,
        // con la posicion de mundo donde la soltó (el gestor decide si es "jugar" o "reordenar").
        public System.Action<CardView, Vector3> OnCartaArrastradaYSoltada;

        // Evento: se dispara en cada frame mientras la carta se arrastra, con su posicion de mundo
        // (el gestor lo usa para mostrar donde quedaria al soltarla).
        public System.Action<CardView, Vector3> OnCartaArrastrando;

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
            AjustarATamano();
        }

        public void MostrarDorso()
        {
            mostrandoCara = false;
            spriteRenderer.sprite = spriteDorso;
            AjustarATamano();
        }

        /// <summary>
        /// Marca/desmarca la carta como seleccionada (la eleva visualmente).
        /// </summary>
        public void Seleccionar(bool valor)
        {
            seleccionada = valor;
            spriteRenderer.sortingOrder = ordenDibujadoBase; // por si venia de un arrastre (orden elevado)
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
            ordenDibujadoBase = orderInLayer;
            spriteRenderer.sortingOrder = orderInLayer;
            ActualizarPosicionObjetivo();
        }

        /// <summary>
        /// Establece la rotacion Z de la carta en grados (para el abanico de la mano).
        /// </summary>
        public void SetRotacion(float anguloZ, bool suave = false)
        {
            rotacionBase = anguloZ;
            if (suave)
                rotacionSuave = true; // Update la interpola hasta rotacionBase
            else
                transform.rotation = Quaternion.Euler(0f, 0f, rotacionBase);
        }

        /// <summary>Resalta la carta (tinte dorado) para indicar que al soltarla se jugara.</summary>
        public void SetListaParaJugar(bool valor)
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.color = valor ? new Color(1f, 0.93f, 0.6f, 1f) : Color.white;
        }

        /// <summary>
        /// Establece la escala de la carta (para diferentes tamaños según la zona de mesa).
        /// </summary>
        public void SetEscala(Vector3 escala)
        {
            escalaBase = escala;
            transform.localScale = escala;
        }

        /// <summary>Fija el tamaño en unidades de mundo de la carta (ancho, alto). La escala se
        /// calcula a partir de los limites del sprite actual y el collider se ajusta a el.</summary>
        public void SetTamanoMundo(Vector2 tamano)
        {
            tamanoMundo = tamano;
            AjustarATamano();
        }

        private void AjustarATamano()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (col2D == null) col2D = GetComponent<BoxCollider2D>();

            Sprite sprite = spriteRenderer.sprite;
            if (sprite == null) return;

            Vector2 tamanoSprite = sprite.bounds.size;
            if (tamanoSprite.x <= 0f || tamanoSprite.y <= 0f) return;

            escalaBase = new Vector3(tamanoMundo.x / tamanoSprite.x, tamanoMundo.y / tamanoSprite.y, 1f);
            transform.localScale = escalaBase;

            // El collider vive en espacio local: se ajusta al sprite para que el area
            // clicable coincida exactamente con lo que se ve.
            col2D.size = tamanoSprite;
            col2D.offset = sprite.bounds.center;
        }

        // =====================================================================
        // Input (Mouse y Touch)
        // =====================================================================

        private void OnMouseEnter()
        {
            if (!interactuable || seleccionada || arrastrando) return;
            transform.localScale = escalaBase * escalaHover;
            ActualizarPosicionObjetivo();
        }

        private void OnMouseExit()
        {
            if (!interactuable || arrastrando) return;
            transform.localScale = escalaBase;
            ActualizarPosicionObjetivo();
        }

        private void OnMouseDown()
        {
            if (!interactuable) return;
            arrastrando = false;
            inicioArrastreMundo = ObtenerPosicionMouseMundo();
        }

        /// <summary>Mientras el boton siga pulsado sobre esta carta: si el puntero se aleja
        /// lo suficiente del punto inicial, pasa a modo arrastre y la carta sigue al mouse.</summary>
        private void OnMouseDrag()
        {
            if (!interactuable) return;

            Vector3 mundo = ObtenerPosicionMouseMundo();

            if (!arrastrando && Vector3.Distance(mundo, inicioArrastreMundo) > umbralArrastreMundo)
            {
                arrastrando = true;
                rotacionSuave = true;
                spriteRenderer.sortingOrder = 100; // por encima de cualquier otra carta mientras se arrastra
                transform.localScale = escalaBase * escalaArrastre;
            }

            if (arrastrando)
            {
                // La carta no se pega al cursor: lo persigue con inercia (ver Update), como en
                // Slay the Spire. El evento lleva la posicion del cursor, no la de la carta.
                posicionCursorArrastre = new Vector3(mundo.x, mundo.y, posicionBase.z);
                OnCartaArrastrando?.Invoke(this, posicionCursorArrastre);
            }
        }

        private void OnMouseUp()
        {
            if (!interactuable) return;

            if (arrastrando)
            {
                arrastrando = false;
                transform.localScale = escalaBase;
                SetListaParaJugar(false);
                OnCartaArrastradaYSoltada?.Invoke(this, posicionCursorArrastre);
            }
            else
            {
                // Click normal (sin movimiento): seleccionar / jugar si ya estaba seleccionada
                Seleccionar(!seleccionada);
                OnCartaSeleccionada?.Invoke(this);
            }
        }

        /// <summary>Cancela un arrastre sin efecto (soltada fuera de cualquier zona valida):
        /// vuelve suavemente a su posicion y orden de dibujado normales.</summary>
        public void CancelarArrastre()
        {
            spriteRenderer.sortingOrder = ordenDibujadoBase;
            ActualizarPosicionObjetivo();
        }

        private Vector3 ObtenerPosicionMouseMundo()
        {
            if (camaraPrincipal == null) camaraPrincipal = Camera.main;
            Vector3 pantalla = Input.mousePosition;
            pantalla.z = camaraPrincipal.WorldToScreenPoint(transform.position).z;
            return camaraPrincipal.ScreenToWorldPoint(pantalla);
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
            if (arrastrando)
            {
                // Sigue al cursor con inercia y se inclina segun la velocidad horizontal
                Vector3 anterior = transform.position;
                transform.position = Vector3.Lerp(anterior, posicionCursorArrastre, Time.deltaTime * seguimientoArrastre);

                float velocidadX = Time.deltaTime > 0f ? (transform.position.x - anterior.x) / Time.deltaTime : 0f;
                float inclinacion = Mathf.Clamp(-velocidadX * 1.5f, -15f, 15f);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, Quaternion.Euler(0f, 0f, inclinacion), Time.deltaTime * 12f);
                return;
            }

            if (rotacionSuave)
            {
                float z = Mathf.LerpAngle(transform.eulerAngles.z, rotacionBase, Time.deltaTime * velocidadAnimacion);
                transform.rotation = Quaternion.Euler(0f, 0f, z);
                if (Mathf.Abs(Mathf.DeltaAngle(z, rotacionBase)) < 0.05f)
                {
                    transform.rotation = Quaternion.Euler(0f, 0f, rotacionBase);
                    rotacionSuave = false;
                }
            }

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
