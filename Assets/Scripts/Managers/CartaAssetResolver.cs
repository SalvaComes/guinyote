using System;
using System.Collections.Generic;
using UnityEngine;
using GuinoteApp.Core;

namespace GuinoteApp.Managers
{
    /// <summary>
    /// Resuelve el nombre de sprite (cara/dorso) de una carta a partir del indice
    /// de assets/baraja_guinote/deck.json, en lugar de reconstruir la ruta a mano.
    ///
    /// No usa deck.json para puntos ni jerarquia de baza: esa baraja documenta el
    /// orden clasico del Tute (Caballo &gt; Sota), mientras que este proyecto
    /// implementa deliberadamente la variante Guinote (Sota &gt; Caballo) — ver
    /// REGLAS_GUINOTE.md § 2 y Carta.ObtenerPuntos(). Esos valores siguen viviendo
    /// solo en Core.
    /// </summary>
    public class CartaAssetResolver
    {
        [Serializable]
        private class CartaEntry
        {
            public string file;
            public string suit;
            public string rank;
            public string code;
        }

        [Serializable]
        private class DeckJson
        {
            public string back;
            public CartaEntry[] cards;
        }

        private readonly Dictionary<string, string> nombrePorCarta = new Dictionary<string, string>();
        private readonly string nombreDorso;

        public CartaAssetResolver(TextAsset deckJson)
        {
            if (deckJson == null)
                throw new ArgumentNullException(nameof(deckJson), "CartaAssetResolver requiere el TextAsset de deck.json.");

            DeckJson deck = JsonUtility.FromJson<DeckJson>(deckJson.text);

            foreach (CartaEntry entry in deck.cards)
            {
                string clave = ClaveDe(entry.suit, entry.rank);
                nombrePorCarta[clave] = NombreSprite(entry.file);
            }

            nombreDorso = NombreSprite(deck.back);
        }

        /// <summary>Nombre del sprite (sin ruta ni extension) para la cara de una carta.</summary>
        public string ResolverNombreSprite(Palo palo, Rango rango)
        {
            string clave = ClaveDe(palo.ToString(), rango.ToString());
            if (nombrePorCarta.TryGetValue(clave, out string nombre))
                return nombre;

            Debug.LogWarning($"[CartaAssetResolver] No hay entrada en deck.json para {clave}.");
            return null;
        }

        /// <summary>Nombre del sprite (sin ruta ni extension) para el dorso.</summary>
        public string ResolverNombreDorso() => nombreDorso;

        private static string ClaveDe(string palo, string rango) => $"{palo}_{rango}";

        // "cards/oros-1.svg" -> "oros-1"
        private static string NombreSprite(string archivo)
        {
            string sinCarpeta = archivo.Substring(archivo.LastIndexOf('/') + 1);
            int puntoIdx = sinCarpeta.LastIndexOf('.');
            return puntoIdx >= 0 ? sinCarpeta.Substring(0, puntoIdx) : sinCarpeta;
        }
    }
}
