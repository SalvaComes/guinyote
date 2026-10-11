using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GuinoteApp.Network
{
    /// <summary>
    /// Multijugador por Internet: un WebSocket contra un servidor Relay que reenvia cada mensaje
    /// a los demas clientes de la misma sala (ver Tools/RelayServer/server.js).
    /// Usa System.Net.WebSockets (disponible en Android e iOS; no en WebGL).
    /// </summary>
    public class InternetRelayServicio : ServicioRedBase
    {
        private const int TamBuffer = 4096;

        private ClientWebSocket socket;
        private CancellationTokenSource cancelacion;
        private readonly SemaphoreSlim envioLock = new SemaphoreSlim(1, 1);

        public override bool EstaConectado => socket != null && socket.State == WebSocketState.Open;

        /// <summary>Compone la URL de una sala: <c>wss://mi-relay.example.com</c> + <c>ABC123</c>.</summary>
        public static string ConstruirUrlSala(string urlBase, string codigoSala) =>
            $"{urlBase.TrimEnd('/')}/{Uri.EscapeDataString(codigoSala)}";

        public override void Inicializar(string nombreJugador)
        {
            // Nada que preparar: el relay no necesita permisos ni adaptadores
        }

        /// <param name="destino">URL completa de la sala, p. ej. wss://relay.example.com/ABC123.</param>
        /// <param name="esAnfitrion">Sin efecto: en el relay todos los clientes son iguales.</param>
        public override void Conectar(string destino, bool esAnfitrion)
        {
            if (EstaConectado) Desconectar();

            cancelacion = new CancellationTokenSource();
            socket = new ClientWebSocket();
            _ = ConectarYEscucharAsync(socket, destino, cancelacion.Token);
        }

        private async Task ConectarYEscucharAsync(ClientWebSocket ws, string url, CancellationToken ct)
        {
            try
            {
                await ws.ConnectAsync(new Uri(url), ct);
                NotificarConexion(true);

                var buffer = new byte[TamBuffer];
                while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    // Un mensaje puede llegar en varios fragmentos
                    using (var acumulado = new MemoryStream())
                    {
                        WebSocketReceiveResult r;
                        do
                        {
                            r = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                            if (r.MessageType == WebSocketMessageType.Close) goto cerrado;
                            acumulado.Write(buffer, 0, r.Count);
                        } while (!r.EndOfMessage);

                        if (r.MessageType != WebSocketMessageType.Text) continue;

                        MensajeRedGuiñote m = MensajeRedGuiñote.DesdeJson(Encoding.UTF8.GetString(acumulado.ToArray()));
                        if (m != null) NotificarMensaje(m);
                    }
                }
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                NotificarError($"Relay: {e.Message}");
            }
            catch (Exception)
            {
                // Cancelacion pedida por Desconectar(): no es un error
            }

            cerrado:
            NotificarConexion(false);
        }

        public override async void EnviarMensaje(MensajeRedGuiñote mensaje)
        {
            if (!EstaConectado)
            {
                NotificarError("Relay: no hay conexion para enviar.");
                return;
            }

            byte[] datos = Encoding.UTF8.GetBytes(mensaje.ToJson());

            // ClientWebSocket no admite dos SendAsync a la vez
            await envioLock.WaitAsync();
            try
            {
                await socket.SendAsync(new ArraySegment<byte>(datos), WebSocketMessageType.Text, true,
                    cancelacion != null ? cancelacion.Token : CancellationToken.None);
            }
            catch (Exception e)
            {
                NotificarError($"Relay: fallo al enviar ({e.Message}).");
            }
            finally
            {
                envioLock.Release();
            }
        }

        public override void Desconectar()
        {
            cancelacion?.Cancel();
            socket?.Abort();
            socket?.Dispose();
            socket = null;
            cancelacion = null;
        }
    }
}
