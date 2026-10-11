using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

namespace GuinoteApp.Network
{
    /// <summary>
    /// Multijugador local por Bluetooth clasico (sockets RFCOMM) usando la API nativa de Android
    /// via JNI, sin plugins. Topologia en estrella: el anfitrion escucha y acepta hasta
    /// <see cref="MaxClientes"/> dispositivos (1 para 2 jugadores, 3 para 4); cada invitado conecta
    /// solo con el anfitrion. Los dispositivos deben estar ya emparejados desde Ajustes > Bluetooth.
    /// Lo que envia el anfitrion llega a todos sus invitados; lo que envia un invitado, solo al anfitrion.
    ///
    /// Requisitos en AndroidManifest.xml (Plugins/Android/AndroidManifest.xml):
    ///   BLUETOOTH, BLUETOOTH_ADMIN (maxSdkVersion 30), BLUETOOTH_CONNECT (Android 12+).
    /// El permiso BLUETOOTH_CONNECT se pide en <see cref="Inicializar"/>.
    ///
    /// Protocolo: cada mensaje va como [int32 big-endian con la longitud][JSON UTF-8].
    /// En el Editor y en otras plataformas el servicio informa de que no esta disponible.
    /// </summary>
    public class BluetoothAndroidServicio : ServicioRedBase
    {
        /// <summary>UUID propio del juego: anfitrion e invitados deben usar el mismo.</summary>
        private const string UuidServicio = "5b2c6a0e-3f1d-4c7a-9a52-7d1f0c8e4b19";

        private const int MaxTamMensaje = 64 * 1024;

        /// <summary>Invitados que acepta el anfitrion (1 a 3). Fijar antes de <see cref="Conectar"/>.</summary>
        public int MaxClientes { get; set; } = 1;

        private volatile bool conectado;
        private string nombreJugador = "Guinote";

        public override bool EstaConectado => conectado;

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>Un dispositivo conectado (el anfitrion tiene hasta 3; un invitado, 1).</summary>
        private class Par
        {
            public AndroidJavaObject Socket;
            public AndroidJavaObject Entrada;
            public AndroidJavaObject Salida;
            public readonly object LockEnvio = new object();
        }

        private volatile bool detener;
        private AndroidJavaObject adaptador;
        private AndroidJavaObject servidor;
        private readonly List<Par> pares = new List<Par>();
        private readonly object lockPares = new object();
#endif

        // =====================================================================
        // Preparacion
        // =====================================================================

        public override void Inicializar(string nombre)
        {
            nombreJugador = string.IsNullOrEmpty(nombre) ? nombreJugador : nombre;
#if UNITY_ANDROID && !UNITY_EDITOR
            // Android 12+ exige pedir BLUETOOTH_CONNECT en tiempo de ejecucion
            const string permiso = "android.permission.BLUETOOTH_CONNECT";
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(permiso))
                UnityEngine.Android.Permission.RequestUserPermission(permiso);

            using (var clase = new AndroidJavaClass("android.bluetooth.BluetoothAdapter"))
                adaptador = clase.CallStatic<AndroidJavaObject>("getDefaultAdapter");

            if (adaptador == null)
                NotificarError("Bluetooth: este dispositivo no tiene Bluetooth.");
            else if (!adaptador.Call<bool>("isEnabled"))
                NotificarError("Bluetooth: activalo en los ajustes del telefono.");
#else
            NotificarError("Bluetooth: solo disponible en dispositivos Android.");
#endif
        }

        /// <summary>Nombres de los dispositivos ya emparejados (para mostrarlos en la UI de "unirse").</summary>
        public List<string> ObtenerDispositivosEmparejados()
        {
            var nombres = new List<string>();
#if UNITY_ANDROID && !UNITY_EDITOR
            if (adaptador == null) return nombres;
            using (AndroidJavaObject set = adaptador.Call<AndroidJavaObject>("getBondedDevices"))
            using (AndroidJavaObject array = set.Call<AndroidJavaObject>("toArray"))
            {
                AndroidJavaObject[] dispositivos = AndroidJNIHelper.ConvertFromJNIArray<AndroidJavaObject[]>(array.GetRawObject());
                foreach (AndroidJavaObject d in dispositivos)
                    nombres.Add(d.Call<string>("getName"));
            }
#endif
            return nombres;
        }

        // =====================================================================
        // Conexion
        // =====================================================================

        /// <param name="destino">Nombre o direccion MAC del dispositivo emparejado al que conectar
        /// (se ignora si <paramref name="esAnfitrion"/>).</param>
        public override void Conectar(string destino, bool esAnfitrion)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (adaptador == null) { NotificarError("Bluetooth: no inicializado."); return; }
            Desconectar();
            detener = false;

            Thread hilo = esAnfitrion
                ? new Thread(HiloAnfitrion)
                : new Thread(() => HiloInvitado(destino));
            hilo.IsBackground = true;
            hilo.Start();
#else
            NotificarError("Bluetooth: solo disponible en dispositivos Android.");
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>Acepta invitados hasta llegar a <see cref="MaxClientes"/>; cada uno tiene su hilo lector.</summary>
        private void HiloAnfitrion()
        {
            AndroidJNI.AttachCurrentThread(); // los hilos de .NET deben registrarse en la JVM
            try
            {
                using (var clase = new AndroidJavaClass("java.util.UUID"))
                using (AndroidJavaObject uuid = clase.CallStatic<AndroidJavaObject>("fromString", UuidServicio))
                    servidor = adaptador.Call<AndroidJavaObject>("listenUsingRfcommWithServiceRecord", nombreJugador, uuid);

                int max = Mathf.Clamp(MaxClientes, 1, 3);
                while (!detener && ContarPares() < max)
                {
                    // accept() bloquea hasta que un invitado se conecta (o se cierra el servidor)
                    AndroidJavaObject socket = servidor.Call<AndroidJavaObject>("accept");
                    Par par = RegistrarPar(socket);
                    var lector = new Thread(() => HiloLector(par)) { IsBackground = true };
                    lector.Start();
                }
            }
            catch (Exception e)
            {
                if (!detener) NotificarError($"Bluetooth: {e.Message}");
            }
            finally
            {
                try { servidor?.Call("close"); } catch (Exception) { }
                servidor = null;
                AndroidJNI.DetachCurrentThread();
            }
        }

        private void HiloInvitado(string destino)
        {
            AndroidJNI.AttachCurrentThread();
            Par par = null;
            try
            {
                AndroidJavaObject socket;
                using (var clase = new AndroidJavaClass("java.util.UUID"))
                using (AndroidJavaObject uuid = clase.CallStatic<AndroidJavaObject>("fromString", UuidServicio))
                using (AndroidJavaObject dispositivo = BuscarEmparejado(destino))
                {
                    if (dispositivo == null)
                    {
                        NotificarError($"Bluetooth: no hay ningun dispositivo emparejado llamado '{destino}'.");
                        return;
                    }
                    adaptador.Call<bool>("cancelDiscovery"); // la busqueda activa ralentiza connect()
                    socket = dispositivo.Call<AndroidJavaObject>("createRfcommSocketToServiceRecord", uuid);
                    socket.Call("connect");
                }

                par = RegistrarPar(socket);
                CicloLectura(par);
            }
            catch (Exception e)
            {
                if (!detener) NotificarError($"Bluetooth: {e.Message}");
            }
            finally
            {
                if (par != null) QuitarPar(par);
                AndroidJNI.DetachCurrentThread();
            }
        }

        private void HiloLector(Par par)
        {
            AndroidJNI.AttachCurrentThread();
            try { CicloLectura(par); }
            catch (Exception e)
            {
                if (!detener) NotificarError($"Bluetooth: {e.Message}");
            }
            finally
            {
                QuitarPar(par);
                AndroidJNI.DetachCurrentThread();
            }
        }

        private AndroidJavaObject BuscarEmparejado(string nombreOMac)
        {
            using (AndroidJavaObject set = adaptador.Call<AndroidJavaObject>("getBondedDevices"))
            using (AndroidJavaObject array = set.Call<AndroidJavaObject>("toArray"))
            {
                AndroidJavaObject[] dispositivos = AndroidJNIHelper.ConvertFromJNIArray<AndroidJavaObject[]>(array.GetRawObject());
                foreach (AndroidJavaObject d in dispositivos)
                {
                    if (d.Call<string>("getName") == nombreOMac || d.Call<string>("getAddress") == nombreOMac)
                        return d;
                    d.Dispose();
                }
            }
            return null;
        }

        private int ContarPares()
        {
            lock (lockPares) return pares.Count;
        }

        private Par RegistrarPar(AndroidJavaObject socket)
        {
            var par = new Par
            {
                Socket = socket,
                Entrada = socket.Call<AndroidJavaObject>("getInputStream"),
                Salida = socket.Call<AndroidJavaObject>("getOutputStream")
            };

            bool primero;
            lock (lockPares)
            {
                pares.Add(par);
                primero = pares.Count == 1;
            }

            if (primero)
            {
                conectado = true;
                NotificarConexion(true);
            }
            return par;
        }

        private void QuitarPar(Par par)
        {
            try { par.Socket?.Call("close"); } catch (Exception) { }

            bool ultimo;
            lock (lockPares)
            {
                pares.Remove(par);
                ultimo = pares.Count == 0;
            }

            // Al quedarse sin ningun dispositivo se da por perdida la conexion
            if (ultimo && conectado)
            {
                conectado = false;
                NotificarConexion(false);
            }
        }

        private void CicloLectura(Par par)
        {
            while (!detener)
            {
                int longitud = (LeerByte(par) << 24) | (LeerByte(par) << 16) | (LeerByte(par) << 8) | LeerByte(par);
                if (longitud < 0 || longitud > MaxTamMensaje)
                    throw new InvalidOperationException("mensaje con longitud invalida");

                var bytes = new byte[longitud];
                for (int i = 0; i < longitud; i++)
                    bytes[i] = LeerByte(par);

                MensajeRedGuiñote m = MensajeRedGuiñote.DesdeJson(Encoding.UTF8.GetString(bytes));
                if (m != null) NotificarMensaje(m);
            }
        }

        // InputStream.read() devuelve un byte (0-255) o -1 si el otro lado cerro. Leer byte a byte
        // via JNI es lento, pero los mensajes de Guiñote son de unos pocos cientos de bytes.
        private static byte LeerByte(Par par)
        {
            int b = par.Entrada.Call<int>("read");
            if (b < 0) throw new System.IO.EndOfStreamException("el otro dispositivo cerro la conexion");
            return (byte)b;
        }
#endif

        // =====================================================================
        // Envio y cierre
        // =====================================================================

        public override void EnviarMensaje(MensajeRedGuiñote mensaje)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!conectado)
            {
                NotificarError("Bluetooth: no hay conexion para enviar.");
                return;
            }

            byte[] cuerpo = Encoding.UTF8.GetBytes(mensaje.ToJson());
            var trama = new sbyte[4 + cuerpo.Length]; // Java usa byte con signo
            trama[0] = (sbyte)(cuerpo.Length >> 24);
            trama[1] = (sbyte)(cuerpo.Length >> 16);
            trama[2] = (sbyte)(cuerpo.Length >> 8);
            trama[3] = (sbyte)cuerpo.Length;
            Buffer.BlockCopy(cuerpo, 0, trama, 4, cuerpo.Length);

            Par[] destinatarios;
            lock (lockPares) destinatarios = pares.ToArray();

            foreach (Par par in destinatarios)
            {
                lock (par.LockEnvio)
                {
                    try
                    {
                        par.Salida.Call("write", trama, 0, trama.Length);
                        par.Salida.Call("flush");
                    }
                    catch (Exception e)
                    {
                        NotificarError($"Bluetooth: fallo al enviar ({e.Message}).");
                    }
                }
            }
#else
            NotificarError("Bluetooth: solo disponible en dispositivos Android.");
#endif
        }

        public override void Desconectar()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            detener = true;
            // Cerrar los sockets desbloquea accept()/read() de los hilos de red
            try { servidor?.Call("close"); } catch (Exception) { }
            Par[] abiertos;
            lock (lockPares) abiertos = pares.ToArray();
            foreach (Par par in abiertos)
                try { par.Socket?.Call("close"); } catch (Exception) { }
#endif
            if (conectado)
            {
                conectado = false;
                NotificarConexion(false);
            }
        }
    }
}
