using System;
using UnityEngine;
using GuinoteApp.Core;
using GuinoteApp.UI;

namespace GuinoteApp.Managers
{
    /// <summary>
    /// Gestor del Coto: contenedor de manos (rondas) que lleva el marcador de Malas y Buenas
    /// de cada equipo, los Juegos ganados y decide quien gana el Juego y el Coto.
    /// Las reglas viven en <see cref="CotoPartida"/> (Core); este componente las conecta con
    /// Unity (configuracion, log y eventos).
    /// Equipo 1 = jugadores 1 y 3 (el humano y su compañero); Equipo 2 = jugadores 2 y 4.
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        /// <summary>Juegos que hay que ganar para llevarse la partida completa (3 o 5).
        /// <see cref="IniciarCoto"/> lo sustituye por la eleccion de <see cref="MenuConfiguracion"/>.</summary>
        public int JuegosParaGanarPartida = 3;

        private CotoPartida coto;

        /// <summary>Se dispara tras evaluar el final de cada mano.</summary>
        public event Action<ResultadoRonda> OnRondaEvaluada;

        /// <summary>Un equipo gana un Juego. Parametros: equipo (1 o 2), juegos de cada equipo.</summary>
        public event Action<int, int, int> OnJuegoGanado;

        /// <summary>Un equipo gana el Coto. Parametros: equipo (1 o 2), juegos de cada equipo.</summary>
        public event Action<int, int, int> OnCotoGanado;

        public MarcadorEquipo MarcadorEquipo1 => Coto.Equipo1;
        public MarcadorEquipo MarcadorEquipo2 => Coto.Equipo2;
        public int JuegosGanadosEquipo1 => Coto.JuegosGanadosEquipo1;
        public int JuegosGanadosEquipo2 => Coto.JuegosGanadosEquipo2;

        /// <summary>False en la primera mano de un Juego (Idas: se juega entera); true desde la segunda
        /// (Vueltas: la mano se corta en cuanto un equipo supera los 100 puntos).</summary>
        public bool EsManoDeVueltas => Coto.EsManoDeVueltas;

        /// <summary>Equipo ganador del Coto (1 o 2); 0 si sigue en juego.</summary>
        public int GanadorCoto => Coto.GanadorCoto;

        private CotoPartida Coto
        {
            get
            {
                if (coto == null) IniciarCoto();
                return coto;
            }
        }

        /// <summary>Empieza un Coto nuevo con la configuracion elegida en el menu.</summary>
        public void IniciarCoto()
        {
            JuegosParaGanarPartida = MenuConfiguracion.JuegosParaGanarPartida;
            coto = new CotoPartida(JuegosParaGanarPartida);
            Debug.Log($"[MatchManager] Nuevo Coto a {JuegosParaGanarPartida} juegos.");
        }

        /// <summary>
        /// Anota los puntos de la mano en los marcadores y comprueba si alguien gana el Juego
        /// (mas de 100 puntos) y, con el, el Coto.
        /// </summary>
        /// <param name="puntosRondaEq1">Puntos de la mano del Equipo 1 (cantes y ultimas incluidos).</param>
        /// <param name="puntosRondaEq2">Puntos de la mano del Equipo 2.</param>
        /// <param name="equipoQueHizoUltimas">1 o 2: equipo que gano la ultima baza (desempata el Juego).</param>
        /// <param name="puntosBazasEq1">Puntos de bazas sin cantes del Equipo 1 (para la regla de los 30); -1 si no se conocen.</param>
        public void EvaluarFinDeRonda(int puntosRondaEq1, int puntosRondaEq2, int equipoQueHizoUltimas,
            int puntosBazasEq1 = -1, int puntosBazasEq2 = -1)
        {
            ResultadoRonda r = Coto.EvaluarFinDeRonda(puntosRondaEq1, puntosRondaEq2, equipoQueHizoUltimas,
                puntosBazasEq1, puntosBazasEq2);

            Debug.Log($"[MatchManager] Mano: Equipo 1 {puntosRondaEq1} pts, Equipo 2 {puntosRondaEq2} pts. {r.Resumen}");

            if (r.GanadorJuego != 0)
            {
                Debug.Log($"[MatchManager] ¡El Equipo {r.GanadorJuego} gana el Juego! Juegos: {r.JuegosEquipo1}-{r.JuegosEquipo2}");
                OnJuegoGanado?.Invoke(r.GanadorJuego, r.JuegosEquipo1, r.JuegosEquipo2);
            }
            if (r.GanadorCoto != 0)
            {
                Debug.Log($"[MatchManager] ¡El Equipo {r.GanadorCoto} gana el Coto! ({r.JuegosEquipo1}-{r.JuegosEquipo2})");
                OnCotoGanado?.Invoke(r.GanadorCoto, r.JuegosEquipo1, r.JuegosEquipo2);
            }

            OnRondaEvaluada?.Invoke(r);
        }
    }
}
