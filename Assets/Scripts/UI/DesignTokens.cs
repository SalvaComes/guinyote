using UnityEngine;

namespace GuinoteApp.UI
{
    /// <summary>
    /// Constantes del design system "Broadsheet" adaptadas a Unity.
    /// Todos los colores y tamaños de referencia están centralizados aquí
    /// para mantener consistencia visual en toda la UI.
    /// </summary>
    public static class DesignTokens
    {
        // =================================================================
        // Colores principales
        // =================================================================

        public static readonly Color ColorBg          = HexColor("#f3f2f2");
        public static readonly Color ColorSurface     = HexColor("#eae9e9");
        public static readonly Color ColorText        = HexColor("#201e1d");
        public static readonly Color ColorAccent      = HexColor("#0088b0");
        public static readonly Color ColorAccent2     = HexColor("#d6006c");

        // -----------------------------------------------------------------
        // Rampa tonal: Neutral
        // -----------------------------------------------------------------
        public static readonly Color Neutral100 = HexColor("#f8f4f4");
        public static readonly Color Neutral200 = HexColor("#eae7e7");
        public static readonly Color Neutral300 = HexColor("#d7d3d3");
        public static readonly Color Neutral400 = HexColor("#bab6b6");
        public static readonly Color Neutral500 = HexColor("#9b9797");
        public static readonly Color Neutral700 = HexColor("#605d5d");
        public static readonly Color Neutral800 = HexColor("#444141");
        public static readonly Color Neutral900 = HexColor("#2d2b2b");

        // -----------------------------------------------------------------
        // Rampa tonal: Accent (#0088b0)
        // -----------------------------------------------------------------
        public static readonly Color Accent100 = HexColor("#e9f8ff");
        public static readonly Color Accent600 = HexColor("#1186ac");
        public static readonly Color Accent700 = HexColor("#006786");
        public static readonly Color Accent800 = HexColor("#004961");

        // -----------------------------------------------------------------
        // Rampa tonal: Accent-2 (#d6006c)
        // -----------------------------------------------------------------
        public static readonly Color Accent2_100 = HexColor("#fff1f4");
        public static readonly Color Accent2_200 = HexColor("#ffdee6");
        public static readonly Color Accent2_700 = HexColor("#aa0b56");
        public static readonly Color Accent2_800 = HexColor("#790e3d");

        // =================================================================
        // Colores específicos de la mesa de juego
        // =================================================================

        /// <summary>Color central del tapete verde (gradiente radial).</summary>
        public static readonly Color TapeteCentro  = HexColor("#2f7d52");
        /// <summary>Color medio del tapete verde.</summary>
        public static readonly Color TapeteMedio   = HexColor("#1f5e3c");
        /// <summary>Color exterior del tapete verde.</summary>
        public static readonly Color TapeteBorde   = HexColor("#164a2f");
        /// <summary>Color de los bordes de madera de la mesa.</summary>
        public static readonly Color MaderaBorde   = HexColor("#8a5a2b");
        /// <summary>Color del dorso de las cartas.</summary>
        public static readonly Color DorsoCarta    = HexColor("#3a2a18");
        /// <summary>Color del texto sobre el tapete (etiquetas de jugador).</summary>
        public static readonly Color TextoTapete   = HexColor("#e9f2ea");

        // =================================================================
        // Tamaños de cartas (en unidades Unity, escalables)
        // =================================================================

        /// <summary>Tamaño de carta en mano del jugador local (62×88 px ref).</summary>
        public static readonly Vector2 CartaManoLocal = new Vector2(0.62f, 0.88f);
        /// <summary>Tamaño de carta en mano de rivales/compañero (48×68 px ref).</summary>
        public static readonly Vector2 CartaRival     = new Vector2(0.48f, 0.68f);
        /// <summary>Tamaño de carta jugada en la baza (54×76 px ref).</summary>
        public static readonly Vector2 CartaJugada    = new Vector2(0.54f, 0.76f);
        /// <summary>Tamaño de la carta de triunfo en mesa (44×62 px ref).</summary>
        public static readonly Vector2 CartaTriunfo   = new Vector2(0.44f, 0.62f);

        // =================================================================
        // Utilidades
        // =================================================================

        /// <summary>Convierte un string hexadecimal (#RRGGBB o #RRGGBBAA) a Color de Unity.</summary>
        public static Color HexColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color color))
                return color;
            Debug.LogWarning($"[DesignTokens] Color hex invalido: {hex}");
            return Color.magenta;
        }
    }
}
