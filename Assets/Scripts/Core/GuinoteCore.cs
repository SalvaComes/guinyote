using System;
using System.Collections.Generic;

namespace GuinoteApp.Core
{
    public enum Palo { Oros, Copas, Espadas, Bastos }
    public enum Rango { Dos, Cuatro, Cinco, Seis, Siete, Caballo, Sota, Rey, Tres, As }

    /// <summary>
    /// Fases de la partida de Guiniote.
    /// Definido en Core para que tanto la IA como los Managers puedan usarlo
    /// sin generar dependencias circulares.
    /// </summary>
    public enum GamePhase { Reparto, FaseRobo, FaseArrastre, FinDePartida }

    [Serializable]
    public class Carta
    {
        public Palo Palo { get; private set; }
        public Rango Rango { get; private set; }

        public Carta(Palo palo, Rango rango)
        {
            Palo = palo;
            Rango = rango;
        }

        public int ObtenerPuntos()
        {
            switch (Rango)
            {
                case Rango.As: return 11;
                case Rango.Tres: return 10;
                case Rango.Rey: return 4;
                case Rango.Sota: return 3;
                case Rango.Caballo: return 2;
                default: return 0;
            }
        }
        public override string ToString() => $"{Rango} de {Palo}";
    }

    public class GestorBaraja
    {
        private List<Carta> mazo = new List<Carta>();
        private Random rng = new Random();

        public GestorBaraja() { GenerarMazo(); }

        private void GenerarMazo()
        {
            mazo.Clear();
            foreach (Palo palo in Enum.GetValues(typeof(Palo)))
                foreach (Rango rango in Enum.GetValues(typeof(Rango)))
                    mazo.Add(new Carta(palo, rango));
        }

        public void Barajar()
        {
            int n = mazo.Count;
            while (n > 1) { n--; int k = rng.Next(n + 1); (mazo[k], mazo[n]) = (mazo[n], mazo[k]); }
        }

        public Carta RobarCarta()
        {
            if (mazo.Count == 0) return null;
            Carta c = mazo[0]; mazo.RemoveAt(0); return c;
        }
        public int CartasRestantes() => mazo.Count;
    }
}
