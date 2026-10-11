namespace GuinoteApp.Core
{
    /// <summary>
    /// Marcador de un equipo a lo largo de un "Juego" (varias manos). Los puntos se acumulan
    /// de mano en mano: del 0 al 49 son "Malas" y a partir del 50 son "Buenas". El Juego lo
    /// gana quien supera los 100 puntos (50 Buenas).
    /// Clase pura sin dependencias de Unity.
    /// </summary>
    public class MarcadorEquipo
    {
        /// <summary>Puntos para entrar en las Buenas.</summary>
        public const int PuntosParaBuenas = 50;

        /// <summary>Puntos que hay que superar para ganar el Juego.</summary>
        public const int PuntosParaGanarJuego = 100;

        /// <summary>Puntos acumulados mano tras mano durante el Juego en curso.</summary>
        public int PuntosTotales { get; private set; }

        public bool EstaEnBuenas => PuntosTotales >= PuntosParaBuenas;

        /// <summary>Puntos tal y como se anuncian: en Malas los totales; en Buenas, los que pasan de 50.</summary>
        public int PuntosMostrados => EstaEnBuenas ? PuntosTotales - PuntosParaBuenas : PuntosTotales;

        /// <summary>Suma los puntos de una mano (cantes y últimas incluidos).</summary>
        public void AnotarPuntosRonda(int puntos)
        {
            PuntosTotales += puntos;
        }

        /// <summary>True si el equipo ha superado los 100 puntos (50 Buenas).</summary>
        public bool HaGanadoJuego() => PuntosTotales > PuntosParaGanarJuego;

        /// <summary>Vuelve a 0 para empezar un nuevo Juego.</summary>
        public void Reiniciar()
        {
            PuntosTotales = 0;
        }

        /// <summary>Texto del marcador, por ejemplo "15 Buenas" o "40 Malas".</summary>
        public override string ToString() => Describir(PuntosTotales);

        /// <summary>Texto del marcador para unos puntos totales dados.</summary>
        public static string Describir(int puntosTotales)
        {
            bool buenas = puntosTotales >= PuntosParaBuenas;
            int mostrados = buenas ? puntosTotales - PuntosParaBuenas : puntosTotales;
            return $"{mostrados} {(buenas ? "Buenas" : "Malas")}";
        }
    }
}
