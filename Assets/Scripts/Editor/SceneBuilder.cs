using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace GuinoteApp.Editor
{
    /// <summary>
    /// Construye la jerarquía completa de la escena principal de Guiñote,
    /// siguiendo el mockup de Claude Design (design_handoff_guinote/Guiñote App.dc.html)
    /// y los tokens del sistema Broadsheet.
    /// Menú: Tools → Guiñote → 🏗️ Construir Escena Principal
    /// Es seguro volver a ejecutarlo: reconstruye el Canvas desde cero pero
    /// reutiliza Cámara/EventSystem/Tapete/GameManager/CartaVisualManager si ya existen,
    /// para no tocar sus referencias de lógica ya conectadas.
    /// </summary>
    public static class SceneBuilder
    {
        // =====================================================================
        // Colores del diseño (Design Tokens — Broadsheet)
        // =====================================================================

        private static readonly Color ColorBg        = new Color(0.953f, 0.949f, 0.949f, 1f); // --color-bg #f3f2f2
        private static readonly Color ColorSurface    = new Color(0.918f, 0.914f, 0.914f, 1f); // --color-surface #eae9e9
        private static readonly Color ColorTexto      = new Color(0.125f, 0.118f, 0.114f, 1f); // --color-text #201e1d
        private static readonly Color ColorAccent     = new Color(0.000f, 0.533f, 0.690f, 1f); // --color-accent #0088b0
        private static readonly Color ColorAccent2    = new Color(0.839f, 0.000f, 0.424f, 1f); // --color-accent-2 #d6006c
        private static readonly Color ColorAccent700  = new Color(0.000f, 0.404f, 0.525f, 1f); // #006786
        private static readonly Color ColorAccent2_200= new Color(1.000f, 0.871f, 0.902f, 1f); // #ffdee6
        private static readonly Color ColorNeutral400 = new Color(0.729f, 0.714f, 0.714f, 1f); // #bab6b6
        private static readonly Color ColorNeutral700 = new Color(0.376f, 0.365f, 0.365f, 1f); // #605d5d
        private static readonly Color ColorDivider    = new Color(0.125f, 0.118f, 0.114f, 0.16f); // color-mix text 16%

        // Tapete / mesa (no forman parte del design system, propios de la pantalla de juego)
        private static readonly Color ColorTapete     = new Color(0.122f, 0.369f, 0.235f, 1f); // #1f5e3c
        private static readonly Color ColorBorde      = new Color(0.541f, 0.353f, 0.169f, 1f); // #8a5a2b

        private const string FontPath = "Assets/Fonts/SourceSerif4-VariableFont_opsz,wght SDF.asset";
        private static TMP_FontAsset _fuente;
        private static TMP_FontAsset Fuente =>
            _fuente != null ? _fuente : (_fuente = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath));

        // =====================================================================
        // Entrada del menú
        // =====================================================================

        [MenuItem("Tools/Guiñote/\U0001f3d7 Construir Escena Principal", false, 10)]
        public static void ConstruirEscena()
        {
            if (!EditorUtility.DisplayDialog(
                    "Construir Escena Principal",
                    "Se creará/reconstruirá el Canvas de UI siguiendo el diseño de Claude Design.\n\n" +
                    "Cámara, Tapete, GameManager y CartaVisualManager se reutilizan si ya existen " +
                    "(no se toca su lógica ni sus referencias). El Canvas se reconstruye siempre desde cero.\n\n" +
                    "¿Continuar?",
                    "Construir", "Cancelar"))
                return;

            Undo.SetCurrentGroupName("Construir Escena Guiñote");
            int undoGroup = Undo.GetCurrentGroup();

            CrearCamara();
            CrearEventSystem();
            // El Tapete se reconstruye siempre (es configuracion visual pura): una ejecucion
            // anterior pudo haber quedado con el sprite integrado roto (ver CrearTapete).
            GameObject tapeteExistente = GameObject.Find("Tapete");
            if (tapeteExistente != null) Undo.DestroyObjectImmediate(tapeteExistente);
            CrearTapete();

            GameObject gmRoot  = GameObject.Find("GameManager_Root")  ?? CrearGameManagerRoot();
            // CrearCartaVisualManagerRoot reutiliza el root si ya existe, pero siempre
            // reaplica las posiciones de anclaje (layout), asi que se llama siempre.
            GameObject cvmRoot = CrearCartaVisualManagerRoot();

            CrearCanvas(gmRoot, cvmRoot);

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[SceneBuilder] ✅ Escena de Guiñote construida correctamente.");
            EditorUtility.DisplayDialog(
                "Escena construida",
                "✅ El Canvas se ha reconstruido siguiendo el diseño.\n\n" +
                "Recuerda: el Game view debe estar en una resolución horizontal " +
                "(el juego es landscape-only) para verlo correctamente.",
                "OK");
        }

        [MenuItem("Tools/Guiñote/\U0001f0cf Preparar Assets (Sprites → Resources)", false, 11)]
        public static void PrepararAssets()
        {
            const string origen  = "Assets/baraja_guinote/cards";
            const string destino = "Assets/Resources/Cards";

            if (!AssetDatabase.IsValidFolder(destino))
            {
                System.IO.Directory.CreateDirectory(
                    System.IO.Path.Combine(UnityEngine.Application.dataPath, "Resources/Cards"));
                AssetDatabase.Refresh();
            }

            string[] allFiles = System.IO.Directory.GetFiles(
                System.IO.Path.Combine(UnityEngine.Application.dataPath, "baraja_guinote/cards"), "*.svg");

            int copiados = 0;
            foreach (string fullPath in allFiles)
            {
                string fileName = System.IO.Path.GetFileName(fullPath);
                string assetSrc = $"{origen}/{fileName}";
                string assetDst = $"{destino}/{fileName}";

                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetDst) != null)
                    continue;

                bool ok = AssetDatabase.CopyAsset(assetSrc, assetDst);
                if (ok) copiados++;
                else Debug.LogWarning($"[SceneBuilder] No se pudo copiar {fileName}");
            }

            AssetDatabase.Refresh();
            Debug.Log($"[SceneBuilder] 🃏 {copiados} sprites copiados a Resources/Cards/");
            EditorUtility.DisplayDialog("Assets preparados", $"✅ {copiados} sprites copiados.", "OK");
        }

        // =====================================================================
        // Cámara principal
        // =====================================================================

        private static void CrearCamara()
        {
            GameObject camGO = GameObject.Find("Main Camera");
            if (camGO == null)
            {
                camGO = new GameObject("Main Camera");
                Undo.RegisterCreatedObjectUndo(camGO, "Crear Main Camera");
            }

            // Se reconfigura siempre (incluso si la cámara ya existía): es un juego 2D
            // ortográfico y una cámara por defecto de Unity (perspectiva + skybox) rompe
            // por completo la vista de la mesa. Esto es configuración visual pura, no
            // afecta a ninguna regla de juego.
            Camera cam = camGO.GetComponent<Camera>();
            if (cam == null) cam = camGO.AddComponent<Camera>();
            cam.orthographic     = true;
            cam.orthographicSize = 5f;
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.backgroundColor  = ColorTapete;
            cam.nearClipPlane    = -10f;
            cam.farClipPlane     = 100f;
            camGO.tag            = "MainCamera";
            camGO.transform.position = new Vector3(0f, 0f, -10f);
            camGO.transform.rotation = Quaternion.identity;

            if (camGO.GetComponent<AudioListener>() == null)
                camGO.AddComponent<AudioListener>();
        }

        // =====================================================================
        // EventSystem
        // =====================================================================

        private static void CrearEventSystem()
        {
            if (GameObject.Find("EventSystem") != null) return;

            GameObject esGO = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(esGO, "Crear EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }

        // =====================================================================
        // Tapete (SpriteRenderer, fuera del Canvas)
        // =====================================================================

        // Sprite blanco solido de 4x4 px, generado en memoria: evita depender del sprite
        // integrado "UI/Skin/UISprite.psd" (necesita el paquete "2D Sprite" para tejerse
        // en modo Full Rect; sin él, Unity genera una malla poligonal diminuta en su lugar).
        private static Sprite _spriteSolido;
        private static Sprite SpriteSolido()
        {
            if (_spriteSolido != null) return _spriteSolido;

            const int lado = 4;
            Texture2D tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[lado * lado];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();

            // pixelsPerUnit = lado → el sprite mide 1x1 unidad de mundo en modo Simple,
            // así transform.localScale controla el tamaño final directamente.
            _spriteSolido = Sprite.Create(tex, new Rect(0, 0, lado, lado), new Vector2(0.5f, 0.5f), lado);
            return _spriteSolido;
        }

        private static void CrearTapete()
        {
            GameObject tapete = new GameObject("Tapete");
            Undo.RegisterCreatedObjectUndo(tapete, "Crear Tapete");

            SpriteRenderer sr = tapete.AddComponent<SpriteRenderer>();
            sr.sprite    = SpriteSolido();
            sr.color     = ColorTapete;
            sr.drawMode  = SpriteDrawMode.Simple;
            // Muy por debajo de cualquier carta (que usan sortingOrder >= 1): a igual
            // sortingOrder el orden de dibujado es indefinido y el tapete opaco puede
            // tapar cartas por encima de el.
            sr.sortingOrder = -10;

            tapete.transform.localScale = new Vector3(18f, 10f, 1f);
            tapete.transform.position   = Vector3.zero;

            GameObject bordeSup = new GameObject("BordeSuperior");
            Undo.RegisterCreatedObjectUndo(bordeSup, "Crear BordeSuperior");
            bordeSup.transform.SetParent(tapete.transform, false);
            SpriteRenderer srSup = bordeSup.AddComponent<SpriteRenderer>();
            srSup.sprite = SpriteSolido();
            srSup.color  = ColorBorde;
            srSup.drawMode = SpriteDrawMode.Simple;
            srSup.sortingOrder = 1;
            bordeSup.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            bordeSup.transform.localScale    = new Vector3(1f, 0.03f, 1f);

            GameObject bordeInf = new GameObject("BordeInferior");
            Undo.RegisterCreatedObjectUndo(bordeInf, "Crear BordeInferior");
            bordeInf.transform.SetParent(tapete.transform, false);
            SpriteRenderer srInf = bordeInf.AddComponent<SpriteRenderer>();
            srInf.sprite = SpriteSolido();
            srInf.color  = ColorBorde;
            srInf.drawMode = SpriteDrawMode.Simple;
            srInf.sortingOrder = 1;
            bordeInf.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            bordeInf.transform.localScale    = new Vector3(1f, 0.03f, 1f);
        }

        // =====================================================================
        // GameManager Root
        // =====================================================================

        private static GameObject CrearGameManagerRoot()
        {
            GameObject root = new GameObject("GameManager_Root");
            Undo.RegisterCreatedObjectUndo(root, "Crear GameManager_Root");
            TryAddComponent<GuinoteApp.Managers.GameManager>(root);
            TryAddComponent<GuinoteApp.Managers.TurnManager>(root);
            return root;
        }

        // =====================================================================
        // CartaVisualManager Root + Anchors + BazaPositions
        // =====================================================================

        private static GameObject CrearCartaVisualManagerRoot()
        {
            GameObject root = GameObject.Find("CartaVisualManager_Root");
            if (root == null)
            {
                root = new GameObject("CartaVisualManager_Root");
                Undo.RegisterCreatedObjectUndo(root, "Crear CartaVisualManager_Root");
                TryAddComponent<GuinoteApp.Managers.CartaVisualManager>(root);
            }

            // Posiciones re-aplicadas siempre (aunque el root ya existiera): son layout
            // puro, calculado para no invadir la barra HUD superior (76px de 1080 de alto
            // de referencia → 0,70 unidades de mundo) ni el borde inferior del tapete,
            // con cámara ortográfica de tamaño 5 (rango visible Y: -5..5) y cartas de
            // ~1,4 x 2,12 unidades.
            // El turno avanza siempre incrementando el indice de jugador (0→1→2→3→0, ver
            // TurnManager.AvanzarTurno/RealizarRobo). Para que eso se vea en sentido
            // antihorario (J0 abajo → J1 derecha → J2 arriba → J3 izquierda), J1 se coloca
            // fisicamente a la derecha y J3 a la izquierda (los nombres de los anclajes se
            // mantienen "Izquierda/Derecha" por legibilidad del lado, no por indice de jugador).
            GameObject anchors = FindChildGO(root, "Anchors") ?? CrearHijo(root, "Anchors");
            // Tamaños de carta del diseño (ver CartaVisualManager): mano 1,03x1,47, baza 0,90x1,27,
            // rival 0,80x1,13. Los laterales se quedan en |x|=6 para no recortarse en 4:3.
            PosicionarAncla(anchors, "AnchorajeManoLocal",      new Vector3( 0.0f, -3.8f, 0f));
            PosicionarAncla(anchors, "AnchorajeManoArriba",     new Vector3( 0.0f,  3.4f, 0f));
            PosicionarAncla(anchors, "AnchorajeManoIzquierda",  new Vector3( 6.0f,  0.0f, 0f));
            PosicionarAncla(anchors, "Anchoraje ManoDerecha",   new Vector3(-6.0f,  0.0f, 0f));
            PosicionarAncla(anchors, "AnclajeTriunfo",          new Vector3( 0.8f,  0.0f, 0f));
            PosicionarAncla(anchors, "AnclajeMazo",             new Vector3(-0.8f,  0.0f, 0f));

            GameObject bazaPos = FindChildGO(root, "BazaPositions") ?? CrearHijo(root, "BazaPositions");
            PosicionarAncla(bazaPos, "PosCartaJ0", new Vector3( 0.0f, -1.5f, 0f));
            PosicionarAncla(bazaPos, "PosCartaJ1", new Vector3( 2.0f,  0.0f, 0f));
            PosicionarAncla(bazaPos, "PosCartaJ2", new Vector3( 0.0f,  1.5f, 0f));
            PosicionarAncla(bazaPos, "PosCartaJ3", new Vector3(-2.0f,  0.0f, 0f));

            return root;
        }

        private static void PosicionarAncla(GameObject parent, string nombre, Vector3 posicion)
        {
            GameObject go = FindChildGO(parent, nombre) ?? CrearHijo(parent, nombre);
            go.transform.localPosition = posicion;
        }

        // =====================================================================
        // Canvas + Paneles UI
        // =====================================================================

        private static void CrearCanvas(GameObject gmRoot, GameObject cvmRoot)
        {
            GameObject existente = GameObject.Find("Canvas");
            if (existente != null)
                Undo.DestroyObjectImmediate(existente);

            GameObject canvasGO = new GameObject("Canvas");
            Undo.RegisterCreatedObjectUndo(canvasGO, "Crear Canvas");

            Canvas canvas      = canvasGO.AddComponent<Canvas>();
            canvas.renderMode    = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera   = Camera.main;
            // El Tapete y las cartas (SpriteRenderer) estan a ~10 unidades de la camara.
            // Sin fijar esto, el Canvas usa su planeDistance por defecto (100): al estar
            // mas lejos que el tapete, se pinta antes en la cola transparente y el tapete
            // opaco lo tapa por completo. 1 unidad lo deja siempre delante de todo lo 3D.
            canvas.planeDistance = 1f;

            CanvasScaler scaler        = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();
            TryAddComponent<GuinoteApp.UI.UIManager>(canvasGO);

            GameObject panelInicio    = CrearPanelInicio(canvasGO);
            GameObject panelSeleccion = CrearPanelSeleccion(canvasGO);
            GameObject panelReglas    = CrearPanelReglas(canvasGO);
            GameObject panelMesa      = CrearPanelMesaDeJuego(canvasGO);

            panelSeleccion.SetActive(false);
            panelReglas.SetActive(false);
            panelMesa.SetActive(false);

            ConectarReferencias(canvasGO, gmRoot, cvmRoot,
                panelInicio, panelSeleccion, panelReglas, panelMesa);
        }

        // =====================================================================
        // Conexión automática de referencias (wiring)
        // =====================================================================

        private static void ConectarReferencias(
            GameObject canvasGO, GameObject gmRoot, GameObject cvmRoot,
            GameObject panelInicio, GameObject panelSeleccion,
            GameObject panelReglas, GameObject panelMesa)
        {
            var uiManager = canvasGO.GetComponent<GuinoteApp.UI.UIManager>();
            if (uiManager != null)
            {
                SerializedObject soUI = new SerializedObject(uiManager);
                soUI.FindProperty("panelInicio").objectReferenceValue      = panelInicio;
                soUI.FindProperty("panelSeleccion").objectReferenceValue   = panelSeleccion;
                soUI.FindProperty("panelReglas").objectReferenceValue      = panelReglas;
                soUI.FindProperty("panelMesaDeJuego").objectReferenceValue = panelMesa;

                GameObject tapete = GameObject.Find("Tapete");
                SerializedProperty objetos3D = soUI.FindProperty("objetosSoloMesaDeJuego");
                objetos3D.arraySize = tapete != null ? 1 : 0;
                if (tapete != null)
                    objetos3D.GetArrayElementAtIndex(0).objectReferenceValue = tapete;

                soUI.ApplyModifiedPropertiesWithoutUndo();
            }

            // ── PantallaInicio ──────────────────────────────────────────────
            var pantInicio = panelInicio.GetComponent<GuinoteApp.UI.PantallaInicio>();
            if (pantInicio != null)
            {
                SerializedObject so = new SerializedObject(pantInicio);
                so.FindProperty("uiManager").objectReferenceValue = uiManager;
                so.FindProperty("botonJugarIA").objectReferenceValue        = FindChildComponent<Button>(panelInicio, "Btn_Jugar");
                so.FindProperty("botonMultijugador").objectReferenceValue   = FindChildComponent<Button>(panelInicio, "Btn_Multijugador");
                so.FindProperty("botonComoSeJuega").objectReferenceValue    = FindChildComponent<Button>(panelInicio, "Btn_ComoSeJuega");
                so.FindProperty("textoLogo").objectReferenceValue          = FindChildComponent<TMPro.TMP_Text>(panelInicio, "Logo");
                so.FindProperty("textoEdicion").objectReferenceValue       = FindChildComponent<TMPro.TMP_Text>(panelInicio, "Subtitulo");
                so.FindProperty("textoJugador").objectReferenceValue       = FindChildComponent<TMPro.TMP_Text>(panelInicio, "Info");
                so.FindProperty("textoTitulo").objectReferenceValue        = FindChildComponent<TMPro.TMP_Text>(panelInicio, "Titulo");
                so.FindProperty("cartaHero1").objectReferenceValue         = FindChildComponent<Image>(panelInicio, "HeroCard1");
                so.FindProperty("cartaHero2").objectReferenceValue         = FindChildComponent<Image>(panelInicio, "HeroCard2");
                so.FindProperty("cartaHero3").objectReferenceValue         = FindChildComponent<Image>(panelInicio, "HeroCard3");
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // ── PantallaSeleccion ───────────────────────────────────────────
            var pantSel = panelSeleccion.GetComponent<GuinoteApp.UI.PantallaSeleccion>();
            if (pantSel != null)
            {
                SerializedObject so = new SerializedObject(pantSel);
                so.FindProperty("uiManager").objectReferenceValue = uiManager;
                so.FindProperty("textoTitulo").objectReferenceValue       = FindChildComponent<TMPro.TMP_Text>(panelSeleccion, "Titulo");
                so.FindProperty("botonContraIA").objectReferenceValue     = FindChildComponent<Button>(panelSeleccion, "Btn_ContraIA");
                so.FindProperty("botonMultijugador").objectReferenceValue = FindChildComponent<Button>(panelSeleccion, "Btn_Multijugador");
                so.FindProperty("fondoContraIA").objectReferenceValue     = FindChildComponent<Image>(panelSeleccion, "Btn_ContraIA");
                so.FindProperty("fondoMultijugador").objectReferenceValue = FindChildComponent<Image>(panelSeleccion, "Btn_Multijugador");
                so.FindProperty("botonFacil").objectReferenceValue        = FindChildComponent<Button>(panelSeleccion, "Btn_Facil");
                so.FindProperty("botonNormal").objectReferenceValue       = FindChildComponent<Button>(panelSeleccion, "Btn_Normal");
                so.FindProperty("botonDificil").objectReferenceValue      = FindChildComponent<Button>(panelSeleccion, "Btn_Dificil");
                so.FindProperty("fondoFacil").objectReferenceValue        = FindChildComponent<Image>(panelSeleccion, "Btn_Facil");
                so.FindProperty("fondoNormal").objectReferenceValue       = FindChildComponent<Image>(panelSeleccion, "Btn_Normal");
                so.FindProperty("fondoDificil").objectReferenceValue      = FindChildComponent<Image>(panelSeleccion, "Btn_Dificil");
                so.FindProperty("botonExperto").objectReferenceValue      = FindChildComponent<Button>(panelSeleccion, "Btn_Experto");
                so.FindProperty("fondoExperto").objectReferenceValue      = FindChildComponent<Image>(panelSeleccion, "Btn_Experto");
                so.FindProperty("botonComenzar").objectReferenceValue     = FindChildComponent<Button>(panelSeleccion, "Btn_Comenzar");
                so.FindProperty("botonVolver").objectReferenceValue       = FindChildComponent<Button>(panelSeleccion, "Btn_Volver");
                so.FindProperty("textoRival1").objectReferenceValue     = FindChildComponent<TMPro.TMP_Text>(panelSeleccion, "Nombre", "Card_Rival1");
                so.FindProperty("textoCompanero").objectReferenceValue  = FindChildComponent<TMPro.TMP_Text>(panelSeleccion, "Nombre", "Card_Companero");
                so.FindProperty("textoRival2").objectReferenceValue     = FindChildComponent<TMPro.TMP_Text>(panelSeleccion, "Nombre", "Card_Rival2");
                so.FindProperty("textoTu").objectReferenceValue         = FindChildComponent<TMPro.TMP_Text>(panelSeleccion, "Nombre", "Card_Tu");
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // ── PantallaReglas ──────────────────────────────────────────────
            var pantReglas = panelReglas.GetComponent<GuinoteApp.UI.PantallaReglas>();
            if (pantReglas != null)
            {
                SerializedObject so = new SerializedObject(pantReglas);
                so.FindProperty("uiManager").objectReferenceValue = uiManager;
                so.FindProperty("textoTitulo").objectReferenceValue         = FindChildComponent<TMPro.TMP_Text>(panelReglas, "Titulo");
                so.FindProperty("textoSubtitulo").objectReferenceValue      = FindChildComponent<TMPro.TMP_Text>(panelReglas, "Subtitulo");
                so.FindProperty("textoSeccionValores").objectReferenceValue = FindChildComponent<TMPro.TMP_Text>(panelReglas, "TituloValores");
                so.FindProperty("textoSeccionCantes").objectReferenceValue  = FindChildComponent<TMPro.TMP_Text>(panelReglas, "TituloCantes");
                so.FindProperty("textoLas20").objectReferenceValue          = FindChildComponent<TMPro.TMP_Text>(panelReglas, "Las20");
                so.FindProperty("textoLas40").objectReferenceValue          = FindChildComponent<TMPro.TMP_Text>(panelReglas, "Las40");
                so.FindProperty("textoArrastre").objectReferenceValue       = FindChildComponent<TMPro.TMP_Text>(panelReglas, "Arrastre");
                so.FindProperty("botonVolver").objectReferenceValue         = FindChildComponent<Button>(panelReglas, "Btn_Volver");

                SerializedProperty filas = so.FindProperty("filasValores");
                filas.arraySize = 6;
                for (int i = 0; i < 6; i++)
                    filas.GetArrayElementAtIndex(i).objectReferenceValue =
                        FindChildComponent<TMPro.TMP_Text>(panelReglas, $"Valor_{i}");

                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // ── HUDMesaDeJuego ──────────────────────────────────────────────
            var hud = panelMesa.GetComponent<GuinoteApp.UI.HUDMesaDeJuego>();
            if (hud != null)
            {
                SerializedObject so = new SerializedObject(hud);
                so.FindProperty("textoNosotros").objectReferenceValue       = FindChildComponent<TMPro.TMP_Text>(panelMesa, "LabelNosotros");
                so.FindProperty("textoEllos").objectReferenceValue          = FindChildComponent<TMPro.TMP_Text>(panelMesa, "LabelEllos");
                so.FindProperty("textoPuntosNosotros").objectReferenceValue = FindChildComponent<TMPro.TMP_Text>(panelMesa, "PuntosA");
                so.FindProperty("textoPuntosEllos").objectReferenceValue    = FindChildComponent<TMPro.TMP_Text>(panelMesa, "PuntosB");
                so.FindProperty("textoMazo").objectReferenceValue           = FindChildComponent<TMPro.TMP_Text>(panelMesa, "TextoMazo");
                so.FindProperty("textoCanteActivo").objectReferenceValue    = FindChildComponent<TMPro.TMP_Text>(panelMesa, "TextoCante");
                so.FindProperty("tagCanteContainer").objectReferenceValue   = FindChildGO(panelMesa, "Tag_Cante");
                so.FindProperty("panelFinPartida").objectReferenceValue     = FindChildGO(panelMesa, "Panel_FinPartida");
                so.FindProperty("textoResultado").objectReferenceValue      = FindChildComponent<TMPro.TMP_Text>(panelMesa, "Resultado");
                so.FindProperty("textoPuntosFinales").objectReferenceValue  = FindChildComponent<TMPro.TMP_Text>(panelMesa, "PuntosFinales");
                so.FindProperty("botonVolverAlMenu").objectReferenceValue   = FindChildComponent<Button>(panelMesa, "Btn_VolverMenu");
                so.FindProperty("uiManager").objectReferenceValue           = uiManager;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // ── GameManager ──────────────────────────────────────────────────
            var gm = gmRoot.GetComponent<GuinoteApp.Managers.GameManager>();
            if (gm != null)
            {
                SerializedObject so = new SerializedObject(gm);
                so.FindProperty("cartaVisualManager").objectReferenceValue = cvmRoot.GetComponent<GuinoteApp.Managers.CartaVisualManager>();
                so.FindProperty("turnManager").objectReferenceValue        = gmRoot.GetComponent<GuinoteApp.Managers.TurnManager>();
                so.FindProperty("uiManager").objectReferenceValue          = uiManager;
                so.FindProperty("hudMesa").objectReferenceValue            = hud;
                so.FindProperty("pantallaSeleccion").objectReferenceValue  = pantSel;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // ── CartaVisualManager ───────────────────────────────────────────
            var cvm = cvmRoot.GetComponent<GuinoteApp.Managers.CartaVisualManager>();
            if (cvm != null)
            {
                SerializedObject so = new SerializedObject(cvm);

                var cartaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CartaPrefab.prefab");
                if (cartaPrefab != null)
                    so.FindProperty("cartaPrefab").objectReferenceValue = cartaPrefab;
                else
                    Debug.LogWarning("[SceneBuilder] No se encontró Assets/Prefabs/CartaPrefab.prefab " +
                        "(Tools → Guiñote → 📦 Crear CartaPrefab para generarlo).");

                so.FindProperty("puntoAnclajeManoPrincipal").objectReferenceValue   = FindChildTransform(cvmRoot, "AnchorajeManoLocal");
                so.FindProperty("puntoAnclajeManoArriba").objectReferenceValue      = FindChildTransform(cvmRoot, "AnchorajeManoArriba");
                so.FindProperty("puntoAnclajeManoIzquierda").objectReferenceValue   = FindChildTransform(cvmRoot, "AnchorajeManoIzquierda");
                so.FindProperty("puntoAnclajeManoDerecha").objectReferenceValue     = FindChildTransform(cvmRoot, "Anchoraje ManoDerecha");
                so.FindProperty("puntoAnclajeTriunfo").objectReferenceValue         = FindChildTransform(cvmRoot, "AnclajeTriunfo");
                so.FindProperty("puntoAnclajeMazo").objectReferenceValue            = FindChildTransform(cvmRoot, "AnclajeMazo");
                so.FindProperty("posCartaJugadaJ0").objectReferenceValue            = FindChildTransform(cvmRoot, "PosCartaJ0");
                so.FindProperty("posCartaJugadaJ1").objectReferenceValue            = FindChildTransform(cvmRoot, "PosCartaJ1");
                so.FindProperty("posCartaJugadaJ2").objectReferenceValue            = FindChildTransform(cvmRoot, "PosCartaJ2");
                so.FindProperty("posCartaJugadaJ3").objectReferenceValue            = FindChildTransform(cvmRoot, "PosCartaJ3");

                var deckJson = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/baraja_guinote/deck.json");
                if (deckJson != null)
                    so.FindProperty("deckJson").objectReferenceValue = deckJson;
                else
                    Debug.LogWarning("[SceneBuilder] No se encontró deck.json en Assets/baraja_guinote/");

                so.ApplyModifiedPropertiesWithoutUndo();
            }

            Debug.Log("[SceneBuilder] Referencias conectadas.");
        }

        // =====================================================================
        // Helpers de búsqueda
        // =====================================================================

        private static T FindChildComponent<T>(GameObject root, string nombre, string parentName = null) where T : Component
        {
            T[] all = root.GetComponentsInChildren<T>(true);
            foreach (T c in all)
            {
                if (c.gameObject.name != nombre) continue;
                if (parentName != null && c.transform.parent != null && c.transform.parent.name != parentName) continue;
                return c;
            }
            return null;
        }

        private static Transform FindChildTransform(GameObject root, string nombre)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in all)
                if (t.name == nombre) return t;
            return null;
        }

        private static GameObject FindChildGO(GameObject root, string nombre)
        {
            Transform t = FindChildTransform(root, nombre);
            return t != null ? t.gameObject : null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Panel_Inicio — mockup: header (logo+edición), info jugador, cuerpo
        // (título + botones a la izquierda, 3 cartas en abanico a la derecha)
        // ─────────────────────────────────────────────────────────────────────

        private static GameObject CrearPanelInicio(GameObject canvas)
        {
            GameObject panel = CrearPanelCompleto(canvas, "Panel_Inicio", ColorBg);
            TryAddComponent<GuinoteApp.UI.PantallaInicio>(panel);

            // Header: logo (izq) + "Edición familiar" (der), separados por línea inferior
            Texto(panel, "Logo", "GUIÑOTE", 30, FontStyles.Bold | FontStyles.UpperCase, ColorTexto,
                Anchor.TopLeft, new Vector2(64f, 48f), new Vector2(400f, 44f), TextAlignmentOptions.BottomLeft);
            Texto(panel, "Subtitulo", "Edición familiar", 18, FontStyles.Italic, ColorNeutral700,
                Anchor.TopRight, new Vector2(64f, 48f), new Vector2(300f, 44f), TextAlignmentOptions.BottomRight);
            Linea(panel, "HeaderDivider", ColorTexto, 3f, Anchor.TopStretch, 64f, 96f);

            // Info jugador
            Texto(panel, "Info", "Jugador: 1 · Baraja española", 15, FontStyles.Normal, ColorNeutral700,
                Anchor.TopLeft, new Vector2(64f, 114f), new Vector2(700f, 28f), TextAlignmentOptions.TopLeft);
            Linea(panel, "InfoDivider", ColorNeutral400, 1f, Anchor.TopStretch, 64f, 146f);

            // Título grande, columna izquierda
            Texto(panel, "Titulo", "El juego de\ncartas de\nsiempre", 52, FontStyles.Bold, ColorTexto,
                Anchor.CenterLeft, new Vector2(64f, 90f), new Vector2(560f, 210f), TextAlignmentOptions.MidlineLeft);

            // Botones
            Boton(panel, "Btn_Jugar", "Jugar con la IA", ColorAccent, ColorBg, false,
                Anchor.CenterLeft, new Vector2(64f, -120f), new Vector2(210f, 52f));
            Boton(panel, "Btn_Multijugador", "Multijugador", Color.clear, ColorTexto, true,
                Anchor.CenterLeft, new Vector2(284f, -120f), new Vector2(190f, 52f));
            var comoSeJuega = Boton(panel, "Btn_ComoSeJuega", "Cómo se juega →", Color.clear, ColorAccent, false,
                Anchor.CenterLeft, new Vector2(64f, -186f), new Vector2(220f, 36f));
            comoSeJuega.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;

            // Cartas en abanico, columna derecha — sprites reales de la baraja
            CrearImagenCarta(panel, "HeroCard1", "oros-1",       -8f, new Vector2(450f,  -6f));
            CrearImagenCarta(panel, "HeroCard2", "copas-rey",     2f, new Vector2(340f, -20f));
            CrearImagenCarta(panel, "HeroCard3", "espadas-sota", 11f, new Vector2(230f,  -6f));

            return panel;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Panel_Seleccion — título + seg.control modo, grid 3x2 de jugadores,
        // seg.control dificultad + botón comenzar
        // ─────────────────────────────────────────────────────────────────────

        private static GameObject CrearPanelSeleccion(GameObject canvas)
        {
            GameObject panel = CrearPanelCompleto(canvas, "Panel_Seleccion", ColorBg);
            TryAddComponent<GuinoteApp.UI.PantallaSeleccion>(panel);

            Boton(panel, "Btn_Volver", "← Volver", Color.clear, ColorAccent, false,
                Anchor.TopLeft, new Vector2(48f, 16f), new Vector2(120f, 28f));

            Texto(panel, "Titulo", "Elige tu mesa", 36, FontStyles.Bold, ColorTexto,
                Anchor.TopLeft, new Vector2(64f, 72f), new Vector2(500f, 44f), TextAlignmentOptions.BottomLeft);

            Boton(panel, "Btn_ContraIA", "Contra la IA", ColorAccent, ColorBg, false,
                Anchor.TopRight, new Vector2(268f, 72f), new Vector2(180f, 44f));
            Boton(panel, "Btn_Multijugador", "Multijugador", Color.clear, ColorTexto, true,
                Anchor.TopRight, new Vector2(64f, 72f), new Vector2(180f, 44f));
            Linea(panel, "HeaderDivider", ColorTexto, 3f, Anchor.TopStretch, 64f, 132f);

            // Grid 3x2 de jugadores (centrado)
            float cellW = 260f, cellH = 210f, gap = 24f;
            float gridW = cellW * 3f + gap * 2f;
            float startX = -gridW / 2f;
            string[,] nombres = { { "Card_Rival1", "Card_Companero", "Card_Rival2" }, { "", "Card_Tu", "" } };
            Color[,] colores  = { { ColorTexto, ColorAccent, ColorTexto }, { ColorTexto, ColorAccent2, ColorTexto } };
            string[,] textos  = { { "Rival · Normal", "+ Elegir compañero", "Rival · Normal" }, { "", "TÚ", "" } };

            for (int fila = 0; fila < 2; fila++)
            {
                for (int col = 0; col < 3; col++)
                {
                    if (nombres[fila, col] == "") continue;
                    float x = startX + col * (cellW + gap) + cellW / 2f;
                    float y = fila == 0 ? 60f : 60f - (cellH + gap);
                    bool esCompanero = nombres[fila, col] == "Card_Companero";
                    CrearCardJugador(panel, nombres[fila, col], textos[fila, col], colores[fila, col],
                        new Vector2(x, y), new Vector2(cellW, cellH), esCompanero);
                }
            }

            Linea(panel, "FooterDivider", ColorNeutral400, 1f, Anchor.BottomStretch, 64f, 124f);

            Boton(panel, "Btn_Facil", "Fácil", Color.clear, ColorTexto, true,
                Anchor.BottomLeft, new Vector2(64f, 44f), new Vector2(120f, 44f));
            Boton(panel, "Btn_Normal", "Normal", ColorAccent, ColorBg, false,
                Anchor.BottomLeft, new Vector2(192f, 44f), new Vector2(120f, 44f));
            Boton(panel, "Btn_Dificil", "Difícil", Color.clear, ColorTexto, true,
                Anchor.BottomLeft, new Vector2(320f, 44f), new Vector2(120f, 44f));
            Boton(panel, "Btn_Experto", "Experto", Color.clear, ColorTexto, true,
                Anchor.BottomLeft, new Vector2(448f, 44f), new Vector2(120f, 44f));

            Boton(panel, "Btn_Comenzar", "Comenzar partida", ColorAccent, ColorBg, false,
                Anchor.BottomRight, new Vector2(64f, 44f), new Vector2(240f, 52f));

            return panel;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Panel_Reglas — 2 columnas: valores de cartas / cantes y arrastre.
        // El TEXTO real lo rellena PantallaReglas.cs en runtime (valores
        // correctos de este proyecto); aquí solo se crean los contenedores.
        // ─────────────────────────────────────────────────────────────────────

        private static GameObject CrearPanelReglas(GameObject canvas)
        {
            GameObject panel = CrearPanelCompleto(canvas, "Panel_Reglas", ColorBg);
            TryAddComponent<GuinoteApp.UI.PantallaReglas>(panel);

            Boton(panel, "Btn_Volver", "← Volver", Color.clear, ColorAccent, false,
                Anchor.TopLeft, new Vector2(48f, 40f), new Vector2(140f, 36f));

            Texto(panel, "Titulo", "Cómo se juega", 32, FontStyles.Bold, ColorTexto,
                Anchor.TopLeft, new Vector2(64f, 96f), new Vector2(700f, 44f), TextAlignmentOptions.BottomLeft);
            Texto(panel, "Subtitulo", "Dos parejas enfrentadas · gana el primer equipo en llegar a 101 puntos",
                15, FontStyles.Normal, ColorNeutral700,
                Anchor.TopLeft, new Vector2(64f, 140f), new Vector2(900f, 30f), TextAlignmentOptions.TopLeft);
            Linea(panel, "HeaderDivider", ColorTexto, 3f, Anchor.TopStretch, 64f, 172f);

            // Columna izquierda — valores de cartas
            GameObject leftCol = CrearHijoUI(panel, "LeftColumn");
            Estirar(leftCol, new Vector2(0f, 0f), new Vector2(0.52f, 1f), 64f, 210f, 24f, 64f);

            Texto(leftCol, "TituloValores", "VALOR DE LAS CARTAS", 14, FontStyles.Bold | FontStyles.UpperCase, ColorAccent700,
                Anchor.TopStretch, new Vector2(0f, 0f), new Vector2(0f, 28f), TextAlignmentOptions.TopLeft);

            string[] glifos = { "A", "3", "R", "S", "C", "·" };
            for (int i = 0; i < 6; i++)
            {
                GameObject fila = CrearHijoUI(leftCol, $"FilaValor_{i}");
                Colocar(fila, Anchor.TopStretch, new Vector2(0f, 40f + i * 46f), new Vector2(0f, 40f));

                GameObject badge = CrearHijoUI(fila, "Badge");
                Colocar(badge, Anchor.CenterLeft, Vector2.zero, new Vector2(34f, 34f));
                var imgBadge = badge.AddComponent<Image>();
                imgBadge.color = ColorSurface;
                var tmpBadge = Texto(badge, "Glifo", glifos[i], 15, FontStyles.Bold, ColorAccent700,
                    Anchor.Fill, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);

                Texto(fila, $"Valor_{i}", "", 15, FontStyles.Normal, ColorTexto,
                    Anchor.CenterLeft, new Vector2(48f, 0f), new Vector2(360f, 34f), TextAlignmentOptions.MidlineLeft);
            }

            // Columna derecha — cantes / arrastre
            GameObject rightCol = CrearHijoUI(panel, "RightColumn");
            Estirar(rightCol, new Vector2(0.52f, 0f), new Vector2(1f, 1f), 24f, 210f, 64f, 64f);

            Texto(rightCol, "TituloCantes", "CANTES", 14, FontStyles.Bold | FontStyles.UpperCase, ColorAccent700,
                Anchor.TopStretch, new Vector2(0f, 0f), new Vector2(0f, 28f), TextAlignmentOptions.TopLeft);
            Texto(rightCol, "Las20", "", 15, FontStyles.Normal, ColorTexto,
                Anchor.TopStretch, new Vector2(0f, 48f), new Vector2(0f, 56f), TextAlignmentOptions.TopLeft);
            Texto(rightCol, "Las40", "", 15, FontStyles.Normal, ColorTexto,
                Anchor.TopStretch, new Vector2(0f, 112f), new Vector2(0f, 56f), TextAlignmentOptions.TopLeft);
            Texto(rightCol, "Arrastre", "", 15, FontStyles.Normal, ColorTexto,
                Anchor.TopStretch, new Vector2(0f, 176f), new Vector2(0f, 72f), TextAlignmentOptions.TopLeft);

            return panel;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Panel_MesaDeJuego — overlay transparente sobre el Tapete (world space):
        // barra HUD superior + panel de fin de partida.
        // ─────────────────────────────────────────────────────────────────────

        private static GameObject CrearPanelMesaDeJuego(GameObject canvas)
        {
            GameObject panel = CrearPanelCompleto(canvas, "Panel_MesaDeJuego", Color.clear);
            TryAddComponent<GuinoteApp.UI.HUDMesaDeJuego>(panel);

            GameObject hudHeader = CrearHijoUI(panel, "HUD_Header");
            Colocar(hudHeader, Anchor.TopStretch, new Vector2(0f, 0f), new Vector2(0f, 76f));
            Image imgHeader = hudHeader.AddComponent<Image>();
            imgHeader.color = new Color(0.125f, 0.118f, 0.114f, 0.85f);

            GameObject scoreLeft = CrearHijoUI(hudHeader, "ScoreLeft");
            Colocar(scoreLeft, Anchor.CenterLeft, new Vector2(32f, 0f), new Vector2(160f, 60f));
            Texto(scoreLeft, "LabelNosotros", "Nosotros", 14, FontStyles.Bold, Color.white,
                Anchor.TopStretch, Vector2.zero, new Vector2(0f, 22f), TextAlignmentOptions.TopLeft);
            Texto(scoreLeft, "PuntosA", "0", 26, FontStyles.Bold, ColorAccent,
                Anchor.BottomStretch, Vector2.zero, new Vector2(0f, 30f), TextAlignmentOptions.BottomLeft);

            GameObject scoreRight = CrearHijoUI(hudHeader, "ScoreRight");
            Colocar(scoreRight, Anchor.CenterLeft, new Vector2(220f, 0f), new Vector2(160f, 60f));
            Texto(scoreRight, "LabelEllos", "Ellos", 14, FontStyles.Bold, Color.white,
                Anchor.TopStretch, Vector2.zero, new Vector2(0f, 22f), TextAlignmentOptions.TopLeft);
            Texto(scoreRight, "PuntosB", "0", 26, FontStyles.Bold, ColorAccent2,
                Anchor.BottomStretch, Vector2.zero, new Vector2(0f, 30f), TextAlignmentOptions.BottomLeft);

            GameObject tagCante = CrearHijoUI(hudHeader, "Tag_Cante");
            Colocar(tagCante, Anchor.Center, Vector2.zero, new Vector2(440f, 40f)); // tambien muestra avisos largos
            Image imgCante = tagCante.AddComponent<Image>();
            imgCante.color = new Color(ColorAccent.r, ColorAccent.g, ColorAccent.b, 0.2f);
            var outlineCante = tagCante.AddComponent<Outline>();
            outlineCante.effectColor = ColorAccent;
            outlineCante.effectDistance = new Vector2(1f, -1f);
            Texto(tagCante, "TextoCante", "¡Las 40 en bastos!", 15, FontStyles.Bold, Color.white,
                Anchor.Fill, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
            tagCante.SetActive(false);

            GameObject tagMazo = CrearHijoUI(hudHeader, "Tag_Mazo");
            Colocar(tagMazo, Anchor.CenterRight, new Vector2(32f, 0f), new Vector2(190f, 40f));
            Image imgMazo = tagMazo.AddComponent<Image>();
            imgMazo.color = ColorSurface;
            Texto(tagMazo, "TextoMazo", "Mazo: 40", 14, FontStyles.Normal, ColorTexto,
                Anchor.Fill, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);

            // Panel de fin de partida — overlay oscuro centrado, oculto por defecto
            GameObject finPartida = CrearPanelCompleto(panel, "Panel_FinPartida", new Color(0.125f, 0.118f, 0.114f, 0.92f));
            Texto(finPartida, "Resultado", "¡Victoria!", 52, FontStyles.Bold, Color.white,
                Anchor.Center, new Vector2(0f, 40f), new Vector2(600f, 70f), TextAlignmentOptions.Center);
            Texto(finPartida, "PuntosFinales", "Nosotros 75 — Ellos 45", 22, FontStyles.Normal, Color.white,
                Anchor.Center, new Vector2(0f, -20f), new Vector2(600f, 40f), TextAlignmentOptions.Center);
            Boton(finPartida, "Btn_VolverMenu", "Volver al menú", ColorAccent, ColorBg, false,
                Anchor.Center, new Vector2(0f, -90f), new Vector2(240f, 52f));
            finPartida.SetActive(false);

            return panel;
        }

        // =====================================================================
        // Sistema de anclaje (equivalente a posicionamiento CSS: anchor+offset+size)
        // =====================================================================

        private readonly struct AnchorPoint
        {
            public readonly Vector2 Min, Max, Pivot;
            public readonly bool StretchX, StretchY;
            public AnchorPoint(Vector2 min, Vector2 max, Vector2 pivot, bool sx = false, bool sy = false)
            { Min = min; Max = max; Pivot = pivot; StretchX = sx; StretchY = sy; }
        }

        private static class Anchor
        {
            public static readonly AnchorPoint TopLeft      = new AnchorPoint(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            public static readonly AnchorPoint TopRight     = new AnchorPoint(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            public static readonly AnchorPoint TopStretch   = new AnchorPoint(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), true, false);
            public static readonly AnchorPoint BottomLeft   = new AnchorPoint(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
            public static readonly AnchorPoint BottomRight  = new AnchorPoint(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
            public static readonly AnchorPoint BottomStretch= new AnchorPoint(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), true, false);
            public static readonly AnchorPoint CenterLeft   = new AnchorPoint(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            public static readonly AnchorPoint CenterRight  = new AnchorPoint(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            public static readonly AnchorPoint Center       = new AnchorPoint(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            public static readonly AnchorPoint Fill         = new AnchorPoint(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), true, true);
        }

        /// <summary>Extremos (min,max) de un eje en coordenadas offsetMin/offsetMax, para un
        /// punto de anclaje con pivote en 0 (borde inicial), 1 (borde final) o 0.5 (centro).
        /// offset siempre es una distancia positiva hacia DENTRO del padre desde ese borde
        /// (o un desplazamiento con signo desde el centro).</summary>
        private static (float min, float max) EjeOffset(float pivot, float offset, float size)
        {
            if (pivot <= 0.01f) return (offset, offset + size);              // borde inicial (izq/abajo)
            if (pivot >= 0.99f) return (-offset - size, -offset);            // borde final (der/arriba)
            return (offset - size / 2f, offset + size / 2f);                 // centro
        }

        /// <summary>Posiciona un elemento por punto de anclaje + offset (px, positivo = hacia
        /// dentro del padre desde ese borde) + tamaño. En los ejes "Stretch" offset es el
        /// margen simétrico entre los dos bordes de esa anchura/altura.
        /// Calcula offsetMin/offsetMax directamente (no toca anchoredPosition/sizeDelta),
        /// así que no hay lecturas de valores intermedios a medio escribir.</summary>
        private static RectTransform Colocar(GameObject go, AnchorPoint anchor, Vector2 offset, Vector2 size)
        {
            RectTransform rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rt.anchorMin = anchor.Min;
            rt.anchorMax = anchor.Max;
            rt.pivot     = anchor.Pivot;

            float minX, maxX, minY, maxY;

            if (anchor.StretchX) { minX = offset.x; maxX = -offset.x; }
            else { (minX, maxX) = EjeOffset(anchor.Pivot.x, offset.x, size.x); }

            if (anchor.StretchY) { minY = offset.y; maxY = -offset.y; }
            else { (minY, maxY) = EjeOffset(anchor.Pivot.y, offset.y, size.y); }

            rt.offsetMin = new Vector2(minX, minY);
            rt.offsetMax = new Vector2(maxX, maxY);

            return rt;
        }

        /// <summary>Estira un elemento entre dos anclas con márgenes fijos en px (izq, arriba, der, abajo).</summary>
        private static RectTransform Estirar(GameObject go, Vector2 anchorMin, Vector2 anchorMax,
            float left, float top, float right, float bottom)
        {
            RectTransform rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        // =====================================================================
        // Utilidades — creación de GameObjects
        // =====================================================================

        private static GameObject CrearPanelCompleto(GameObject parent, string nombre, Color color)
        {
            GameObject go = new GameObject(nombre);
            Undo.RegisterCreatedObjectUndo(go, $"Crear {nombre}");
            go.transform.SetParent(parent.transform, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            if (color.a > 0f)
            {
                Image img = go.AddComponent<Image>();
                img.color = color;
            }

            return go;
        }

        private static GameObject CrearHijoUI(GameObject parent, string nombre)
        {
            GameObject go = new GameObject(nombre);
            Undo.RegisterCreatedObjectUndo(go, $"Crear {nombre}");
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static GameObject CrearHijo(GameObject parent, string nombre)
        {
            GameObject go = new GameObject(nombre);
            Undo.RegisterCreatedObjectUndo(go, $"Crear {nombre}");
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static void CrearAncla(GameObject parent, string nombre, Vector3 posicion)
        {
            GameObject go = CrearHijo(parent, nombre);
            go.transform.localPosition = posicion;
        }

        private static TextMeshProUGUI Texto(GameObject parent, string nombre, string texto,
            float tamano, FontStyles estilo, Color color, AnchorPoint anchor, Vector2 offset, Vector2 size,
            TextAlignmentOptions align)
        {
            GameObject go = CrearHijoUI(parent, nombre);
            Colocar(go, anchor, offset, size);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text          = texto;
            tmp.fontSize      = tamano;
            tmp.fontStyle     = estilo;
            tmp.color         = color;
            tmp.alignment     = align;
            tmp.raycastTarget = false;
            if (Fuente != null) tmp.font = Fuente;
            return tmp;
        }

        private static void Linea(GameObject parent, string nombre, Color color, float grosor,
            AnchorPoint anchor, float margenIzqDer, float offsetY)
        {
            GameObject go = CrearHijoUI(parent, nombre);
            Colocar(go, anchor, new Vector2(margenIzqDer, offsetY), new Vector2(0f, grosor));
            Image img = go.AddComponent<Image>();
            img.color = color;
        }

        private static Button Boton(GameObject parent, string nombre, string label,
            Color bgColor, Color textColor, bool borde, AnchorPoint anchor, Vector2 offset, Vector2 size)
        {
            GameObject go = CrearHijoUI(parent, nombre);
            Colocar(go, anchor, offset, size);

            Image img = go.AddComponent<Image>();
            img.color = bgColor.a > 0f ? bgColor : new Color(1f, 1f, 1f, 0.001f); // transparente pero clicable

            if (borde)
            {
                Outline outline        = go.AddComponent<Outline>();
                outline.effectColor    = ColorDivider;
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }

            Button btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor      = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.pressedColor     = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor    = new Color(1f, 1f, 1f, 0.45f);
            btn.colors = colors;

            GameObject labelGO = CrearHijoUI(go, "Label");
            Estirar(labelGO, Vector2.zero, Vector2.one, 6f, 4f, 6f, 4f);
            TextMeshProUGUI tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text          = label;
            tmp.fontSize      = 16f;
            tmp.fontStyle     = FontStyles.Bold;
            tmp.color         = textColor;
            tmp.alignment     = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            if (Fuente != null) tmp.font = Fuente;

            return btn;
        }

        private static void CrearImagenCarta(GameObject parent, string nombre, string spriteName, float rotacion, Vector2 offset)
        {
            GameObject go = CrearHijoUI(parent, nombre);
            Colocar(go, Anchor.CenterRight, offset, new Vector2(150f, 216f));
            go.transform.localEulerAngles = new Vector3(0f, 0f, rotacion);
            Image img = go.AddComponent<Image>();
            img.preserveAspect = true;
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Resources/Cards/{spriteName}.png");
            if (sprite != null) img.sprite = sprite;
            else Debug.LogWarning($"[SceneBuilder] No se encontró el sprite {spriteName} para {nombre}.");
        }

        private static void CrearCardJugador(GameObject parent, string nombre, string texto, Color color,
            Vector2 offset, Vector2 size, bool discontinuo)
        {
            GameObject go = CrearHijoUI(parent, nombre);
            Colocar(go, Anchor.Center, offset, size);
            Image img = go.AddComponent<Image>();
            img.color = discontinuo ? new Color(0f, 0f, 0f, 0f) : ColorSurface;

            if (discontinuo)
            {
                Outline outline = go.AddComponent<Outline>();
                outline.effectColor = ColorAccent;
                outline.effectDistance = new Vector2(2f, -2f);
            }

            GameObject avatar = CrearHijoUI(go, "Avatar");
            Colocar(avatar, Anchor.Center, new Vector2(0f, 24f), new Vector2(52f, 52f));
            Image imgAvatar = avatar.AddComponent<Image>();
            imgAvatar.color = discontinuo ? new Color(0f, 0f, 0f, 0f) : ColorAccent2_200;

            Texto(go, "Nombre", texto, 14, FontStyles.Bold, color,
                Anchor.BottomStretch, new Vector2(8f, 16f), new Vector2(0f, 24f), TextAlignmentOptions.Center);
        }

        // ─ AddComponent seguro ───────────────────────────────────────────────

        private static void TryAddComponent<T>(GameObject go) where T : Component
        {
            try   { go.AddComponent<T>(); }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SceneBuilder] No se pudo añadir {typeof(T).Name}: {e.Message}");
            }
        }
    }
}
