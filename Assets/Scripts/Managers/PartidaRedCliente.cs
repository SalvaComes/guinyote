using System;
using System.Collections.Generic;
using UnityEngine;
using GuinoteApp.Core;
using GuinoteApp.Network;
using GuinoteApp.UI;
using GuinoteApp.View;

namespace GuinoteApp.Managers
{
    /// <summary>
    /// Lado invitado del multijugador. No ejecuta reglas ni IA: pinta lo que cuenta el anfitrion y
    /// le envia las jugadas y decisiones de este jugador.
    ///
    /// La vista esta ROTADA para que quien juega aqui siempre se vea abajo (indice local 0): un
    /// asiento global g se dibuja en (g - miAsiento + 4) % 4. Como el turno va 0→1→2→3 y los equipos
    /// son 0+2 / 1+3, la rotacion conserva el orden de juego y los equipos, asi que se reutilizan
    /// CartaVisualManager y el HUD tal cual. Solo se conocen las cartas propias; las de los demas
    /// son marcadores (solo importa cuantas hay).
    /// </summary>
    public class PartidaRedCliente : MonoBehaviour
    {
        private const float PeriodoLatido = 2f;
        private const float PeriodoHello = 1f;
        private const float TiempoMaxSinAnfitrion = 15f;

        private static readonly string[] TextosSalida =
        {
            "Sorteo: sales tú",
            "Sorteo: sale el rival de tu derecha",
            "Sorteo: sale tu compañero",
            "Sorteo: sale el rival de tu izquierda"
        };

        private CartaVisualManager cartaVisualManager;
        private HUDMesaDeJuego hudMesa;
        private UIManager uiManager;
        private RedManager red;

        private readonly string clienteId = Guid.NewGuid().ToString("N").Substring(0, 8);
        private string nombre;
        private int miAsiento = -1;
        private bool enPartida;
        private bool activo;
        private float ultimoMensajeAnfitrion;
        private float proximoLatido;
        private float proximoHello;

        // Estado de la partida tal y como lo ve este jugador
        private List<Carta>[] manos;
        private Carta triunfoEnMesa;
        private int restantes;
        private bool ultimaRondaMostrada;
        private CardView cartaSeleccionada;

        /// <summary>Llega el estado de la sala (quien ocupa cada asiento).</summary>
        public event Action<EstadoSalaDto> OnSalaActualizada;

        /// <summary>Llega el primer reparto: la partida ha empezado.</summary>
        public event Action OnPartidaIniciada;

        /// <summary>Se perdio la conexion con el anfitrion o este cerro la partida. Parametro: motivo.</summary>
        public event Action<string> OnDesconectado;

        public EstadoSalaDto UltimoEstado { get; private set; }
        public bool Unido => miAsiento > 0;
        public int MiAsiento => miAsiento;

        // =====================================================================
        // Conexion
        // =====================================================================

        /// <summary>Empieza a escuchar al anfitrion y a pedir asiento. La conexion de red ya debe estar abierta o abriendose.</summary>
        public void Unirse(RedManager redManager, string nombreJugador,
            CartaVisualManager cvm, HUDMesaDeJuego hud, UIManager ui)
        {
            red = redManager;
            nombre = string.IsNullOrWhiteSpace(nombreJugador) ? "Invitado" : nombreJugador;
            cartaVisualManager = cvm;
            hudMesa = hud;
            uiManager = ui;

            miAsiento = -1;
            enPartida = false;
            activo = true;
            ultimoMensajeAnfitrion = Time.unscaledTime;

            red.JugadorLocalId = -1;
            red.OnMensajeRecibido += Procesar;
            red.OnConexionCambiada += AlCambiarConexion;
        }

        /// <summary>Sale de la partida y cierra la conexion.</summary>
        public void Salir()
        {
            Parar();
            red?.Desconectar();
            Destroy(this);
        }

        private void Parar()
        {
            activo = false;
            if (red == null) return;
            red.OnMensajeRecibido -= Procesar;
            red.OnConexionCambiada -= AlCambiarConexion;
        }

        private void OnDestroy() => Parar();

        private void AlCambiarConexion(bool conectado)
        {
            if (!conectado && activo) Perdido("Se perdió la conexión con el anfitrión");
        }

        private void Perdido(string motivo)
        {
            Parar();
            Debug.Log($"[PartidaRedCliente] {motivo}");
            if (enPartida && hudMesa != null) hudMesa.MostrarAviso(motivo);
            OnDesconectado?.Invoke(motivo);
        }

        private void Update()
        {
            if (!activo || red == null || !red.EstaConectado) return;

            float ahora = Time.unscaledTime;

            // Hasta tener asiento, repetir la peticion (el anfitrion puede tardar en estar listo)
            if (miAsiento < 0 && ahora >= proximoHello)
            {
                proximoHello = ahora + PeriodoHello;
                Enviar(TipoMensajeRed.UnirseSala, new UnirseSalaDto { clienteId = clienteId, nombre = nombre });
            }

            if (ahora >= proximoLatido)
            {
                proximoLatido = ahora + PeriodoLatido;
                Enviar(TipoMensajeRed.Latido, new AvisoDto { texto = "" });
            }

            if (ahora - ultimoMensajeAnfitrion > TiempoMaxSinAnfitrion)
                Perdido("El anfitrión no responde");
        }

        private void Enviar<T>(TipoMensajeRed tipo, T dto) =>
            red.Enviar(MensajeRedGuiñote.Crear(tipo, miAsiento, dto, MensajeRedGuiñote.Anfitrion));

        // =====================================================================
        // Mensajes del anfitrion
        // =====================================================================

        private int Local(int asientoGlobal) => (asientoGlobal - miAsiento + 4) % 4;
        private int MiEquipo => miAsiento % 2 == 0 ? 1 : 2;

        private void Procesar(MensajeRedGuiñote m)
        {
            // Solo importan los mensajes del anfitrion dirigidos a este asiento (o a todos)
            if (m.emisorId != MensajeRedGuiñote.Anfitrion) return;
            if (m.destino != MensajeRedGuiñote.ATodos && m.destino != miAsiento) return;
            ultimoMensajeAnfitrion = Time.unscaledTime;

            // Hasta el primer reparto solo se atiende la sala
            if (!enPartida && m.tipo != TipoMensajeRed.EstadoSala && m.tipo != TipoMensajeRed.RepartirCartas
                && m.tipo != TipoMensajeRed.PartidaTerminada && m.tipo != TipoMensajeRed.Latido)
                return;

            switch (m.tipo)
            {
                case TipoMensajeRed.EstadoSala:     AlRecibirSala(m.LeerPayload<EstadoSalaDto>()); break;
                case TipoMensajeRed.RepartirCartas: AlRepartir(m.LeerPayload<RepartoDto>()); break;
                case TipoMensajeRed.CartaJugada:    AlJugarCarta(m.LeerPayload<CartaJugadaDto>()); break;
                case TipoMensajeRed.FinalizarBaza:  AlResolverBaza(m.LeerPayload<BazaDto>()); break;
                case TipoMensajeRed.Robo:           AlRobar(m.LeerPayload<RoboDto>()); break;
                case TipoMensajeRed.SincronizarManos: AlSincronizar(m.LeerPayload<SincronizarDto>()); break;
                case TipoMensajeRed.CambioPinta:    AlCambiarPinta(m.LeerPayload<CambioPintaDto>()); break;
                case TipoMensajeRed.CanteRealizado: AlCantar(m.LeerPayload<CanteDto>()); break;
                case TipoMensajeRed.CambioFase:     AlCambiarFase(m.LeerPayload<FaseDto>()); break;
                case TipoMensajeRed.PedirCante:     AlPedirCante(m.LeerPayload<PedirCanteDto>()); break;
                case TipoMensajeRed.PedirCambio:    AlPedirCambio(m.LeerPayload<PedirCambioDto>()); break;
                case TipoMensajeRed.Marcador:       AplicarMarcador(m.LeerPayload<MarcadorDto>()); break;
                case TipoMensajeRed.FinDeMano:      AlTerminarMano(m.LeerPayload<FinManoDto>()); break;
                case TipoMensajeRed.JugadaRechazada: AlRechazarJugada(); break;
                case TipoMensajeRed.Aviso:          hudMesa?.MostrarAviso(m.LeerPayload<AvisoDto>().texto); break;
                case TipoMensajeRed.PartidaTerminada:
                    Perdido(m.LeerPayload<AvisoDto>().texto);
                    break;
            }
        }

        private void AlRecibirSala(EstadoSalaDto sala)
        {
            UltimoEstado = sala;
            int idx = Array.IndexOf(sala.clienteIds, clienteId);
            if (idx > 0)
            {
                miAsiento = idx;
                red.JugadorLocalId = idx;
            }
            OnSalaActualizada?.Invoke(sala);
        }

        private void AlRepartir(RepartoDto dto)
        {
            miAsiento = dto.asiento;
            red.JugadorLocalId = miAsiento;

            manos = new List<Carta>[4];
            manos[0] = CartaDto.ALista(dto.mano);
            for (int i = 1; i < 4; i++)
                manos[i] = Marcadores(dto.mano.Length);

            triunfoEnMesa = dto.triunfo.ALaCarta();
            restantes = dto.restantesMazo;
            ultimaRondaMostrada = false;
            cartaSeleccionada = null;

            if (!enPartida)
            {
                enPartida = true;
                if (uiManager != null) uiManager.IrAMesaDeJuego();
                OnPartidaIniciada?.Invoke();
            }

            if (cartaVisualManager != null)
            {
                cartaVisualManager.LimpiarTodo();
                cartaVisualManager.MostrarManoJugador(manos[0]);
                cartaVisualManager.ActualizarDorsosIA(manos);
                cartaVisualManager.MostrarTriunfo(triunfoEnMesa);
                cartaVisualManager.MostrarMazo(restantes);
                cartaVisualManager.OnJugadorSeleccionoCarta -= AlSeleccionarCarta;
                cartaVisualManager.OnJugadorSeleccionoCarta += AlSeleccionarCarta;
                cartaVisualManager.OnJugadorArrastroCartaAJugar -= AlArrastrarCarta;
                cartaVisualManager.OnJugadorArrastroCartaAJugar += AlArrastrarCarta;
            }

            if (hudMesa != null)
            {
                hudMesa.Resetear();
                AplicarMarcador(dto.marcador);
                hudMesa.ActualizarMazo(restantes);
                hudMesa.MostrarAviso(TextosSalida[Local(dto.jugadorInicial)]);
            }
        }

        private void AlJugarCarta(CartaJugadaDto dto)
        {
            int local = Local(dto.asiento);
            Carta carta = dto.carta.ALaCarta();

            AudioManager.Reproducir(TipoSonido.CartaLanzar);
            if (cartaVisualManager != null)
                cartaVisualManager.MostrarCartaEnBaza(local, carta);

            if (local == 0)
            {
                manos[0].RemoveAll(c => dto.carta.Es(c));
                if (cartaVisualManager != null)
                {
                    if (cartaSeleccionada != null && dto.carta.Es(cartaSeleccionada.CartaData))
                        cartaVisualManager.QuitarCartaDeMano(cartaSeleccionada);
                    else
                        cartaVisualManager.MostrarManoJugador(manos[0]);
                }
                cartaSeleccionada = null;
            }
            else
            {
                if (manos[local].Count > 0) manos[local].RemoveAt(manos[local].Count - 1);
                cartaVisualManager?.ActualizarDorsosIA(manos);
            }
        }

        private void AlResolverBaza(BazaDto dto)
        {
            if (dto.ultimaBaza) AudioManager.Reproducir(TipoSonido.UltimasBaza);
            cartaVisualManager?.AnimarBazaHaciaGanador(Local(dto.ganador));

            restantes = dto.restantesMazo;
            if (hudMesa != null && restantes > 0 && restantes < 4)
                hudMesa.MostrarAviso("Última ronda de robo: después, arrastre");

            RefrescarVisuales();
        }

        private void AlRobar(RoboDto dto)
        {
            AudioManager.Reproducir(TipoSonido.CartaRobar);
            int local = Local(dto.asiento);
            restantes = dto.restantesMazo;

            // La carta ya forma parte de la mano al empezar la animacion (igual que en el anfitrion)
            manos[local].Add(dto.carta.valida ? dto.carta.ALaCarta() : Marcador());

            if (cartaVisualManager == null) return;
            if (!dto.esPinta)
                cartaVisualManager.MostrarMazo(restantes, restantes > 0 && restantes < 4);
            cartaVisualManager.AnimarRobo(local, manos, dto.esPinta);
        }

        private void AlSincronizar(SincronizarDto dto)
        {
            manos[0] = CartaDto.ALista(dto.mano);
            for (int g = 0; g < 4; g++)
            {
                if (g == miAsiento) continue;
                List<Carta> lista = manos[Local(g)];
                while (lista.Count > dto.cartasPorAsiento[g]) lista.RemoveAt(lista.Count - 1);
                while (lista.Count < dto.cartasPorAsiento[g]) lista.Add(Marcador());
            }
            restantes = dto.restantesMazo;
            RefrescarVisuales();
        }

        private void AlCambiarPinta(CambioPintaDto dto)
        {
            triunfoEnMesa = dto.entregada.ALaCarta();

            bool mio = Local(dto.asiento) == 0;
            if (mio)
            {
                manos[0].RemoveAll(c => dto.entregada.Es(c));
                manos[0].Add(dto.recibida.ALaCarta());
            }

            if (cartaVisualManager == null) return;
            cartaVisualManager.MostrarTriunfo(triunfoEnMesa, ultimaRondaMostrada);
            if (mio) cartaVisualManager.MostrarManoJugador(manos[0]);
        }

        private void AlCantar(CanteDto dto)
        {
            AudioManager.Reproducir(dto.puntos >= 40 ? TipoSonido.Cante40 : TipoSonido.Cante20);
            hudMesa?.MostrarCante((Palo)dto.palo, dto.puntos);
        }

        private void AlCambiarFase(FaseDto dto)
        {
            if ((GamePhase)dto.fase != GamePhase.FaseArrastre) return;

            restantes = 0;
            if (cartaVisualManager != null)
            {
                cartaVisualManager.MostrarTriunfo(null);
                cartaVisualManager.MostrarMazo(0);
            }
            if (hudMesa != null)
            {
                hudMesa.ActualizarMazo(0);
                hudMesa.MostrarAviso("¡Arrastre! Hay que asistir y montar");
            }
        }

        // =====================================================================
        // Decisiones de este jugador
        // =====================================================================

        private void AlPedirCante(PedirCanteDto dto)
        {
            var palos = new List<Palo>();
            foreach (int p in dto.palos) palos.Add((Palo)p);
            var triunfo = (Palo)dto.paloTriunfo;

            if (hudMesa == null)
            {
                ResponderCante(HeuristicasIA.MejorCante(palos, triunfo));
                return;
            }
            hudMesa.PedirCante(palos, triunfo, ResponderCante);
        }

        private void ResponderCante(Palo? palo) =>
            Enviar(TipoMensajeRed.HacerCante,
                new RespuestaCanteDto { canta = palo.HasValue, palo = palo.HasValue ? (int)palo.Value : 0 });

        private void AlPedirCambio(PedirCambioDto dto)
        {
            if (hudMesa == null)
            {
                ResponderCambio(true);
                return;
            }
            hudMesa.PedirCambio(dto.propia.ALaCarta(), dto.pinta.ALaCarta(), ResponderCambio);
        }

        private void ResponderCambio(bool cambiar) =>
            Enviar(TipoMensajeRed.ResponderCambio, new RespuestaCambioDto { cambiar = cambiar });

        // =====================================================================
        // Entrada: jugar una carta (clic sobre la seleccionada, o arrastrarla a la mesa)
        // =====================================================================

        private void AlSeleccionarCarta(CardView vista)
        {
            if (cartaSeleccionada == vista && vista.CartaData != null)
            {
                JugarCarta(vista);
                return;
            }
            cartaSeleccionada = vista;
        }

        private void AlArrastrarCarta(CardView vista)
        {
            cartaSeleccionada = vista;
            JugarCarta(vista);
        }

        // El anfitrion decide: la carta no sale de la mano hasta que llega CartaJugada
        private void JugarCarta(CardView vista) =>
            Enviar(TipoMensajeRed.LanzarCarta, CartaDto.De(vista.CartaData));

        private void AlRechazarJugada()
        {
            if (cartaSeleccionada != null) cartaSeleccionada.Seleccionar(false);
            cartaSeleccionada = null;
        }

        // =====================================================================
        // Marcador y fin de mano
        // =====================================================================

        /// <summary>El marcador llega en equipos globales; aqui "nosotros" es el equipo de este jugador.</summary>
        private void AplicarMarcador(MarcadorDto dto)
        {
            if (hudMesa == null || dto == null) return;

            bool somosEq1 = MiEquipo == 1;
            hudMesa.ActualizarMarcadorCoto(
                somosEq1 ? dto.puntosEq1 : dto.puntosEq2,
                somosEq1 ? dto.puntosEq2 : dto.puntosEq1,
                somosEq1 ? dto.juegosEq1 : dto.juegosEq2,
                somosEq1 ? dto.juegosEq2 : dto.juegosEq1);
        }

        private void AlTerminarMano(FinManoDto dto)
        {
            AplicarMarcador(dto.marcador);

            bool somosEq1 = MiEquipo == 1;
            int juegosNuestros = somosEq1 ? dto.juegosEq1 : dto.juegosEq2;
            int juegosSuyos = somosEq1 ? dto.juegosEq2 : dto.juegosEq1;

            if (dto.ganadorCoto != 0)
            {
                bool ganamos = dto.ganadorCoto == MiEquipo;
                AudioManager.Reproducir(ganamos ? TipoSonido.Victoria : TipoSonido.Derrota);
                hudMesa?.MostrarFinCoto(ganamos, juegosNuestros, juegosSuyos);
                return;
            }

            if (hudMesa == null) return;

            string marcador = somosEq1
                ? $"Marcador: nosotros {dto.marcador.puntosEq1} · ellos {dto.marcador.puntosEq2}."
                : $"Marcador: nosotros {dto.marcador.puntosEq2} · ellos {dto.marcador.puntosEq1}.";
            string detalle = $"{marcador}\nEsperando al anfitrión para seguir…";

            if (dto.ganadorJuego != 0)
            {
                hudMesa.PedirContinuar(
                    dto.ganadorJuego == MiEquipo ? "¡Ganáis el juego!" : "Ellos ganan el juego",
                    $"{detalle}\nJuegos: nosotros {juegosNuestros} · ellos {juegosSuyos}.",
                    "De acuerdo", null);
                return;
            }

            hudMesa.PedirContinuar("Fin de la mano", detalle, "De acuerdo", null);
        }

        // =====================================================================
        // Utilidades de vista
        // =====================================================================

        /// <summary>Equivalente a GameManager.ActualizarVisualesDeManos con el estado local.</summary>
        private void RefrescarVisuales()
        {
            bool ultimaRonda = restantes > 0 && restantes < 4;

            if (cartaVisualManager != null)
            {
                cartaVisualManager.MostrarManoJugador(manos[0]);
                cartaVisualManager.ActualizarDorsosIA(manos);
                cartaVisualManager.MostrarMazo(restantes, ultimaRonda);

                if (restantes > 0 && ultimaRonda != ultimaRondaMostrada)
                {
                    cartaVisualManager.MostrarTriunfo(triunfoEnMesa, ultimaRonda);
                    ultimaRondaMostrada = ultimaRonda;
                }
            }

            hudMesa?.ActualizarMazo(restantes, ultimaRonda);
        }

        // Carta cualquiera que ocupa el sitio de una carta ajena: solo se cuenta, nunca se muestra
        private static Carta Marcador() => new Carta(Palo.Oros, Rango.As);

        private static List<Carta> Marcadores(int n)
        {
            var l = new List<Carta>(n);
            for (int i = 0; i < n; i++) l.Add(Marcador());
            return l;
        }
    }
}
