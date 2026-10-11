using System.Collections;
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

        [Header("Baraja")]
        [Tooltip("assets/baraja_guinote/deck.json — indice de sprites de la baraja.")]
        [SerializeField] private TextAsset deckJson;

        // =================================================================
        // Layout — Mano del Jugador Local (abajo)
        // =================================================================

        [Header("Layout — Tu mano (abajo)")]
        [SerializeField] private Transform puntoAnclajeManoPrincipal;
        [SerializeField] private float separacionCartas = 1.0f;
        [SerializeField] private float zOrdenBase = 0f;
        [SerializeField] private float anguloAbanico = 4f; // grados entre cartas
        [Tooltip("Cuanto desciende (en unidades) cada carta respecto al centro del abanico, al cuadrado de su distancia.")]
        [SerializeField] private float curvaAbanico = 0.035f;

        // =================================================================
        // Tamaños de carta por zona (unidades de mundo). Proporciones del diseño
        // (62x88 / 54x76 / 48x68 / 44x62 px sobre un tapete de 600 px de alto = 10 unidades).
        // =================================================================

        [Header("Tamaños de carta (ancho, alto)")]
        [SerializeField] private Vector2 tamanoMano    = new Vector2(1.03f, 1.47f);
        [SerializeField] private Vector2 tamanoBaza    = new Vector2(0.90f, 1.27f);
        [SerializeField] private Vector2 tamanoRival   = new Vector2(0.80f, 1.13f);
        [SerializeField] private Vector2 tamanoTriunfo = new Vector2(0.73f, 1.03f);

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
        private CartaAssetResolver resolverAssets;

        // =================================================================
        // Cartas visuales activas
        // =================================================================

        // Mano del jugador local (boca arriba, interactuables)
        private List<CardView> cartasEnMano = new List<CardView>();

        // Orden visual persistente de la mano del jugador local: se inicializa por
        // palo+puntos al repartir, y luego el jugador puede reordenarla arrastrando
        // (no se vuelve a recalcular sola en cada refresco, solo se sincroniza:
        // se quitan las jugadas y se añaden al final las robadas).
        private List<Carta> ordenVisualMano = new List<Carta>();

        // Dorsos de las manos de los otros jugadores
        private List<GameObject> dorsosArriba    = new List<GameObject>();
        private List<GameObject> dorsosIzquierda = new List<GameObject>();
        private List<GameObject> dorsosDerecha   = new List<GameObject>();

        // Cartas jugadas en la baza actual
        private CardView[] cartasEnBaza = new CardView[4]; // [0..3] por jugador

        // Triunfo y mazo visual
        private CardView triunfoVisual;
        private GameObject haloTriunfo;
        private readonly List<GameObject> mazoCapas = new List<GameObject>();

        // Estado de la recolocacion de la mano mientras se arrastra una carta

        private int destinoPreview = SinPreview;
        private const int SinPreview = -2; // -1 = sobre la zona de juego; >=0 = hueco libre

        // Evento: el jugador local hizo clic en una carta (seleccionar/jugar)
        public System.Action<CardView> OnJugadorSeleccionoCarta;

        // Evento: el jugador local arrastró una carta hasta la zona de juego y la soltó
        public System.Action<CardView> OnJugadorArrastroCartaAJugar;

        // =====================================================================
        // Inicializacion
        // =====================================================================

        private void Awake()
        {
            CargarSprites();
        }

        private void CargarSprites()
        {
            if (deckJson == null)
            {
                Debug.LogError("[CartaVisualManager] deckJson no asignado en el Inspector (assets/baraja_guinote/deck.json).");
                return;
            }

            resolverAssets = new CartaAssetResolver(deckJson);

            // Carga todos los sprites de la carpeta Resources/Cards/
            // Si una carta existe en varios formatos (p.ej. .svg y .png con el mismo nombre)
            // LoadAll devuelve ambos y el ultimo ganaria de forma arbitraria. Nos quedamos con
            // el de proporcion mas cercana a la de la baraja (264x400), que descarta exportaciones
            // PNG recortadas o con otro aspecto.
            const float proporcionBaraja = 264f / 400f;
            Sprite[] todos = Resources.LoadAll<Sprite>("Cards");
            foreach (Sprite s in todos)
            {
                if (spritesCartas.TryGetValue(s.name, out Sprite previo) &&
                    Mathf.Abs(Proporcion(previo) - proporcionBaraja) <= Mathf.Abs(Proporcion(s) - proporcionBaraja))
                    continue;

                spritesCartas[s.name] = s;
            }

            spriteDorso = ObtenerSprite(resolverAssets.ResolverNombreDorso());

            Debug.Log($"[CartaVisualManager] {spritesCartas.Count} sprites cargados desde Resources/Cards/");
        }

        private static float Proporcion(Sprite s) => s.bounds.size.x / s.bounds.size.y;

        // =====================================================================
        // API Publica
        // =====================================================================

        /// <summary>
        /// Instancia una CardView en la posicion indicada con el tamaño de zona indicado.
        /// </summary>
        public CardView CrearCartaVisual(Carta carta, Vector3 posicion, bool mostrarCara, Vector2 tamano)
        {
            CardView vista = CrearCartaVisual(carta, posicion, mostrarCara);
            if (vista != null) vista.SetTamanoMundo(tamano);
            return vista;
        }

        /// <summary>
        /// Instancia una CardView en la posicion indicada (tamaño de mano por defecto).
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
            SincronizarOrdenVisualMano(mano);
            RenderizarMano();
        }

        /// <summary>Mantiene ordenVisualMano al dia sin deshacer el orden manual del jugador:
        /// quita las cartas que ya no estan en la mano (jugadas) y añade al final las nuevas
        /// (robadas). La primera vez (mano vacia: reparto inicial) ordena por palo y puntos.</summary>
        private void SincronizarOrdenVisualMano(List<Carta> mano)
        {
            if (ordenVisualMano.Count == 0)
            {
                ordenVisualMano = new List<Carta>(mano);
                ordenVisualMano.Sort(CompararPorPaloYPuntos);
                return;
            }

            ordenVisualMano.RemoveAll(c => !mano.Contains(c));
            foreach (Carta c in mano)
                if (!ordenVisualMano.Contains(c))
                    ordenVisualMano.Add(c);
        }

        /// <summary>Dibuja/reposiciona el abanico de la mano local a partir de ordenVisualMano.</summary>
        private void RenderizarMano()
        {
            LimpiarMano();

            int total = ordenVisualMano.Count;

            for (int i = 0; i < total; i++)
            {
                CalcularPosicionAbanico(i, total, out Vector3 pos, out float rotacion);

                CardView vista = CrearCartaVisual(ordenVisualMano[i], pos, true, tamanoMano);
                if (vista == null) continue;

                // orderInLayer empieza en 1 (no en 0): el Tapete usa sortingOrder 0, y un
                // empate deja el orden de dibujado indefinido (a veces tapa la carta).
                vista.MoverA(pos, orderInLayer: i + 1);
                vista.SetRotacion(rotacion);
                vista.OnCartaSeleccionada += ManejarSeleccionCarta;
                vista.OnCartaArrastradaYSoltada += ManejarCartaArrastrada;
                vista.OnCartaArrastrando += ManejarCartaArrastrando;
                cartasEnMano.Add(vista);
            }
        }

        /// <summary>Orden por defecto al repartir: por palo y, dentro de cada palo, de menor a
        /// mayor puntuación (empate a 0 puntos: por orden de cara, As, Dos, Tres... Rey).</summary>
        private static int CompararPorPaloYPuntos(Carta a, Carta b)
        {
            int porPalo = ((int)a.Palo).CompareTo((int)b.Palo);
            if (porPalo != 0) return porPalo;
            int porPuntos = a.ObtenerPuntos().CompareTo(b.ObtenerPuntos());
            return porPuntos != 0 ? porPuntos : OrdenVisualRango(a.Rango).CompareTo(OrdenVisualRango(b.Rango));
        }

        /// <summary>Orden visual "de cara" de un rango dentro de su palo (As=1 ... Rey=10),
        /// independiente del orden de fuerza en baza (que vive en el enum Rango de Core).
        /// Solo se usa como desempate entre cartas con los mismos puntos (todas las de 0).</summary>
        private static int OrdenVisualRango(Rango rango)
        {
            switch (rango)
            {
                case Rango.As:      return 1;
                case Rango.Dos:     return 2;
                case Rango.Tres:    return 3;
                case Rango.Cuatro:  return 4;
                case Rango.Cinco:   return 5;
                case Rango.Seis:    return 6;
                case Rango.Siete:   return 7;
                case Rango.Sota:    return 8;
                case Rango.Caballo: return 9;
                case Rango.Rey:     return 10;
                default:            return 99;
            }
        }

        // =================================================================
        // Arrastrar y soltar: jugar la carta o reordenar la mano
        // =================================================================

        /// <summary>Interpreta donde se soltó una carta arrastrada: zona de juego (jugarla),
        /// hueco de la mano (reordenar), o en cualquier otro sitio (cancelar).</summary>
        private void ManejarCartaArrastrada(CardView carta, Vector3 posSoltado)
        {
            destinoPreview = SinPreview;

            if (EsZonaDeJuego(posSoltado))
            {
                ReordenarMano(); // reabre la mano completa mientras se valida la jugada
                OnJugadorArrastroCartaAJugar?.Invoke(carta);
                return;
            }

            if (carta.CartaData != null)
            {
                int indiceOrigen  = ordenVisualMano.IndexOf(carta.CartaData);
                int indiceDestino = IndiceHuecoMasCercano(posSoltado.x, ordenVisualMano.Count);
                if (indiceOrigen >= 0 && indiceOrigen != indiceDestino)
                {
                    ordenVisualMano.RemoveAt(indiceOrigen);
                    ordenVisualMano.Insert(indiceDestino, carta.CartaData);
                    RenderizarMano();
                    return;
                }
            }

            ReordenarMano();
            carta.CancelarArrastre();
        }

        /// <summary>Mientras se arrastra una carta de la mano (estilo Slay the Spire): el resto de la
        /// mano se recoloca con animacion suave. Sobre la fila de la mano deja un hueco en el sitio
        /// donde caeria la carta; al sacarla hacia la zona de juego la mano se cierra sobre el hueco
        /// y la carta se resalta para indicar que al soltarla se jugara.</summary>
        private void ManejarCartaArrastrando(CardView carta, Vector3 pos)
        {
            int total = ordenVisualMano.Count;
            if (carta.CartaData == null || !ordenVisualMano.Contains(carta.CartaData)) return;

            bool enZonaDeJuego = EsZonaDeJuego(pos);
            carta.SetListaParaJugar(enZonaDeJuego);

            int destino = enZonaDeJuego ? -1 : IndiceHuecoMasCercano(pos.x, total);
            if (destino == destinoPreview) return; // nada cambio: no recolocar cada frame
            destinoPreview = destino;

            List<Carta> otros = new List<Carta>(ordenVisualMano);
            otros.Remove(carta.CartaData);

            // En la fila de la mano: total huecos, con el hueco 'destino' libre.
            // En zona de juego: la mano se cierra a total-1 huecos.
            int huecos = enZonaDeJuego ? total - 1 : total;

            foreach (CardView c in cartasEnMano)
            {
                if (c == null || c == carta) continue;
                int k = otros.IndexOf(c.CartaData);
                if (k < 0) continue;

                int hueco = (!enZonaDeJuego && k >= destino) ? k + 1 : k;
                CalcularPosicionAbanico(hueco, huecos, out Vector3 p, out float rot);
                c.MoverA(p, orderInLayer: hueco + 1);
                c.SetRotacion(rot, true);
            }
        }

        /// <summary>Indice del hueco del abanico (0..total-1) cuya X esta mas cerca de la dada.
        /// Se calcula sobre los huecos fijos del layout, no sobre las cartas (que se mueven
        /// durante el arrastre), asi que no oscila al desplazarlas.</summary>
        private int IndiceHuecoMasCercano(float x, int total)
        {
            float centroX = puntoAnclajeManoPrincipal != null ? puntoAnclajeManoPrincipal.position.x : 0f;
            int indice = Mathf.RoundToInt((x - centroX) / separacionCartas + (total - 1) / 2f);
            return Mathf.Clamp(indice, 0, total - 1);
        }

        /// <summary>Arrastrar hacia arriba, claramente fuera de la fila de la mano, cuenta como
        /// intento de jugar la carta (igual que soltarla sobre el hueco de la carta jugada).</summary>
        private bool EsZonaDeJuego(Vector3 posicionMundo)
        {
            float yMano = puntoAnclajeManoPrincipal != null ? puntoAnclajeManoPrincipal.position.y : -3.4f;
            return posicionMundo.y > yMano + 0.8f;
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

                GameObject dorso = CrearDorso(pos, rot, i + 1, tamanoRival); // +1: ver nota en MostrarManoJugador
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
        /// Para la IA (J1-J3), la carta sale boca abajo desde su mano y se voltea al llegar.
        /// El jugador humano (J0) ya la tiene boca arriba en su mano: se muestra directa.
        /// </summary>
        public void MostrarCartaEnBaza(int jugadorIdx, Carta carta)
        {
            Transform pos = ObtenerPosicionBaza(jugadorIdx);
            if (pos == null)
            {
                Debug.LogWarning($"[CartaVisualManager] Posición de baza no asignada para J{jugadorIdx + 1}");
                return;
            }

            if (jugadorIdx == 0)
            {
                CardView vista = CrearCartaVisual(carta, pos.position, true, tamanoBaza);
                if (vista != null)
                {
                    vista.SetInteractuable(false);
                    vista.MoverA(pos.position, orderInLayer: 10 + jugadorIdx);
                    cartasEnBaza[jugadorIdx] = vista;
                }
                return;
            }

            StartCoroutine(AnimarCartaRivalHaciaBaza(jugadorIdx, carta, pos));
        }

        /// <summary>Anima la carta de un rival/compañero: sale de su mano boca abajo,
        /// se desliza hasta la posición de baza y se voltea boca arriba al llegar.</summary>
        private IEnumerator AnimarCartaRivalHaciaBaza(int jugadorIdx, Carta carta, Transform posBaza)
        {
            Vector3 origen = ObtenerAnclaMano(jugadorIdx);

            CardView vista = CrearCartaVisual(carta, origen, false, tamanoRival);
            if (vista == null) yield break;

            vista.SetTamanoMundo(tamanoBaza); // de tamaño de mano rival a tamaño de baza al llegar
            vista.SetInteractuable(false);
            cartasEnBaza[jugadorIdx] = vista;
            vista.MoverA(posBaza.position, orderInLayer: 10 + jugadorIdx);

            const float distanciaLlegada = 0.15f;
            const float tiempoMaximo = 1.2f;
            float transcurrido = 0f;
            while (vista != null &&
                   Vector3.Distance(vista.transform.position, posBaza.position) > distanciaLlegada &&
                   transcurrido < tiempoMaximo)
            {
                transcurrido += Time.deltaTime;
                yield return null;
            }

            if (vista != null)
                vista.MostrarCara();
        }

        /// <summary>Punto de anclaje de la mano del jugador indicado (0=tú, 1=izq, 2=arriba, 3=der).</summary>
        private Vector3 ObtenerAnclaMano(int jugadorIdx)
        {
            Transform anclaje = jugadorIdx switch
            {
                0 => puntoAnclajeManoPrincipal,
                1 => puntoAnclajeManoIzquierda,
                2 => puntoAnclajeManoArriba,
                3 => puntoAnclajeManoDerecha,
                _ => null
            };
            return anclaje != null ? anclaje.position : Vector3.zero;
        }

        /// <summary>Anima una carta boca abajo desde el mazo hasta la mano del jugador indicado.</summary>
        public void AnimarRobo(int jugadorIdx, List<Carta>[] manosJugadores, bool esPinta = false) =>
            StartCoroutine(AnimarRoboHaciaJugador(jugadorIdx, manosJugadores, esPinta));

        /// <summary>Anima una carta boca abajo desde el mazo hasta la mano del jugador
        /// indicado, y al llegar refresca su vista (mano ordenada si es el humano, pila de
        /// dorsos si es la IA) para que la carta "recien robada" pase a formar parte de ella.</summary>
        private IEnumerator AnimarRoboHaciaJugador(int jugadorIdx, List<Carta>[] manosJugadores, bool esPinta)
        {
            Vector3 origen = puntoAnclajeMazo != null ? puntoAnclajeMazo.position : Vector3.zero;
            Vector3 destino = ObtenerAnclaMano(jugadorIdx);

            Debug.Log($"[CartaVisualManager] Animando robo de J{jugadorIdx + 1} | esPinta={esPinta} | " +
                      $"triunfoEnMesa={(triunfoVisual != null ? triunfoVisual.name : "no")}");

            GameObject visual;
            if (esPinta && triunfoVisual != null)
            {
                // Ultimo robo: vuela la propia carta de la pinta (boca arriba, desde donde esta),
                // no un dorso del mazo. Se desacopla de triunfoVisual para que nada la destruya.
                visual = triunfoVisual.gameObject;
                triunfoVisual = null;
                if (haloTriunfo != null) { Destroy(haloTriunfo); haloTriunfo = null; }
            }
            else
            {
                visual = CrearDorso(origen, Quaternion.identity, 20, tamanoRival);
            }
            if (visual == null) yield break;

            CardView vista = visual.GetComponent<CardView>();
            if (vista != null)
            {
                vista.MoverA(destino, orderInLayer: 20);
                if (esPinta)
                {
                    vista.SetRotacion(7f); // punto de partida: el giro de 7° con el que se mostraba
                    vista.SetRotacion(0f, true);
                }
            }

            const float distanciaLlegada = 0.15f;
            const float tiempoMaximo = 1f;
            float transcurrido = 0f;
            while (visual != null &&
                   Vector3.Distance(visual.transform.position, destino) > distanciaLlegada &&
                   transcurrido < tiempoMaximo)
            {
                transcurrido += Time.deltaTime;
                yield return null;
            }

            if (visual != null) Destroy(visual);

            if (jugadorIdx == 0)
                MostrarManoJugador(manosJugadores[0]);
            else
                ActualizarDorsosIA(manosJugadores);
        }

        /// <summary>Las cartas de la baza viajan hacia la mano del ganador, se desvanecen al llegar
        /// y desaparecen. Las cartas se sueltan de cartasEnBaza al empezar, asi la siguiente baza
        /// puede empezar a colocar cartas mientras esta animacion termina.</summary>
        public void AnimarBazaHaciaGanador(int ganadorIdx)
        {
            List<CardView> cartas = new List<CardView>();
            for (int i = 0; i < cartasEnBaza.Length; i++)
            {
                if (cartasEnBaza[i] != null) cartas.Add(cartasEnBaza[i]);
                cartasEnBaza[i] = null;
            }

            if (cartas.Count > 0)
                StartCoroutine(BarrerBazaHaciaGanador(cartas, ObtenerAnclaMano(ganadorIdx)));
        }

        private IEnumerator BarrerBazaHaciaGanador(List<CardView> cartas, Vector3 destino)
        {
            const float duracion = 0.55f;
            const float inicioFundido = 0.6f; // fraccion del recorrido a partir de la cual se desvanecen

            foreach (CardView c in cartas)
            {
                if (c == null) continue;
                c.MoverA(destino, orderInLayer: 50 + cartas.IndexOf(c)); // por encima de todo lo demas
                c.SetRotacion(0f);
            }

            float t = 0f;
            while (t < duracion)
            {
                t += Time.deltaTime;
                float fundido = Mathf.InverseLerp(inicioFundido, 1f, t / duracion);
                foreach (CardView c in cartas)
                {
                    if (c == null) continue;
                    SpriteRenderer sr = c.GetComponent<SpriteRenderer>();
                    Color col = sr.color;
                    col.a = 1f - fundido;
                    sr.color = col;
                }
                yield return null;
            }

            foreach (CardView c in cartas)
                if (c != null) Destroy(c.gameObject);
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
        public void MostrarTriunfo(Carta triunfo, bool ultimaRonda = false)
        {
            // Destruir el triunfo visual anterior si existe
            if (triunfoVisual != null)
            {
                Destroy(triunfoVisual.gameObject);
                triunfoVisual = null;
            }
            if (haloTriunfo != null)
            {
                Destroy(haloTriunfo);
                haloTriunfo = null;
            }

            // null indica que el mazo se agoto (Fase Arrastre) → no crear nuevo visual
            if (triunfo == null) return;

            Vector3 pos = puntoAnclajeTriunfo != null
                ? puntoAnclajeTriunfo.position
                : new Vector3(0f, 0f, 0f);

            triunfoVisual = CrearCartaVisual(triunfo, pos, true, tamanoTriunfo);
            if (triunfoVisual != null)
            {
                // orderInLayer explicito (nunca se llamaba MoverA aqui): el prefab trae
                // sortingOrder 0 por defecto, empatado con el Tapete (ver nota en MostrarManoJugador).
                triunfoVisual.MoverA(pos, orderInLayer: 5);
                // El triunfo se muestra girado 7° según el diseño
                triunfoVisual.transform.rotation = Quaternion.Euler(0f, 0f, 7f);
                triunfoVisual.SetInteractuable(false);
                triunfoVisual.gameObject.name = "Carta_Triunfo";

                if (ultimaRonda)
                    CrearHaloTriunfo(triunfoVisual, pos);
            }
        }

        /// <summary>Resplandor dorado tras la pinta: indica que es la ultima ronda de robo y
        /// que quien robe el ultimo se la lleva (despues empieza el arrastre).</summary>
        private void CrearHaloTriunfo(CardView triunfo, Vector3 pos)
        {
            SpriteRenderer srTriunfo = triunfo.GetComponent<SpriteRenderer>();
            if (srTriunfo == null || srTriunfo.sprite == null) return;

            haloTriunfo = new GameObject("HaloTriunfo");
            SpriteRenderer sr = haloTriunfo.AddComponent<SpriteRenderer>();
            sr.sprite = srTriunfo.sprite;
            sr.color = new Color(1f, 0.8f, 0.2f, 0.95f);
            sr.sortingOrder = srTriunfo.sortingOrder - 1;

            haloTriunfo.transform.SetPositionAndRotation(pos, triunfo.transform.rotation);
            Vector3 escala = triunfo.transform.localScale;
            haloTriunfo.transform.localScale = new Vector3(escala.x * 1.18f, escala.y * 1.12f, 1f);
        }

        /// <summary>Muestra el mazo como una pila de dorsos cuyo grosor refleja las cartas que
        /// quedan (una capa cada 3 cartas). En la ultima ronda de robo (menos de 4 cartas boca
        /// abajo) queda una sola capa y la pinta se resalta, para que se vea que es lo ultimo.</summary>
        public void MostrarMazo(int cartasRestantes, bool ultimaRonda = false)
        {
            foreach (GameObject capa in mazoCapas)
                if (capa != null) Destroy(capa);
            mazoCapas.Clear();

            if (cartasRestantes <= 0) return;

            Vector3 pos = puntoAnclajeMazo != null
                ? puntoAnclajeMazo.position
                : new Vector3(-0.5f, 0f, 0f);

            int capas = Mathf.Clamp(Mathf.CeilToInt(cartasRestantes / 3f), 1, 6);
            Vector3 desfase = new Vector3(-0.03f, 0.035f, 0f);

            for (int i = 0; i < capas; i++)
            {
                GameObject capa = CrearDorso(pos + desfase * i, Quaternion.Euler(0f, 0f, -6f), 5 + i, tamanoTriunfo);
                if (capa == null) continue;

                // Las capas inferiores algo mas oscuras dan sensacion de volumen
                SpriteRenderer sr = capa.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    float brillo = Mathf.Lerp(0.72f, 1f, capas == 1 ? 1f : i / (float)(capas - 1));
                    sr.color = new Color(brillo, brillo, brillo, 1f);
                }
                mazoCapas.Add(capa);
            }
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
            ordenVisualMano.Clear(); // nueva partida: el orden persistente empieza de cero
            LimpiarBaza();
            LimpiarDorsos(1);
            LimpiarDorsos(2);
            LimpiarDorsos(3);

            if (triunfoVisual != null) { Destroy(triunfoVisual.gameObject); triunfoVisual = null; }
            if (haloTriunfo != null)   { Destroy(haloTriunfo); haloTriunfo = null; }
            MostrarMazo(0);
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

            for (int i = 0; i < total; i++)
            {
                CalcularPosicionAbanico(i, total, out Vector3 nuevaPos, out float rotacion);

                cartasEnMano[i].MoverA(nuevaPos, orderInLayer: i + 1); // +1: ver nota en MostrarManoJugador
                cartasEnMano[i].SetRotacion(rotacion, true);
            }
        }

        /// <summary>Posicion y giro de la carta i de un abanico de 'total' cartas: centrado en el
        /// anclaje, girando hacia fuera y con los extremos algo mas bajos (arco suave).</summary>
        private void CalcularPosicionAbanico(int i, int total, out Vector3 pos, out float rotacion)
        {
            Vector3 origen = puntoAnclajeManoPrincipal != null
                ? puntoAnclajeManoPrincipal.position
                : new Vector3(0f, -3.8f, zOrdenBase);

            float centrado = i - (total - 1) / 2f;
            pos = origen + new Vector3(centrado * separacionCartas, -curvaAbanico * centrado * centrado, 0f);
            // Las cartas de la derecha del abanico giran en sentido horario (angulo Z negativo).
            rotacion = -centrado * anguloAbanico;
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

        private GameObject CrearDorso(Vector3 posicion, Quaternion rotacion, int sortOrder, Vector2 tamano)
        {
            if (cartaPrefab == null) return null;

            GameObject go = Instantiate(cartaPrefab, posicion, rotacion);
            go.name = "Dorso";

            CardView vista = go.GetComponent<CardView>();
            if (vista != null)
            {
                vista.Inicializar(null, spriteDorso, spriteDorso);
                vista.MostrarDorso();
                vista.SetTamanoMundo(tamano);
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
            string nombre = resolverAssets.ResolverNombreSprite(carta.Palo, carta.Rango);
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
