using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GuinoteApp.UI
{
    /// <summary>
    /// Pantalla de selección de mesa: modo de juego, nivel de IA de cada jugador
    /// y botón de comenzar partida.
    /// Layout: grid 3×2 con 4 posiciones de jugador. Cada jugador controlado por la IA
    /// (rival 1, compañero, rival 2) tiene su propio selector de nivel dentro de su tarjeta
    /// (se crea en tiempo de ejecución, no depende de la escena). Los botones Fácil/Normal/
    /// Difícil/Experto del pie asignan el mismo nivel a los tres a la vez.
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

        [Header("Dificultad para todos (Segmented Control)")]
        [SerializeField] private Button botonFacil;
        [SerializeField] private Button botonNormal;
        [SerializeField] private Button botonDificil;
        [SerializeField] private Button botonExperto;
        [SerializeField] private Image  fondoFacil;
        [SerializeField] private Image  fondoNormal;
        [SerializeField] private Image  fondoDificil;
        [SerializeField] private Image  fondoExperto;

        [Header("Acción")]
        [SerializeField] private Button botonComenzar;
        [SerializeField] private Button botonVolver;

        // =================================================================
        // Estado
        // =================================================================

        /// <summary>Número de jugadores controlados por la IA: Rival 1 (J2), Compañero (J3) y Rival 2 (J4).</summary>
        public const int JugadoresIA = 3;

        private static readonly string[] NombresJugadores = { "Rival 1", "Compañero", "Rival 2" };
        private static readonly string[] NombresCards = { "Card_Rival1", "Card_Companero", "Card_Rival2" };

        private readonly DificultadIA[] nivelesJugador =
            { DificultadIA.Normal, DificultadIA.Normal, DificultadIA.Normal };

        private readonly TMP_Text[] etiquetasNivel = new TMP_Text[JugadoresIA];
        private bool modoContraIA = true;

        /// <summary>Nivel de cada jugador IA, en el orden J2, J3, J4 (rival 1, compañero, rival 2).</summary>
        public DificultadIA[] NivelesJugador => (DificultadIA[])nivelesJugador.Clone();

        /// <summary>Se dispara al comenzar la partida con el nivel de J2, J3 y J4, en ese orden.</summary>
        public event Action<DificultadIA[]> OnComenzarPartida;

        /// <summary>Se dispara al pulsar "Multijugador": abre el lobby para crear o unirse a una sala.</summary>
        public event Action OnAbrirMultijugador;

        // =================================================================
        // Inicialización
        // =================================================================

        private void Awake()
        {
            if (textoTitulo != null)
                textoTitulo.text = "Elige tu mesa";
            if (textoTu != null)
                textoTu.text = "Tú";

            // Modo de juego
            if (botonContraIA != null)
                botonContraIA.onClick.AddListener(() => SeleccionarModo(true));
            if (botonMultijugador != null)
            {
                botonMultijugador.interactable = true;
                botonMultijugador.onClick.AddListener(() =>
                {
                    SeleccionarModo(false);
                    OnAbrirMultijugador?.Invoke();
                });
            }

            // Dificultad para todos
            CrearBotonExpertoSiFalta();
            if (botonFacil != null)
                botonFacil.onClick.AddListener(() => AsignarNivelATodos(DificultadIA.Facil));
            if (botonNormal != null)
                botonNormal.onClick.AddListener(() => AsignarNivelATodos(DificultadIA.Normal));
            if (botonDificil != null)
                botonDificil.onClick.AddListener(() => AsignarNivelATodos(DificultadIA.Dificil));
            if (botonExperto != null)
                botonExperto.onClick.AddListener(() => AsignarNivelATodos(DificultadIA.Experto));

            // Selector de nivel propio en la tarjeta de cada jugador IA
            CrearSelectoresPorJugador();

            // A cuantos Juegos se juega el Coto (3 o 5)
            CrearBotonCoto();

            // Acciones
            if (botonComenzar != null)
                botonComenzar.onClick.AddListener(OnClickComenzar);
            if (botonVolver != null)
                botonVolver.onClick.AddListener(OnClickVolver);

            // Estado visual inicial
            SeleccionarModo(true);
            ActualizarVistaNiveles();
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

        /// <summary>Asigna el mismo nivel a los tres jugadores controlados por la IA.</summary>
        public void AsignarNivelATodos(DificultadIA nivel)
        {
            for (int i = 0; i < JugadoresIA; i++)
                nivelesJugador[i] = nivel;
            ActualizarVistaNiveles();
            Debug.Log($"[PantallaSeleccion] Nivel para todos: {nivel}");
        }

        /// <summary>Asigna el nivel de un jugador IA (0 = rival 1 / J2, 1 = compañero / J3, 2 = rival 2 / J4).</summary>
        public void AsignarNivel(int jugadorIA, DificultadIA nivel)
        {
            if (jugadorIA < 0 || jugadorIA >= JugadoresIA) return;
            nivelesJugador[jugadorIA] = nivel;
            ActualizarVistaNiveles();
            Debug.Log($"[PantallaSeleccion] {NombresJugadores[jugadorIA]}: {nivel}");
        }

        /// <summary>Pasa al siguiente nivel del jugador (Fácil → Normal → Difícil → Experto → Fácil).</summary>
        public void CambiarNivelSiguiente(int jugadorIA)
        {
            if (jugadorIA < 0 || jugadorIA >= JugadoresIA) return;
            int siguiente = ((int)nivelesJugador[jugadorIA] + 1) % Enum.GetValues(typeof(DificultadIA)).Length;
            AsignarNivel(jugadorIA, (DificultadIA)siguiente);
        }

        private static string NombreNivel(DificultadIA nivel) =>
            nivel == DificultadIA.Dificil ? "Difícil" : nivel.ToString();

        /// <summary>Refresca etiquetas de las tarjetas y resalta el control global si los tres coinciden.</summary>
        private void ActualizarVistaNiveles()
        {
            for (int i = 0; i < JugadoresIA; i++)
            {
                if (etiquetasNivel[i] != null)
                    etiquetasNivel[i].text = $"◀  {NombreNivel(nivelesJugador[i])}  ▶";
            }

            bool iguales = nivelesJugador[0] == nivelesJugador[1] && nivelesJugador[1] == nivelesJugador[2];
            DificultadIA comun = nivelesJugador[0];

            if (fondoFacil != null)
                fondoFacil.color = iguales && comun == DificultadIA.Facil ? DesignTokens.ColorAccent : Color.clear;
            if (fondoNormal != null)
                fondoNormal.color = iguales && comun == DificultadIA.Normal ? DesignTokens.ColorAccent : Color.clear;
            if (fondoDificil != null)
                fondoDificil.color = iguales && comun == DificultadIA.Dificil ? DesignTokens.ColorAccent : Color.clear;
            if (fondoExperto != null)
                fondoExperto.color = iguales && comun == DificultadIA.Experto ? DesignTokens.ColorAccent : Color.clear;

            // Los textos de las tarjetas indican quién es cada uno
            if (textoRival1 != null) textoRival1.text = $"Rival 1 · {NombreNivel(nivelesJugador[0])}";
            if (textoCompanero != null) textoCompanero.text = $"Compañero · {NombreNivel(nivelesJugador[1])}";
            if (textoRival2 != null) textoRival2.text = $"Rival 2 · {NombreNivel(nivelesJugador[2])}";
        }

        /// <summary>
        /// Si la escena no trae el boton "Experto" (se construyo antes de existir ese nivel),
        /// lo crea clonando el de "Dificil" y colocandolo a su derecha.
        /// </summary>
        private void CrearBotonExpertoSiFalta()
        {
            if (botonExperto != null || botonDificil == null) return;

            Transform padre = botonDificil.transform.parent;
            GameObject clon = Instantiate(botonDificil.gameObject, padre);
            clon.name = "Btn_Experto";

            // Con un LayoutGroup en el padre la posicion la decide el propio layout
            if (padre == null || padre.GetComponent<LayoutGroup>() == null)
            {
                var origen = (RectTransform)botonDificil.transform;
                var rt = (RectTransform)clon.transform;
                rt.anchoredPosition = origen.anchoredPosition + new Vector2(origen.sizeDelta.x + 8f, 0f);
            }

            TMP_Text texto = clon.GetComponentInChildren<TMP_Text>();
            if (texto != null) texto.text = "Experto";

            botonExperto = clon.GetComponent<Button>();
            fondoExperto = clon.GetComponent<Image>();
        }

        // =================================================================
        // Selector de nivel por jugador (creado en tiempo de ejecución)
        // =================================================================

        private void CrearSelectoresPorJugador()
        {
            TMP_Text[] textos = { textoRival1, textoCompanero, textoRival2 };

            for (int i = 0; i < JugadoresIA; i++)
            {
                Transform tarjeta = BuscarTarjeta(NombresCards[i]);
                if (tarjeta == null && textos[i] != null)
                    tarjeta = textos[i].transform.parent;
                if (tarjeta == null)
                {
                    Debug.LogWarning($"[PantallaSeleccion] No se encuentra la tarjeta de {NombresJugadores[i]}: no se puede crear su selector de nivel.");
                    continue;
                }

                int jugador = i;
                etiquetasNivel[i] = CrearBotonNivel(tarjeta, jugador);
            }
        }

        private Transform BuscarTarjeta(string nombre)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.name == nombre) return t;
            return null;
        }

        /// <summary>Boton "◀ Nivel ▶" anclado al fondo de la tarjeta; cada clic pasa al siguiente nivel.</summary>
        private TMP_Text CrearBotonNivel(Transform tarjeta, int jugador)
        {
            var go = new GameObject("Btn_Nivel", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(tarjeta, false);
            go.GetComponent<LayoutElement>().ignoreLayout = true;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(180f, 34f);
            rt.anchoredPosition = new Vector2(0f, 48f);

            go.GetComponent<Image>().color = DesignTokens.Neutral300;
            go.GetComponent<Button>().onClick.AddListener(() => CambiarNivelSiguiente(jugador));

            var hijo = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
            hijo.transform.SetParent(go.transform, false);
            var rtTexto = (RectTransform)hijo.transform;
            rtTexto.anchorMin = Vector2.zero;
            rtTexto.anchorMax = Vector2.one;
            rtTexto.offsetMin = new Vector2(4f, 2f);
            rtTexto.offsetMax = new Vector2(-4f, -2f);

            var texto = hijo.GetComponent<TextMeshProUGUI>();
            texto.fontSize = 16f;
            texto.fontStyle = FontStyles.Bold;
            texto.color = DesignTokens.ColorText;
            texto.alignment = TextAlignmentOptions.Center;
            texto.raycastTarget = false;
            return texto;
        }

        // =================================================================
        // Coto: a 3 o a 5 juegos
        // =================================================================

        private TMP_Text etiquetaCoto;

        /// <summary>Boton "Coto a N juegos" sobre el de comenzar; cada clic alterna entre 3 y 5.</summary>
        private void CrearBotonCoto()
        {
            var go = new GameObject("Btn_Coto", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(transform, false);
            go.GetComponent<LayoutElement>().ignoreLayout = true;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(240f, 44f);
            rt.anchoredPosition = new Vector2(-64f, 112f);

            go.GetComponent<Image>().color = DesignTokens.Neutral300;
            go.GetComponent<Button>().onClick.AddListener(() =>
            {
                MenuConfiguracion.Alternar();
                ActualizarEtiquetaCoto();
            });

            var hijo = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
            hijo.transform.SetParent(go.transform, false);
            var rtTexto = (RectTransform)hijo.transform;
            rtTexto.anchorMin = Vector2.zero;
            rtTexto.anchorMax = Vector2.one;
            rtTexto.offsetMin = new Vector2(4f, 2f);
            rtTexto.offsetMax = new Vector2(-4f, -2f);

            etiquetaCoto = hijo.GetComponent<TextMeshProUGUI>();
            etiquetaCoto.fontSize = 18f;
            etiquetaCoto.fontStyle = FontStyles.Bold;
            etiquetaCoto.color = DesignTokens.ColorText;
            etiquetaCoto.alignment = TextAlignmentOptions.Center;
            etiquetaCoto.raycastTarget = false;
            ActualizarEtiquetaCoto();
        }

        private void ActualizarEtiquetaCoto()
        {
            if (etiquetaCoto != null)
                etiquetaCoto.text = $"Coto a {MenuConfiguracion.JuegosParaGanarPartida} juegos";
        }

        // =================================================================
        // Acciones
        // =================================================================

        private void OnClickComenzar()
        {
            Debug.Log($"[PantallaSeleccion] Comenzar partida — Rival 1: {nivelesJugador[0]}, Compañero: {nivelesJugador[1]}, Rival 2: {nivelesJugador[2]}");
            OnComenzarPartida?.Invoke((DificultadIA[])nivelesJugador.Clone());

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
        Dificil,
        Experto
    }
}
