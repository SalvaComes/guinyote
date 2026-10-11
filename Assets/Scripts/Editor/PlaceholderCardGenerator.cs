using UnityEngine;
using UnityEditor;
using System.IO;
using GuinoteApp.Core;
using GuinoteApp.View;

namespace GuinoteApp.Editor
{
    /// <summary>
    /// Herramienta de editor que genera 41 sprites placeholder para la baraja espanola:
    /// 40 caras (una por carta) + 1 dorso.
    /// Ejecutar desde: Guinote > Generar Sprites Placeholder
    /// </summary>
    public static class PlaceholderCardGenerator
    {
        private const int CARD_WIDTH = 140;
        private const int CARD_HEIGHT = 200;
        private const int CORNER_RADIUS = 12;
        private const string OUTPUT_PATH = "Assets/Resources/Cards";

        // Colores por palo
        private static readonly Color COL_OROS     = new Color(1.00f, 0.78f, 0.10f);
        private static readonly Color COL_COPAS    = new Color(0.85f, 0.15f, 0.20f);
        private static readonly Color COL_ESPADAS  = new Color(0.20f, 0.40f, 0.85f);
        private static readonly Color COL_BASTOS   = new Color(0.15f, 0.65f, 0.30f);
        private static readonly Color COL_BG       = new Color(0.98f, 0.96f, 0.90f);
        private static readonly Color COL_DORSO    = new Color(0.12f, 0.18f, 0.38f);

        [MenuItem("Tools/Gui\u00f1ote/\U0001f4e6 Crear CartaPrefab", false, 20)]
        public static void CrearCartaPrefab()
        {
            const string prefabDir  = "Assets/Prefabs";
            const string prefabPath = "Assets/Prefabs/CartaPrefab.prefab";

            if (!Directory.Exists(prefabDir))
                Directory.CreateDirectory(prefabDir);

            if (File.Exists(prefabPath))
            {
                EditorUtility.DisplayDialog("Prefab ya existe",
                    $"Ya existe un prefab en:\n{prefabPath}\n\nBórralo primero si quieres regenerarlo.", "OK");
                return;
            }

            // Crear GO temporal
            GameObject go = new GameObject("CartaPrefab");

            // SpriteRenderer
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            // Crear sorting layer "Cards" si no existe (requiere serialización)
            sr.sortingLayerName = "Default"; // cambiar a "Cards" manualmente si el layer existe
            sr.sortingOrder     = 0;

            // BoxCollider2D
            BoxCollider2D col  = go.AddComponent<BoxCollider2D>();
            col.size           = new Vector2(1f, 1.4f);
            col.isTrigger      = true;

            // CardView
            go.AddComponent<CardView>();

            // Guardar como prefab
            bool success;
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath, out success);
            Object.DestroyImmediate(go);

            AssetDatabase.Refresh();

            if (success)
            {
                Debug.Log($"[PlaceholderCardGenerator] Prefab creado en {prefabPath}");
                EditorUtility.DisplayDialog("Prefab creado",
                    $"\u2705 CartaPrefab.prefab creado en:\n{prefabPath}\n\n" +
                    "Recuerda asignarlo en CartaVisualManager > cartaPrefab.", "OK");

                // Seleccionar el asset recién creado
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }
            else
            {
                Debug.LogError("[PlaceholderCardGenerator] Error al crear el prefab.");
            }
        }

        [MenuItem("Tools/Gui\u00f1ote/\U0001f5bc Generar Sprites Placeholder", false, 21)]
        public static void GenerarTodos()
        {
            if (!Directory.Exists(OUTPUT_PATH))
                Directory.CreateDirectory(OUTPUT_PATH);

            int generadas = 0;

            foreach (Palo palo in System.Enum.GetValues(typeof(Palo)))
            {
                foreach (Rango rango in System.Enum.GetValues(typeof(Rango)))
                {
                    string nombre = $"{palo.ToString().ToLower()}_{rango.ToString().ToLower()}";
                    string filePath = $"{OUTPUT_PATH}/{nombre}.png";

                    if (!File.Exists(filePath))
                    {
                        Texture2D tex = GenerarTexturaCara(palo, rango);
                        File.WriteAllBytes(filePath, tex.EncodeToPNG());
                        Object.DestroyImmediate(tex);
                        generadas++;
                    }
                }
            }

            // Dorso
            string dorsoPath = $"{OUTPUT_PATH}/dorso.png";
            if (!File.Exists(dorsoPath))
            {
                Texture2D texDorso = GenerarTexturaDorso();
                File.WriteAllBytes(dorsoPath, texDorso.EncodeToPNG());
                Object.DestroyImmediate(texDorso);
                generadas++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[PlaceholderCardGenerator] {generadas} sprites generados en {OUTPUT_PATH}");
            EditorUtility.DisplayDialog("Sprites generados", $"{generadas} sprites creados en\n{OUTPUT_PATH}", "OK");
        }

        // =====================================================================
        // Generacion de texturas
        // =====================================================================

        private static Texture2D GenerarTexturaCara(Palo palo, Rango rango)
        {
            Texture2D tex = new Texture2D(CARD_WIDTH, CARD_HEIGHT, TextureFormat.RGBA32, false);
            Color paloColor = ObtenerColorPalo(palo);

            // Fondo blanco/crema con borde redondeado
            PintarFondo(tex, COL_BG, paloColor, CORNER_RADIUS);

            // Franja de color del palo en la parte superior
            PintarFranja(tex, paloColor, CARD_HEIGHT - 45, CARD_HEIGHT, CORNER_RADIUS);

            // Franja inferior (espejo)
            PintarFranja(tex, paloColor, 0, 45, 0);

            tex.Apply();
            return tex;
        }

        private static Texture2D GenerarTexturaDorso()
        {
            Texture2D tex = new Texture2D(CARD_WIDTH, CARD_HEIGHT, TextureFormat.RGBA32, false);

            for (int y = 0; y < CARD_HEIGHT; y++)
            {
                for (int x = 0; x < CARD_WIDTH; x++)
                {
                    bool enEsquina = EsEsquinaRedondeada(x, y, CARD_WIDTH, CARD_HEIGHT, CORNER_RADIUS);
                    if (enEsquina)
                    {
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    // Patron de rombo para el dorso
                    bool esLinea = ((x + y) % 10 < 2) || ((x - y + CARD_HEIGHT) % 10 < 2);
                    tex.SetPixel(x, y, esLinea ? new Color(0.08f, 0.14f, 0.32f) : COL_DORSO);
                }
            }

            // Borde blanco
            DibujarBorde(tex, Color.white, 3);
            tex.Apply();
            return tex;
        }

        // =====================================================================
        // Utilidades de dibujo
        // =====================================================================

        private static void PintarFondo(Texture2D tex, Color fondoColor, Color bordeColor, int radio)
        {
            for (int y = 0; y < CARD_HEIGHT; y++)
            {
                for (int x = 0; x < CARD_WIDTH; x++)
                {
                    if (EsEsquinaRedondeada(x, y, CARD_WIDTH, CARD_HEIGHT, radio))
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                    else if (EsBorde(x, y, CARD_WIDTH, CARD_HEIGHT, 3, radio))
                    {
                        tex.SetPixel(x, y, bordeColor);
                    }
                    else
                    {
                        tex.SetPixel(x, y, fondoColor);
                    }
                }
            }
        }

        private static void PintarFranja(Texture2D tex, Color color, int yMin, int yMax, int radio)
        {
            for (int y = yMin; y < yMax && y < CARD_HEIGHT; y++)
            {
                for (int x = 0; x < CARD_WIDTH; x++)
                {
                    if (!EsEsquinaRedondeada(x, y, CARD_WIDTH, CARD_HEIGHT, radio))
                        tex.SetPixel(x, y, color);
                }
            }
        }

        private static void DibujarBorde(Texture2D tex, Color color, int grosor)
        {
            for (int i = 0; i < grosor; i++)
            {
                for (int x = 0; x < CARD_WIDTH; x++)
                {
                    if (tex.GetPixel(x, i) != Color.clear) tex.SetPixel(x, i, color);
                    if (tex.GetPixel(x, CARD_HEIGHT - 1 - i) != Color.clear) tex.SetPixel(x, CARD_HEIGHT - 1 - i, color);
                }
                for (int y = 0; y < CARD_HEIGHT; y++)
                {
                    if (tex.GetPixel(i, y) != Color.clear) tex.SetPixel(i, y, color);
                    if (tex.GetPixel(CARD_WIDTH - 1 - i, y) != Color.clear) tex.SetPixel(CARD_WIDTH - 1 - i, y, color);
                }
            }
        }

        private static bool EsEsquinaRedondeada(int x, int y, int w, int h, int r)
        {
            if (x < r && y < r)           return Vector2.Distance(new Vector2(x, y), new Vector2(r, r)) > r;
            if (x > w - r - 1 && y < r)  return Vector2.Distance(new Vector2(x, y), new Vector2(w - r - 1, r)) > r;
            if (x < r && y > h - r - 1)  return Vector2.Distance(new Vector2(x, y), new Vector2(r, h - r - 1)) > r;
            if (x > w - r - 1 && y > h - r - 1) return Vector2.Distance(new Vector2(x, y), new Vector2(w - r - 1, h - r - 1)) > r;
            return false;
        }

        private static bool EsBorde(int x, int y, int w, int h, int grosor, int radio)
        {
            return (x < grosor || x >= w - grosor || y < grosor || y >= h - grosor)
                && !EsEsquinaRedondeada(x, y, w, h, radio);
        }

        private static Color ObtenerColorPalo(Palo palo)
        {
            switch (palo)
            {
                case Palo.Oros:    return COL_OROS;
                case Palo.Copas:   return COL_COPAS;
                case Palo.Espadas: return COL_ESPADAS;
                case Palo.Bastos:  return COL_BASTOS;
                default:           return Color.gray;
            }
        }
    }
}
