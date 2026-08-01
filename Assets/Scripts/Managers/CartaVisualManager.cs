using System.Collections.Generic;
using UnityEngine;
using GuinoteApp.Core;
using GuinoteApp.View;

namespace GuinoteApp.Managers
{
    /// <summary>
    /// Gestiona la creacion, posicionado y destruccion de los GameObjects de cartas.
    /// Actua como fabrica y organizador de la capa visual.
    /// </summary>
    public class CartaVisualManager : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private GameObject cartaPrefab;

        [Header("Layout - Mano del Jugador Local")]
        [SerializeField] private Transform puntoAnclajeManoPrincipal;
        [SerializeField] private float separacionCartas = 1.4f;
        [SerializeField] private float zOrdenBase = 0f;

        [Header("Layout - Triunfo")]
        [SerializeField] private Transform puntoAnclajeTriunfo;

        // Sprites cargados desde Resources/Cards/
        private Dictionary<string, Sprite> spritesCartas = new Dictionary<string, Sprite>();
        private Sprite spriteDorso;

        // Cartas visuales activas
        private List<CardView> cartasEnMano = new List<CardView>();
        private CardView triufoVisual;

        // Evento: el jugador local hizo clic en una carta
        public System.Action<CardView> OnJugadorSeleccionoCarta;

        // =====================================================================
        // Inicializacion
        // =====================================================================

        private void Awake()
        {
            CargarSprites();
        }

        private void CargarSprites()
        {
            // Carga todos los sprites de la carpeta Resources/Cards/
            Sprite[] todos = Resources.LoadAll<Sprite>("Cards");
            foreach (Sprite s in todos)
            {
                spritesCartas[s.name] = s;
            }

            spriteDorso = ObtenerSprite("dorso");

            Debug.Log($"[CartaVisualManager] {spritesCartas.Count} sprites cargados desde Resources/Cards/");
        }

        // =====================================================================
        // API Publica
        // =====================================================================

        /// <summary>
        /// Instancia una CardView en la posicion indicada.
        /// </summary>
        public CardView CrearCartaVisual(Carta carta, Vector3 posicion, bool mostrarCara = true)
        {
            if (cartaPrefab == null)
            {
                Debug.LogError("[CartaVisualManager] cartaPrefab no asignado en el Inspector.");
                return null;
            }

            GameObject go = Instantiate(cartaPrefab, posicion, Quaternion.identity);
            go.name = $"Carta_{carta}";

            CardView vista = go.GetComponent<CardView>();
            if (vista == null)
            {
                Debug.LogError("[CartaVisualManager] El prefab no tiene componente CardView.");
                return null;
            }

            Sprite cara = ObtenerSpriteCarta(carta);
            vista.Inicializar(carta, cara, spriteDorso);

            if (!mostrarCara)
                vista.MostrarDorso();

            return vista;
        }

        /// <summary>
        /// Muestra la mano del jugador local con animacion de despliegue.
        /// Las cartas se distribuyen horizontalmente centradas en el punto de anclaje.
        /// </summary>
        public void MostrarManoJugador(List<Carta> mano)
        {
            LimpiarMano();

            int total = mano.Count;
            float anchoTotal = (total - 1) * separacionCartas;
            Vector3 origen = puntoAnclajeManoPrincipal != null
                ? puntoAnclajeManoPrincipal.position
                : new Vector3(0f, -3.5f, zOrdenBase);

            for (int i = 0; i < total; i++)
            {
                float offsetX = -anchoTotal / 2f + i * separacionCartas;
                Vector3 pos = origen + new Vector3(offsetX, 0f, 0f);

                CardView vista = CrearCartaVisual(mano[i], pos, mostrarCara: true);
                if (vista == null) continue;

                vista.MoverA(pos, orderInLayer: i);
                vista.OnCartaSeleccionada += ManejarSeleccionCarta;
                cartasEnMano.Add(vista);
            }
        }

        /// <summary>
        /// Muestra el triunfo junto al mazo.
        /// </summary>
        public void MostrarTriunfo(Carta triunfo)
        {
            // Destruir el triunfo visual anterior si existe
            if (triufoVisual != null)
            {
                Destroy(triufoVisual.gameObject);
                triufoVisual = null;
            }

            // null indica que el mazo se agoto (Fase Arrastre) → no crear nuevo visual
            if (triunfo == null) return;

            Vector3 pos = puntoAnclajeTriunfo != null
                ? puntoAnclajeTriunfo.position
                : new Vector3(1.5f, 0f, 0f);

            triufoVisual = CrearCartaVisual(triunfo, pos, mostrarCara: true);
            if (triufoVisual != null)
            {
                // El triunfo se muestra girado 90 grados por convencion del juego
                triufoVisual.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                triufoVisual.SetInteractuable(false);
                triufoVisual.gameObject.name = "Carta_Triunfo";
            }
        }

        /// <summary>
        /// Elimina todas las cartas visuales de la mano del jugador.
        /// </summary>
        public void LimpiarMano()
        {
            foreach (CardView vista in cartasEnMano)
            {
                if (vista != null)
                    Destroy(vista.gameObject);
            }
            cartasEnMano.Clear();
        }

        /// <summary>
        /// Elimina una carta especifica de la mano y reordena las restantes.
        /// </summary>
        public void QuitarCartaDeMano(CardView carta)
        {
            if (cartasEnMano.Contains(carta))
            {
                cartasEnMano.Remove(carta);
                Destroy(carta.gameObject);
                ReordenarMano();
            }
        }

        // =====================================================================
        // Logica interna
        // =====================================================================

        private void ManejarSeleccionCarta(CardView carta)
        {
            // Deselecciona el resto al seleccionar una
            foreach (CardView c in cartasEnMano)
            {
                if (c != carta)
                    c.Seleccionar(false);
            }
            OnJugadorSeleccionoCarta?.Invoke(carta);
        }

        private void ReordenarMano()
        {
            int total = cartasEnMano.Count;
            if (total == 0) return;

            float anchoTotal = (total - 1) * separacionCartas;
            Vector3 origen = puntoAnclajeManoPrincipal != null
                ? puntoAnclajeManoPrincipal.position
                : new Vector3(0f, -3.5f, zOrdenBase);

            for (int i = 0; i < total; i++)
            {
                float offsetX = -anchoTotal / 2f + i * separacionCartas;
                Vector3 nuevaPos = origen + new Vector3(offsetX, 0f, 0f);
                cartasEnMano[i].MoverA(nuevaPos, orderInLayer: i);
            }
        }

        private Sprite ObtenerSpriteCarta(Carta carta)
        {
            string nombre = $"{carta.Palo.ToString().ToLower()}_{carta.Rango.ToString().ToLower()}";
            return ObtenerSprite(nombre);
        }

        private Sprite ObtenerSprite(string nombre)
        {
            if (spritesCartas.TryGetValue(nombre, out Sprite s))
                return s;

            // Fallback: sprite vacio magenta para detectar sprites que faltan
            Debug.LogWarning($"[CartaVisualManager] Sprite no encontrado: '{nombre}'. Usando fallback magenta.");
            return CrearSpriteFallback(nombre);
        }

        private Sprite CrearSpriteFallback(string nombre)
        {
            Texture2D tex = new Texture2D(140, 200);
            Color[] pixels = new Color[140 * 200];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.magenta;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 140, 200), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
