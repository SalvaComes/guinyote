using System;
using System.Collections.Generic;
using GuinoteApp.Core;

namespace GuinoteApp.Network
{
    // DTOs (cargas utiles) de cada TipoMensajeRed. Solo campos publicos simples para JsonUtility.
    // Los asientos son siempre GLOBALES (0 = anfitrion, el turno avanza 0→1→2→3; equipo 1 = 0+2,
    // equipo 2 = 1+3). Cada invitado los rota a su propia vista en PartidaRedCliente.

    /// <summary>Carta en la red. <see cref="valida"/> false = "ninguna" (JsonUtility no admite null).</summary>
    [Serializable]
    public class CartaDto
    {
        public bool valida;
        public int palo;
        public int rango;

        public static CartaDto De(Carta c) =>
            c == null ? new CartaDto() : new CartaDto { valida = true, palo = (int)c.Palo, rango = (int)c.Rango };

        public static CartaDto[] De(IReadOnlyList<Carta> cartas)
        {
            var r = new CartaDto[cartas.Count];
            for (int i = 0; i < r.Length; i++) r[i] = De(cartas[i]);
            return r;
        }

        public Carta ALaCarta() => valida ? new Carta((Palo)palo, (Rango)rango) : null;

        public bool Es(Carta c) => valida && c != null && (int)c.Palo == palo && (int)c.Rango == rango;

        public static List<Carta> ALista(CartaDto[] dtos)
        {
            var l = new List<Carta>();
            if (dtos != null) foreach (CartaDto d in dtos) l.Add(d.ALaCarta());
            return l;
        }
    }

    [Serializable] public class UnirseSalaDto { public string clienteId; public string nombre; }

    /// <summary>Quien ocupa cada asiento; "" = libre o IA. El invitado se reconoce por su clienteId.</summary>
    [Serializable]
    public class EstadoSalaDto
    {
        public string[] clienteIds = new string[4];
        public string[] nombres = new string[4];
        public int humanosPrevistos;
        public bool partidaEmpezada;
    }

    [Serializable]
    public class MarcadorDto
    {
        // Textos "15 Buenas" / "40 Malas" de cada equipo (equipo 1 = asientos 0 y 2)
        public string puntosEq1;
        public string puntosEq2;
        public int juegosEq1;
        public int juegosEq2;
    }

    [Serializable]
    public class RepartoDto
    {
        public int asiento;
        public CartaDto[] mano;
        public CartaDto triunfo;
        public int restantesMazo;
        public int jugadorInicial;
        public MarcadorDto marcador;
    }

    [Serializable] public class CartaJugadaDto { public int asiento; public CartaDto carta; }
    [Serializable] public class BazaDto { public int ganador; public int puntos; public int restantesMazo; public bool ultimaBaza; }
    [Serializable] public class RoboDto { public int asiento; public bool esPinta; public CartaDto carta; public int restantesMazo; }

    [Serializable]
    public class SincronizarDto
    {
        public CartaDto[] mano;
        public int[] cartasPorAsiento;
        public int restantesMazo;
    }

    [Serializable] public class CambioPintaDto { public int asiento; public CartaDto entregada; public CartaDto recibida; }
    [Serializable] public class CanteDto { public int asiento; public int palo; public int puntos; }
    [Serializable] public class PedirCanteDto { public int[] palos; public int paloTriunfo; }
    [Serializable] public class RespuestaCanteDto { public bool canta; public int palo; }
    [Serializable] public class PedirCambioDto { public CartaDto propia; public CartaDto pinta; }
    [Serializable] public class RespuestaCambioDto { public bool cambiar; }
    [Serializable] public class FaseDto { public int fase; }
    [Serializable] public class AvisoDto { public string texto; }

    [Serializable]
    public class FinManoDto
    {
        public int ptsManoEq1;
        public int ptsManoEq2;
        public int ganadorJuego;   // 0 = el Juego sigue
        public int ganadorCoto;    // 0 = el Coto sigue
        public int juegosEq1;
        public int juegosEq2;
        public string resumen;
        public MarcadorDto marcador; // marcador de la mano recien terminada, antes de reiniciar el Juego
    }
}
