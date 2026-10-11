using UnityEngine;

namespace GuinoteApp.UI
{
    /// <summary>
    /// Configuración del Coto antes de empezar: a cuántos Juegos se juega la partida (3 o 5).
    /// La elección se guarda (PlayerPrefs) para que <c>MatchManager</c> la lea al iniciar el Coto.
    /// Se puede usar desde código (<see cref="Establecer"/>) o conectar a un botón de la UI
    /// (<see cref="EstablecerJuegos"/>).
    /// </summary>
    public class MenuConfiguracion : MonoBehaviour
    {
        public const int JuegosPorDefecto = 3;
        public const int JuegosAlternativos = 5;
        private const string ClavePrefs = "Guinote.JuegosParaGanarPartida";

        private static int juegosParaGanar;
        private static bool cargado;

        /// <summary>Juegos que hay que ganar para llevarse la partida completa (3 o 5).</summary>
        public static int JuegosParaGanarPartida
        {
            get
            {
                if (!cargado)
                {
                    juegosParaGanar = Normalizar(PlayerPrefs.GetInt(ClavePrefs, JuegosPorDefecto));
                    cargado = true;
                }
                return juegosParaGanar;
            }
        }

        /// <summary>Fija a cuántos Juegos se juega la partida. Solo se admiten 3 o 5; otro valor se ajusta al más cercano.</summary>
        public static void Establecer(int juegos)
        {
            juegosParaGanar = Normalizar(juegos);
            cargado = true;
            PlayerPrefs.SetInt(ClavePrefs, juegosParaGanar);
            PlayerPrefs.Save();
        }

        /// <summary>Alterna entre 3 y 5 Juegos y devuelve el nuevo valor.</summary>
        public static int Alternar()
        {
            Establecer(JuegosParaGanarPartida == JuegosPorDefecto ? JuegosAlternativos : JuegosPorDefecto);
            return juegosParaGanar;
        }

        /// <summary>Versión de instancia de <see cref="Establecer"/>, para conectarla a un botón.</summary>
        public void EstablecerJuegos(int juegos) => Establecer(juegos);

        private static int Normalizar(int juegos) =>
            juegos >= JuegosAlternativos ? JuegosAlternativos : JuegosPorDefecto;
    }
}
