using System.Collections.Generic;

namespace GuinoteApp.Core
{
    /// <summary>
    /// Informacion publica de la partida que una IA puede usar para decidir.
    /// Solo contiene lo que un jugador sentado a la mesa podria saber: su propia mano,
    /// las cartas ya jugadas, la pinta visible, el marcador y los cantes anunciados.
    /// Clase pura sin dependencias de Unity.
    /// </summary>
    public class ContextoIA
    {
        /// <summary>Indice (0-3) del jugador IA que decide.</summary>
        public int JugadorIdx { get; set; }

        /// <summary>Mano del jugador IA.</summary>
        public List<Carta> Mano { get; set; } = new List<Carta>();

        /// <summary>Baza en curso (puede estar vacia si la IA abre).</summary>
        public EstadoBaza Baza { get; set; } = new EstadoBaza();

        public GamePhase Fase { get; set; } = GamePhase.FaseRobo;

        public Palo Triunfo { get; set; }

        /// <summary>Cartas ya jugadas en esta mano. Puede incluir las de la baza en curso.</summary>
        public IReadOnlyList<Carta> CartasJugadas { get; set; } = new List<Carta>();

        /// <summary>Puntos de la pareja del jugador IA (incluye cantes).</summary>
        public int PuntosEquipo { get; set; }

        /// <summary>Puntos de la pareja rival (incluye cantes).</summary>
        public int PuntosRival { get; set; }

        /// <summary>Pinta visible sobre la mesa; null si ya se ha robado.</summary>
        public Carta PintaVisible { get; set; }

        /// <summary>Cartas boca abajo que quedan en el mazo (sin contar la pinta).</summary>
        public int CartasEnMazo { get; set; }

        /// <summary>Palos cuyo cante ya se anuncio en esta mano (por cualquier jugador).</summary>
        public IReadOnlyCollection<Palo> PalosCantados { get; set; } = new List<Palo>();

        // ---------------------------------------------------------------------
        // Derivados
        // ---------------------------------------------------------------------

        public int CompaneroIdx => (JugadorIdx + 2) % 4;

        public bool CompaneroGanando => Baza != null && Baza.CartaSalida != null &&
                                        Baza.IndicadorGanadorActual == CompaneroIdx;

        public bool RivalGanando => Baza != null && Baza.CartaSalida != null &&
                                    Baza.IndicadorGanadorActual != JugadorIdx &&
                                    !CompaneroGanando;

        /// <summary>Cartas que quedan por robar: las del mazo mas la pinta, que se roba la ultima.</summary>
        public int CartasPorRobar => CartasEnMazo + (PintaVisible != null ? 1 : 0);

        public bool EsUltimoEnJugar => Baza != null && Baza.CartasJugadasCount == 3;

        public bool EsApertura => Baza == null || Baza.CartaSalida == null;
    }
}
