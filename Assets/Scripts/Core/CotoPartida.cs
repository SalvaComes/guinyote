namespace GuinoteApp.Core
{
    /// <summary>Resultado de evaluar el final de una mano dentro del Coto.</summary>
    public struct ResultadoRonda
    {
        /// <summary>Equipo (1 o 2) que ha ganado el Juego con esta mano; 0 si el Juego continua.</summary>
        public int GanadorJuego;

        /// <summary>Equipo (1 o 2) que ha ganado el Coto; 0 si el Coto continua.</summary>
        public int GanadorCoto;

        public int JuegosEquipo1;
        public int JuegosEquipo2;

        /// <summary>Marcador de cada equipo justo despues de anotar la mano, por ejemplo
        /// "Equipo 1: 15 Buenas | Equipo 2: 40 Malas" (antes de reiniciarlo si se gano el Juego).</summary>
        public string Resumen;

        /// <summary>Puntos totales de cada equipo tras la mano, antes de reiniciar el Juego.</summary>
        public int PuntosTotalesEquipo1;
        public int PuntosTotalesEquipo2;
    }

    /// <summary>
    /// Un Coto: serie de Juegos. Cada Juego se compone de varias manos y se gana superando los
    /// 100 puntos (50 Buenas); gana el Coto el primer equipo que llega al numero de Juegos pedido.
    /// Equipo 1 = jugadores 1 y 3 (el humano y su compañero); Equipo 2 = jugadores 2 y 4.
    /// Clase pura sin dependencias de Unity.
    /// </summary>
    public class CotoPartida
    {
        public int JuegosParaGanar { get; }
        public MarcadorEquipo Equipo1 { get; } = new MarcadorEquipo();
        public MarcadorEquipo Equipo2 { get; } = new MarcadorEquipo();
        public int JuegosGanadosEquipo1 { get; private set; }
        public int JuegosGanadosEquipo2 { get; private set; }

        /// <summary>Equipo ganador del Coto (1 o 2); 0 mientras siga en juego.</summary>
        public int GanadorCoto { get; private set; }

        /// <summary>Puntos de bazas que hay que sumar (sin contar cantes) para poder ganar un Juego en las Idas.</summary>
        public const int PuntosMinimosSinCantes = 30;

        /// <summary>Manos ya terminadas en el Juego en curso (vuelve a 0 al ganarse un Juego).</summary>
        public int ManosJugadasEnJuego { get; private set; }

        /// <summary>False en la primera mano de cada Juego (las "Idas", que se juegan siempre hasta el final);
        /// true desde la segunda (las "Vueltas", que se cortan en cuanto un equipo supera los 100).</summary>
        public bool EsManoDeVueltas => ManosJugadasEnJuego > 0;

        public CotoPartida(int juegosParaGanar)
        {
            JuegosParaGanar = juegosParaGanar < 1 ? 1 : juegosParaGanar;
        }

        /// <summary>
        /// Desempate de las Idas cuando los dos equipos superan los 100: gana quien hizo las diez de
        /// ultimas, salvo que solo uno de ellos no llegue a 30 puntos sin cantes (ese pierde).
        /// </summary>
        /// <param name="puntosBazasEq1">Puntos de bazas del equipo 1 sin cantes; -1 si no se conocen.</param>
        public static int GanadorDesempateIdas(int equipoQueHizoUltimas, int puntosBazasEq1, int puntosBazasEq2)
        {
            bool corto1 = puntosBazasEq1 >= 0 && puntosBazasEq1 < PuntosMinimosSinCantes;
            bool corto2 = puntosBazasEq2 >= 0 && puntosBazasEq2 < PuntosMinimosSinCantes;
            if (corto1 && !corto2) return 2;
            if (corto2 && !corto1) return 1;
            return equipoQueHizoUltimas == 2 ? 2 : 1;
        }

        /// <summary>
        /// Quien gana el Juego dados los marcadores: el que supera los 100 puntos. Si lo superan
        /// los dos en la misma mano, gana el que tenga mas puntos totales y, en caso de empate
        /// exacto, el equipo que hizo las diez de ultimas.
        /// </summary>
        /// <returns>1, 2, o 0 si nadie ha ganado el Juego.</returns>
        public static int GanadorDelJuego(MarcadorEquipo equipo1, MarcadorEquipo equipo2, int equipoQueHizoUltimas)
        {
            bool gana1 = equipo1.HaGanadoJuego();
            bool gana2 = equipo2.HaGanadoJuego();

            if (!gana1 && !gana2) return 0;
            if (gana1 && !gana2) return 1;
            if (gana2 && !gana1) return 2;

            if (equipo1.PuntosTotales != equipo2.PuntosTotales)
                return equipo1.PuntosTotales > equipo2.PuntosTotales ? 1 : 2;

            return equipoQueHizoUltimas == 2 ? 2 : 1;
        }

        /// <summary>
        /// Suma los puntos de la mano a cada equipo y comprueba si alguien gana el Juego y, con
        /// el, el Coto. Si se gana un Juego, los marcadores vuelven a 0 para el siguiente.
        /// </summary>
        /// <param name="equipoQueHizoUltimas">1 o 2: equipo que gano la ultima baza de la mano.</param>
        /// <param name="puntosBazasEq1">Puntos de bazas (sin cantes) del equipo 1 en la mano; -1 si no se conocen.</param>
        /// <param name="puntosBazasEq2">Idem para el equipo 2.</param>
        public ResultadoRonda EvaluarFinDeRonda(int puntosRondaEq1, int puntosRondaEq2, int equipoQueHizoUltimas,
            int puntosBazasEq1 = -1, int puntosBazasEq2 = -1)
        {
            bool eraIdas = !EsManoDeVueltas;
            Equipo1.AnotarPuntosRonda(puntosRondaEq1);
            Equipo2.AnotarPuntosRonda(puntosRondaEq2);

            var resultado = new ResultadoRonda
            {
                Resumen = $"Equipo 1: {Equipo1} | Equipo 2: {Equipo2}",
                PuntosTotalesEquipo1 = Equipo1.PuntosTotales,
                PuntosTotalesEquipo2 = Equipo2.PuntosTotales
            };

            int ganadorJuego = GanadorDelJuego(Equipo1, Equipo2, equipoQueHizoUltimas);
            if (eraIdas && Equipo1.HaGanadoJuego() && Equipo2.HaGanadoJuego())
                ganadorJuego = GanadorDesempateIdas(equipoQueHizoUltimas, puntosBazasEq1, puntosBazasEq2);

            ManosJugadasEnJuego++;
            if (ganadorJuego != 0)
            {
                ManosJugadasEnJuego = 0;
                if (ganadorJuego == 1) JuegosGanadosEquipo1++;
                else JuegosGanadosEquipo2++;

                Equipo1.Reiniciar();
                Equipo2.Reiniciar();

                if (JuegosGanadosEquipo1 >= JuegosParaGanar) GanadorCoto = 1;
                else if (JuegosGanadosEquipo2 >= JuegosParaGanar) GanadorCoto = 2;
            }

            resultado.GanadorJuego = ganadorJuego;
            resultado.GanadorCoto = GanadorCoto;
            resultado.JuegosEquipo1 = JuegosGanadosEquipo1;
            resultado.JuegosEquipo2 = JuegosGanadosEquipo2;
            return resultado;
        }
    }
}
