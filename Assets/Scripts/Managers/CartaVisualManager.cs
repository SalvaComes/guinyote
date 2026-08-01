using System.Collections.Generic;
using UnityEngine;
using GuinoteApp.Core;
using GuinoteApp.View;

namespace GuinoteApp.Managers
{
    /// <summary>
    /// Gestiona la creacion, posicionado y destruccion de los GameObjects de cartas.
    /// Actua como fabrica y organizador de la capa visual.
    ///
    /// Layout de mesa (según diseño):
    ///   - Arriba (centro): Compañero (J3) — dorsos horizontales + carta jugada debajo
    ///   - Izquierda: Rival (J2) — dorsos verticales + carta jugada a la derecha
    ///   - Derecha: Rival (J4) — carta jugada a la izquierda + dorsos verticales
    ///   - Abajo (centro): Tú (J0) — carta jugada arriba + mano en abanico (boca arriba)
    ///   - Centro: mazo + triunfo rotado 7°
    /// </summary>
    public class CartaVisualManager : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private GameObject cartaPrefab;

        // =================================================================
        // Layout — Mano del Jugador Local (abajo)
        // =================================================================

        [Header("Layout — Tu mano (abajo)")]
        [SerializeField] private Transform puntoAnclajeManoPrincipal;
        [SerializeField] private float separacionCartas = 1.0f;
        [SerializeField] private float zOrdenBase = 0f;
        [SerializeField] private float anguloAbanico = 4f; // grados entre cartas

        // =================================================================
        // Layout — Posiciones de cartas jugadas en la baza (centro de mesa)
        // =================================================================

        [Header("Layout — Cartas jugadas en baza")]
        [SerializeField] private Transform posCartaJugadaJ0; // Abajo (tú)
        [SerializeField] private Transform posCartaJugadaJ1; // Izquierda (rival)
        [SerializeField] private Transform posCartaJugadaJ2; // Arriba (compañero)
        [SerializeField] private Transform posCartaJugadaJ3; // Derecha (rival)

        // =================================================================
        // Layout — Dorsos de rivales/compañero
        // =================================================================

        [Header("Layout — Manos de IA (dorsos)")]
        [SerializeField] private Transform puntoAnclajeManoArriba;    // Compañero (J2 en diseño, J3 en lógica)
        [SerializeField] private Transform puntoAnclajeManoIzquierda; // Rival izquierdo (J1)
        [SerializeField] private Transform puntoAnclajeManoDerecha;   // Rival derecho (J3)

        // =================================================================
        // Layout — Mazo y triunfo
        // =================================================================

        [Header("Layout — Mazo y triunfo")]
        [SerializeField] private Transform puntoAnclajeTriunfo;
        [SerializeField] private Transform puntoAnclajeMazo;

        // =================================================================
        // Sprites
        // =================================================================

        private Dictionary<string, Sprite> spritesCartas = new Dictionary<string, Sprite>();
        private Sprite spriteDorso;

        // =================================================================
        // Cartas visuales activas
        // =================================================================

        // Mano del jugador local (boca arriba, interactuables)
        private List<CardView> cartasEnMano = new List<CardView>();

        // Dorsos de las manos de los otros jugadores
        private List<GameObject> dorsosArriba    = new List<GameObject>();
        private List<GameObject> dorsosIzquierda = new List<GameObject>();
        private List<GameObject> dorsosDerecha   = new List<GameObject>();

        // Cartas jugadas en la baza actual
        private CardView[] cartasEnBaza = new CardView[4]; // [0..3] por jugador

        // Triunfo y mazo visual
        private CardView triunfoVisual;
        private GameObject mazoVisual;

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

        // =================================================================
        // Mano del jugador local (abajo, boca arriba, en abanico)
        // =================================================================

        /// <summary>
        /// Muestra la mano del jugador local en abanico con rotaciones.
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

            float anguloInicial = -anguloAbanico * (total - 1) / 2f;

            for (int i = 0; i < total; i++)
            {
                float offsetX = -anchoTotal / 2f + i * separacionCartas;
                Vector3 pos = origen + new Vector3(offsetX, 0f, 0f);
                float rotacion = anguloInicial + i * anguloAbanico;

                CardView vista = CrearCartaVisual(mano[i], pos, mostrarCara: true);
                if (vista == null) continue;

                vista.MoverA(pos, orderInLayer: i);
                vista.SetRotacion(rotacion);
                vista.OnCartaSeleccionada += ManejarSeleccionCarta;
                cartasEnMano.Add(vista);
            }
        }

        // =================================================================
        // Dorsos de manos de IA (arriba, izquierda, derecha)
        // =================================================================

        /// <summary>
        /// Muestra los dorsos de la mano de un jugador IA en la posición indicada.
        /// </summary>
        public void MostrarDorsosJugador(int jugadorIdx, int numeroDorsos)
        {
            // Limpiar dorsos anteriores de este jugador
            LimpiarDorsos(jugadorIdx);

            Transform anclaje = null;
            bool horizontal = true;
            float separacion = 0.25f;
            List<GameObject> listaDorsos;

            switch (jugadorIdx)
            {
                case 1: // Izquierda
                    anclaje = puntoAnclajeManoIzquierda;
                    horizontal = false;
                    separacion = 0.2f;
                    listaDorsos = dorsosIzquierda;
                    break;
                case 2: // Arriba (compañero)
                    anclaje = puntoAnclajeManoArriba;
                    horizontal = true;
                    separacion = 0.25f;
                    listaDorsos = dorsosArriba;
                    break;
                case 3: // Derecha
                    anclaje = puntoAnclajeManoDerecha;
                    horizontal = false;
                    separacion = 0.2f;
                    listaDorsos = dorsosDerecha;
                    break;
                default:
                    return;
            }

            Vector3 origen = anclaje != null ? anclaje.position : Vector3.zero;
            float totalExtent = (numeroDorsos - 1) * separacion;

            for (int i = 0; i < numeroDorsos; i++)
            {
                float offset = -totalExtent / 2f + i * separacion;
                Vector3 pos;
                Quaternion rot;

                if (horizontal)
                {
                    pos = origen + new Vector3(offset, 0f, 0f);
                    rot = Quaternion.identity;
                }
                else
                {
                    pos = origen + new Vector3(0f, -offset, 0f);
                    rot = Quaternion.Euler(0f, 0f, 90f);
                }

                GameObject dorso = CrearDorso(pos, rot, i);
                listaDorsos.Add(dorso);
            }
        }

        /// <summary>Actualiza los dorsos de todos los jugadores IA.</summary>
        public void ActualizarDorsosIA(List<Carta>[] manos)
        {
            for (int j = 1; j <= 3; j++)
            {
                MostrarDorsosJugador(j, manos[j].Count);
            }
        }

        // =================================================================
        // Cartas jugadas en la baza (centro de mesa)
        // =================================================================

        /// <summary>
        /// Muestra una carta jugada en la posición de baza del jugador indicado.
        /// </summary>
        public void MostrarCartaEnBaza(int jugadorIdx, Carta carta)
        {
            Transform pos = ObtenerPosicionBaza(jugadorIdx);
            if (pos == null)
            {
                Debug.LogWarning($"[CartaVisualManager] Posición de baza no asignada para J{jugadorIdx + 1}");
                return;
            }

            CardView vista = CrearCartaVisual(carta, pos.position, mostrarCara: true);
            if (vista != null)
            {
                vista.SetInteractuable(false);
                vista.MoverA(pos.position, orderInLayer: 10 + jugadorIdx);
                cartasEnBaza[jugadorIdx] = vista;
            }
        }

        /// <summary>Limpia todas las cartas de la baza actual.</summary>
        public void LimpiarBaza()
        {
            for (int i = 0; i < cartasEnBaza.Length; i++)
            {
                if (cartasEnBaza[i] != null)
                {
                    Destroy(cartasEnBaza[i].gameObject);
                    cartasEnBaza[i] = null;
                }
            }
        }

        // =================================================================
        // Triunfo y Mazo
        // =================================================================

        /// <summary>
        /// Muestra el triunfo junto al mazo (rotado 7° según diseño).
        /// </summary>
        public void MostrarTriunfo(Carta triunfo)
        {
            // Destruir el triunfo visual anterior si existe
            if (triunfoVisual != null)
            {
                Destroy(triunfoVisual.gameObject);
                triunfoVisual = null;
            }

            // null indica que el mazo se agoto (Fase Arrastre) → no crear nuevo visual
            if (triunfo == null) return;

            Vector3 pos = puntoAnclajeTriunfo != null
                ? puntoAnclajeTriunfo.position
                : new Vector3(0f, 0f, 0f);

            triunfoVisual = CrearCartaVisual(triunfo, pos, mostrarCara: true);
            if (triunfoVisual != null)
            {
                // El triunfo se muestra girado 7° según el diseño
                triunfoVisual.transform.rotation = Quaternion.Euler(0f, 0f, 7f);
                triunfoVisual.SetInteractuable(false);
                triunfoVisual.gameObject.name = "Carta_Triunfo";
            }
        }

        /// <summary>Muestra el mazo visual (pila de dorsos apilados).</summary>
        public void MostrarMazo(int cartasRestantes)
        {
            if (mazoVisual != null)
                Destroy(mazoVisual);

            if (cartasRestantes <= 0) return;

            Vector3 pos = puntoAnclajeMazo != null
                ? puntoAnclajeMazo.position
                : new Vector3(-0.5f, 0f, 0f);

            mazoVisual = CrearDorso(pos, Quaternion.Euler(0f, 0f, -6f), 5);
        }

        // =================================================================
        // Gestión de mano del jugador
        // =================================================================

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

        /// <summary>Limpia toda la mesa (mano, dorsos, baza, triunfo, mazo).</summary>
        public void LimpiarTodo()
        {
            LimpiarMano();
            LimpiarBaza();
            LimpiarDorsos(1);
            LimpiarDorsos(2);
            LimpiarDorsos(3);

            if (triunfoVisual != null) { Destroy(triunfoVisual.gameObject); triunfoVisual = null; }
            if (mazoVisual != null)    { Destroy(mazoVisual); mazoVisual = null; }
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

            float anguloInicial = -anguloAbanico * (total - 1) / 2f;

            for (int i = 0; i < total; i++)
            {
                float offsetX = -anchoTotal / 2f + i * separacionCartas;
                Vector3 nuevaPos = origen + new Vector3(offsetX, 0f, 0f);
                float rotacion = anguloInicial + i * anguloAbanico;

                cartasEnMano[i].MoverA(nuevaPos, orderInLayer: i);
                cartasEnMano[i].SetRotacion(rotacion);
            }
        }

        private Transform ObtenerPosicionBaza(int jugadorIdx)
        {
            switch (jugadorIdx)
            {
                case 0: return posCartaJugadaJ0;
                case 1: return posCartaJugadaJ1;
                case 2: return posCartaJugadaJ2;
                case 3: return posCartaJugadaJ3;
                default: return null;
            }
        }

        private GameObject CrearDorso(Vector3 posicion, Quaternion rotacion, int sortOrder)
        {
            if (cartaPrefab == null) return null;

            GameObject go = Instantiate(cartaPrefab, posicion, rotacion);
            go.name = "Dorso";

            CardView vista = go.GetComponent<CardView>();
            if (vista != null)
            {
                vista.Inicializar(null, spriteDorso, spriteDorso);
                vista.MostrarDorso();
                vista.SetInteractuable(false);

                SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sortingOrder = sortOrder;
            }

            return go;
        }

        private void LimpiarDorsos(int jugadorIdx)
        {
            List<GameObject> lista;
            switch (jugadorIdx)
            {
                case 1: lista = dorsosIzquierda; break;
                case 2: lista = dorsosArriba; break;
                case 3: lista = dorsosDerecha; break;
                default: return;
            }

            foreach (GameObject go in lista)
            {
                if (go != null) Destroy(go);
            }
            lista.Clear();
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
