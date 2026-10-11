using System;
using System.Collections.Generic;
using UnityEngine;
using GuinoteApp.Core;
using GuinoteApp.Network;

namespace GuinoteApp.Managers
{
    /// <summary>
    /// Lado anfitrion del multijugador. El anfitrion (asiento 0) ejecuta TODA la partida con su
    /// TurnManager (reparto, reglas, IA); este componente
    ///  - gestiona la sala (quien ocupa cada asiento),
    ///  - difunde los eventos del TurnManager a los invitados (cada uno solo ve sus cartas),
    ///  - convierte los mensajes de los invitados en jugadas / respuestas al TurnManager,
    ///  - sustituye por IA a quien deje de dar señales de vida.
    /// Los asientos sin humano los juega la IA. Soporta 2 o 4 humanos; los invitados se sientan
    /// primero de companero (asiento 2) o de rival (asiento 1) segun la configuracion de la sala.
    /// </summary>
    public class PartidaRedAnfitrion : MonoBehaviour
    {
        private const float PeriodoLatido = 2f;
        private const float TiempoMaxSinLatido = 10f;

        private RedManager red;
        private TurnManager turnManager;
        private Func<List<Carta>[]> obtenerManos;
        private Func<int> obtenerRestantes;
        private Func<Carta> obtenerTriunfo;

        private readonly string[] clienteIds = new string[4];
        private readonly string[] nombres = new string[4];
        private readonly float[] ultimoLatido = new float[4];
        private int[] ordenInvitados = { 2, 1, 3 };
        private int humanosPrevistos = 2;
        private bool empezada;
        private bool suscritoATurnos;
        private float proximoLatido;

        /// <summary>La sala cambio (alguien se unio o se fue). Para refrescar el lobby.</summary>
        public event Action OnSalaCambiada;

        /// <summary>Un humano dejo la partida y su asiento paso a la IA. Parametro: asiento.</summary>
        public event Action<int> OnJugadorDesconectado;

        public int HumanosPrevistos => humanosPrevistos;
        public bool PartidaEmpezada => empezada;

        public int InvitadosConectados
        {
            get
            {
                int n = 0;
                for (int s = 1; s < 4; s++) if (clienteIds[s] != null) n++;
                return n;
            }
        }

        public bool SalaCompleta => InvitadosConectados >= humanosPrevistos - 1;

        /// <summary>Nombre del jugador en cada asiento; null si lo ocupa la IA o esta libre.</summary>
        public string NombreAsiento(int asiento) => nombres[asiento];

        // =====================================================================
        // Sala
        // =====================================================================

        public void AbrirSala(RedManager redManager, int humanos, bool invitadoCompanero, string nombreAnfitrion)
        {
            red = redManager;
            humanosPrevistos = Mathf.Clamp(humanos, 2, 4);
            ordenInvitados = invitadoCompanero ? new[] { 2, 1, 3 } : new[] { 1, 2, 3 };

            Array.Clear(clienteIds, 0, 4);
            Array.Clear(nombres, 0, 4);
            clienteIds[0] = "anfitrion";
            nombres[0] = string.IsNullOrWhiteSpace(nombreAnfitrion) ? "Anfitrión" : nombreAnfitrion;
            empezada = false;

            red.JugadorLocalId = 0;
            red.OnMensajeRecibido += Procesar;
        }

        /// <summary>Asientos con un humano (el anfitrion y los invitados ya unidos).</summary>
        public List<int> AsientosHumanos()
        {
            var l = new List<int>();
            for (int s = 0; s < 4; s++) if (clienteIds[s] != null) l.Add(s);
            return l;
        }

        /// <summary>
        /// Conecta la sala con la partida: a partir de aqui se difunden los eventos del TurnManager.
        /// </summary>
        public void Iniciar(TurnManager tm, Func<List<Carta>[]> manos, Func<int> restantes, Func<Carta> triunfoEnMesa)
        {
            turnManager = tm;
            obtenerManos = manos;
            obtenerRestantes = restantes;
            obtenerTriunfo = triunfoEnMesa;
            empezada = true;

            if (suscritoATurnos) return;
            suscritoATurnos = true;
            tm.OnCartaJugada += AlJugarCarta;
            tm.OnBazaResuelta += AlResolverBaza;
            tm.OnCambioFase += AlCambiarFase;
            tm.OnCante += AlCantar;
            tm.OnCambioTriunfo += AlCambiarPinta;
            tm.OnManosActualizadas += AlActualizarManos;
            tm.OnJugadorRobo += AlRobar;
            tm.OnDecisionCanteHumano += AlPedirCante;
            tm.OnDecisionCambioHumano += AlPedirCambio;
        }

        /// <summary>Termina la sesion: avisa a los invitados, deja de escuchar y cierra la conexion.</summary>
        public void Cerrar()
        {
            if (red != null)
            {
                Difundir(TipoMensajeRed.PartidaTerminada, new AvisoDto { texto = "El anfitrión ha cerrado la partida" });
                red.OnMensajeRecibido -= Procesar;
                red.Desconectar();
            }
            Desuscribir();
            Destroy(this);
        }

        private void OnDestroy()
        {
            if (red != null) red.OnMensajeRecibido -= Procesar;
            Desuscribir();
        }

        private void Desuscribir()
        {
            if (!suscritoATurnos || turnManager == null) return;
            suscritoATurnos = false;
            turnManager.OnCartaJugada -= AlJugarCarta;
            turnManager.OnBazaResuelta -= AlResolverBaza;
            turnManager.OnCambioFase -= AlCambiarFase;
            turnManager.OnCante -= AlCantar;
            turnManager.OnCambioTriunfo -= AlCambiarPinta;
            turnManager.OnManosActualizadas -= AlActualizarManos;
            turnManager.OnJugadorRobo -= AlRobar;
            turnManager.OnDecisionCanteHumano -= AlPedirCante;
            turnManager.OnDecisionCambioHumano -= AlPedirCambio;
        }

        private void Update()
        {
            if (red == null) return;

            if (Time.unscaledTime >= proximoLatido)
            {
                proximoLatido = Time.unscaledTime + PeriodoLatido;
                Difundir(TipoMensajeRed.Latido, new AvisoDto { texto = "" });
            }

            // Un invitado que lleva demasiado sin latir se ha ido
            for (int s = 1; s < 4; s++)
            {
                if (clienteIds[s] == null || Time.unscaledTime - ultimoLatido[s] < TiempoMaxSinLatido) continue;
                DarDeBaja(s);
            }
        }

        private void DarDeBaja(int asiento)
        {
            Debug.Log($"[PartidaRedAnfitrion] Asiento {asiento} sin señales de vida.");
            clienteIds[asiento] = null;
            nombres[asiento] = null;

            if (empezada)
            {
                turnManager.SustituirPorIA(asiento);
                Difundir(TipoMensajeRed.Aviso, new AvisoDto { texto = $"Jugador {asiento + 1} se ha ido: lo sustituye la IA" });
                OnJugadorDesconectado?.Invoke(asiento);
            }
            else
            {
                EnviarEstadoSala();
            }
            OnSalaCambiada?.Invoke();
        }

        // =====================================================================
        // Envio
        // =====================================================================

        public void Difundir<T>(TipoMensajeRed tipo, T dto) => EnviarA(tipo, dto, MensajeRedGuiñote.ATodos);

        private void EnviarA<T>(TipoMensajeRed tipo, T dto, int destino)
        {
            if (red == null || !red.EstaConectado) return;
            red.Enviar(MensajeRedGuiñote.Crear(tipo, MensajeRedGuiñote.Anfitrion, dto, destino));
        }

        private bool EsRemoto(int asiento) => asiento > 0 && asiento < 4 && clienteIds[asiento] != null;

        private void EnviarEstadoSala()
        {
            var dto = new EstadoSalaDto { humanosPrevistos = humanosPrevistos, partidaEmpezada = empezada };
            for (int s = 0; s < 4; s++)
            {
                dto.clienteIds[s] = clienteIds[s] ?? "";
                dto.nombres[s] = nombres[s] ?? "";
            }
            Difundir(TipoMensajeRed.EstadoSala, dto);
        }

        /// <summary>Reparto de una mano: cada invitado recibe solo sus cartas.</summary>
        public void EnviarReparto(List<Carta>[] manos, Carta triunfo, int restantes, int jugadorInicial, MarcadorDto marcador)
        {
            for (int s = 1; s < 4; s++)
            {
                if (!EsRemoto(s)) continue;
                EnviarA(TipoMensajeRed.RepartirCartas, new RepartoDto
                {
                    asiento = s,
                    mano = CartaDto.De(manos[s]),
                    triunfo = CartaDto.De(triunfo),
                    restantesMazo = restantes,
                    jugadorInicial = jugadorInicial,
                    marcador = marcador
                }, s);
            }
        }

        // =====================================================================
        // Eventos del TurnManager → invitados
        // =====================================================================

        private void AlJugarCarta(int asiento, Carta carta) =>
            Difundir(TipoMensajeRed.CartaJugada, new CartaJugadaDto { asiento = asiento, carta = CartaDto.De(carta) });

        private void AlResolverBaza(int ganador, int puntos, int ptsA, int ptsB) =>
            Difundir(TipoMensajeRed.FinalizarBaza, new BazaDto
            {
                ganador = ganador,
                puntos = puntos,
                restantesMazo = obtenerRestantes(),
                ultimaBaza = obtenerManos()[0].Count == 0
            });

        private void AlCambiarFase(GamePhase fase)
        {
            if (fase == GamePhase.FaseArrastre)
                Difundir(TipoMensajeRed.CambioFase, new FaseDto { fase = (int)fase });
        }

        private void AlCantar(int asiento, Palo palo, int puntos) =>
            Difundir(TipoMensajeRed.CanteRealizado, new CanteDto { asiento = asiento, palo = (int)palo, puntos = puntos });

        // La pinta entregada y la recibida son publicas: se difunde igual a todos
        private void AlCambiarPinta(int asiento, Carta entregada, Carta recibida) =>
            Difundir(TipoMensajeRed.CambioPinta, new CambioPintaDto
            {
                asiento = asiento,
                entregada = CartaDto.De(entregada),
                recibida = CartaDto.De(recibida)
            });

        private void AlRobar(int asiento, bool esPinta)
        {
            List<Carta> mano = obtenerManos()[asiento];
            Carta robada = mano.Count > 0 ? mano[mano.Count - 1] : null; // TurnManager la añade al final

            for (int s = 1; s < 4; s++)
            {
                if (!EsRemoto(s)) continue;
                EnviarA(TipoMensajeRed.Robo, new RoboDto
                {
                    asiento = asiento,
                    esPinta = esPinta,
                    // Solo quien roba ve la carta; los demas solo ven que roba
                    carta = CartaDto.De(s == asiento ? robada : null),
                    restantesMazo = obtenerRestantes()
                }, s);
            }
        }

        private void AlActualizarManos()
        {
            List<Carta>[] manos = obtenerManos();
            var cuentas = new[] { manos[0].Count, manos[1].Count, manos[2].Count, manos[3].Count };

            for (int s = 1; s < 4; s++)
            {
                if (!EsRemoto(s)) continue;
                EnviarA(TipoMensajeRed.SincronizarManos, new SincronizarDto
                {
                    mano = CartaDto.De(manos[s]),
                    cartasPorAsiento = cuentas,
                    restantesMazo = obtenerRestantes()
                }, s);
            }
        }

        private void AlPedirCante(int asiento, IReadOnlyList<Palo> palos)
        {
            if (!EsRemoto(asiento)) return;

            var lista = new int[palos.Count];
            for (int i = 0; i < lista.Length; i++) lista[i] = (int)palos[i];

            EnviarA(TipoMensajeRed.PedirCante,
                new PedirCanteDto { palos = lista, paloTriunfo = (int)obtenerTriunfo().Palo }, asiento);
        }

        private void AlPedirCambio(int asiento, Carta propia, Carta pinta)
        {
            if (!EsRemoto(asiento)) return;
            EnviarA(TipoMensajeRed.PedirCambio,
                new PedirCambioDto { propia = CartaDto.De(propia), pinta = CartaDto.De(pinta) }, asiento);
        }

        // =====================================================================
        // Mensajes de los invitados
        // =====================================================================

        private void Procesar(MensajeRedGuiñote m)
        {
            if (m.destino != MensajeRedGuiñote.Anfitrion && m.destino != MensajeRedGuiñote.ATodos) return;

            switch (m.tipo)
            {
                case TipoMensajeRed.UnirseSala:
                    AlUnirse(m.LeerPayload<UnirseSalaDto>());
                    break;

                case TipoMensajeRed.Latido:
                    if (EsRemoto(m.emisorId)) ultimoLatido[m.emisorId] = Time.unscaledTime;
                    break;

                case TipoMensajeRed.LanzarCarta:
                    AlLanzarCarta(m.emisorId, m.LeerPayload<CartaDto>());
                    break;

                case TipoMensajeRed.HacerCante:
                    if (!EsRemoto(m.emisorId) || turnManager == null) break;
                    RespuestaCanteDto c = m.LeerPayload<RespuestaCanteDto>();
                    turnManager.ResponderCanteDe(m.emisorId, c.canta ? (Palo?)(Palo)c.palo : null);
                    break;

                case TipoMensajeRed.ResponderCambio:
                    if (!EsRemoto(m.emisorId) || turnManager == null) break;
                    turnManager.ResponderCambioDe(m.emisorId, m.LeerPayload<RespuestaCambioDto>().cambiar);
                    break;
            }
        }

        private void AlUnirse(UnirseSalaDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.clienteId)) return;

            int asiento = Array.IndexOf(clienteIds, dto.clienteId);
            if (asiento < 0)
            {
                if (empezada || SalaCompleta) { EnviarEstadoSala(); return; }

                foreach (int s in ordenInvitados)
                {
                    if (clienteIds[s] != null) continue;
                    asiento = s;
                    break;
                }
                if (asiento < 0) return;

                clienteIds[asiento] = dto.clienteId;
                nombres[asiento] = string.IsNullOrWhiteSpace(dto.nombre) ? $"Jugador {asiento + 1}" : dto.nombre;
                Debug.Log($"[PartidaRedAnfitrion] {nombres[asiento]} se sienta en el asiento {asiento}.");
                OnSalaCambiada?.Invoke();
            }

            ultimoLatido[asiento] = Time.unscaledTime;
            EnviarEstadoSala(); // tambien sirve de respuesta si el invitado repite su peticion
        }

        private void AlLanzarCarta(int asiento, CartaDto dto)
        {
            if (!EsRemoto(asiento) || turnManager == null || dto == null) return;

            // Debe ser la instancia que esta en la mano (TurnManager compara por referencia)
            Carta carta = obtenerManos()[asiento].Find(c => dto.Es(c));
            if (carta == null || !turnManager.ProcesarJugadaDe(asiento, carta))
                EnviarA(TipoMensajeRed.JugadaRechazada, new AvisoDto { texto = "Jugada no válida" }, asiento);
        }
    }
}
