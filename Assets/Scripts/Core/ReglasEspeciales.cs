using System.Collections.Generic;

namespace GuinoteApp.Core
{
    /// <summary>
    /// Reglas de los cantes (20 y 40) y del cambio de la pinta (cambio del 7 o del 2).
    /// Compartidas por el TurnManager y por el simulador de manos de la IA, de modo que
    /// ambos aplican exactamente las mismas condiciones.
    /// </summary>
    public static class ReglasEspeciales
    {
        public const int PuntosCanteTriunfo = 40;
        public const int PuntosCanteNormal = 20;

        /// <summary>Puntos que vale cantar el palo indicado.</summary>
        public static int PuntosCante(Palo palo, Palo triunfo) =>
            palo == triunfo ? PuntosCanteTriunfo : PuntosCanteNormal;

        /// <summary>True si la mano tiene el Rey y la Sota del palo.</summary>
        public static bool TieneReyYSota(IReadOnlyList<Carta> mano, Palo palo)
        {
            bool rey = false, sota = false;
            foreach (Carta c in mano)
            {
                if (c.Palo != palo) continue;
                if (c.Rango == Rango.Rey) rey = true;
                else if (c.Rango == Rango.Sota) sota = true;
            }
            return rey && sota;
        }

        /// <summary>Palos que la mano puede cantar ahora (Rey + Sota, y aun no cantados).</summary>
        public static List<Palo> PalosCantables(IReadOnlyList<Carta> mano, IReadOnlyCollection<Palo> palosCantados)
        {
            var res = new List<Palo>(2);
            foreach (Palo palo in System.Enum.GetValues(typeof(Palo)))
            {
                if (Contiene(palosCantados, palo)) continue;
                if (TieneReyYSota(mano, palo)) res.Add(palo);
            }
            return res;
        }

        /// <summary>
        /// Rango que permite cambiar la pinta: el 7 si la pinta es mayor que el 7; el 2 si es
        /// de valor 7 o inferior (pero no el 2); null si no hay mejora posible.
        /// </summary>
        public static Rango? RangoDeCambio(Carta pinta)
        {
            if (pinta == null) return null;
            if (pinta.Rango > Rango.Siete) return Rango.Siete;
            if (pinta.Rango > Rango.Dos) return Rango.Dos;
            return null;
        }

        /// <summary>Carta de la mano con la que se puede cambiar la pinta, o null.</summary>
        public static Carta CartaDeCambio(IReadOnlyList<Carta> mano, Carta pinta, Palo triunfo)
        {
            Rango? rango = RangoDeCambio(pinta);
            if (rango == null) return null;
            foreach (Carta c in mano)
                if (c.Palo == triunfo && c.Rango == rango.Value) return c;
            return null;
        }

        private static bool Contiene(IReadOnlyCollection<Palo> palos, Palo palo)
        {
            if (palos == null) return false;
            foreach (Palo p in palos)
                if (p == palo) return true;
            return false;
        }
    }
}
