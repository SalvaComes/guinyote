using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GuinoteApp.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GuinoteApp.PlayMode.Tests
{
    /// <summary>
    /// Juega partidas completas con el TurnManager real (coroutines incluidas) y un humano
    /// automatico que responde a las decisiones de cante y cambio de la pinta.
    /// TurnManager vive en el ensamblado por defecto (sin asmdef), asi que se maneja por reflexion.
    /// Si el TurnManager registra un error durante la partida, la prueba falla.
    /// </summary>
    public class TurnManagerPartidaTests
    {
        private const int MaximoDeFrames = 30000;

        private static readonly NivelDificultadIA[] Niveles =
        {
            NivelDificultadIA.Facil, NivelDificultadIA.Normal, NivelDificultadIA.Dificil, NivelDificultadIA.Experto
        };

        /// <summary>Humano automatico: juega la primera carta valida y responde a las preguntas del TurnManager.</summary>
        private sealed class HumanoAutomatico
        {
            public object TurnManager;
            public MethodInfo ResponderCante, ResponderCambio;
            public bool AceptaCantar = true, AceptaCambiar = true;

            public bool TurnoPendiente;
            public bool PartidaTerminada;
            public int PuntosA, PuntosB;
            public int CantesHumano, CambiosHumano, PreguntasCante, PreguntasCambio;

            public void OnTurno() { TurnoPendiente = true; }

            public void OnDecisionCante(int asiento, IReadOnlyList<Palo> palos)
            {
                PreguntasCante++;
                Palo? respuesta = AceptaCantar ? palos[0] : (Palo?)null;
                ResponderCante.Invoke(TurnManager, new object[] { respuesta });
            }

            public void OnDecisionCambio(int asiento, Carta propia, Carta pinta)
            {
                PreguntasCambio++;
                ResponderCambio.Invoke(TurnManager, new object[] { AceptaCambiar });
            }

            public void OnCante(int jugador, Palo palo, int puntos) { if (jugador == 0) CantesHumano++; }
            public void OnCambio(int jugador, Carta entregada, Carta recibida) { if (jugador == 0) CambiosHumano++; }
            public void OnFin(int a, int b) { PartidaTerminada = true; PuntosA = a; PuntosB = b; }
        }

        private static Type BuscarTipo(string nombre)
        {
            foreach (Assembly ensamblado in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = ensamblado.GetType(nombre);
                if (t != null) return t;
            }
            Assert.Fail("No se encuentra el tipo " + nombre);
            return null;
        }

        private static void Suscribir(Type tipo, object instancia, string evento, object destino, string metodo)
        {
            EventInfo e = tipo.GetEvent(evento);
            Assert.IsNotNull(e, "Evento no encontrado: " + evento);
            MethodInfo m = destino.GetType().GetMethod(metodo);
            e.AddEventHandler(instancia, Delegate.CreateDelegate(e.EventHandlerType, destino, m));
        }

        private static IEnumerator JugarPartida(
            NivelDificultadIA nivel, bool aceptaCantar, bool aceptaCambiar, Action<HumanoAutomatico> alTerminar)
        {
            Type tipo = BuscarTipo("GuinoteApp.Managers.TurnManager");
            var go = new GameObject("TurnManagerDePrueba");
            Component tm = go.AddComponent(tipo);

            // Sin esperas visuales: los delays son campos privados serializados
            foreach (string campo in new[] { "delayIAPiensa", "delayEntreCartas", "delayFinBaza", "delayRobo" })
                tipo.GetField(campo, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(tm, 0f);

            var humano = new HumanoAutomatico
            {
                TurnManager = tm,
                ResponderCante = tipo.GetMethod("ResponderCanteHumano"),
                ResponderCambio = tipo.GetMethod("ResponderCambioHumano"),
                AceptaCantar = aceptaCantar,
                AceptaCambiar = aceptaCambiar
            };
            Suscribir(tipo, tm, "OnTurnoJugadorHumano", humano, nameof(HumanoAutomatico.OnTurno));
            Suscribir(tipo, tm, "OnDecisionCanteHumano", humano, nameof(HumanoAutomatico.OnDecisionCante));
            Suscribir(tipo, tm, "OnDecisionCambioHumano", humano, nameof(HumanoAutomatico.OnDecisionCambio));
            Suscribir(tipo, tm, "OnCante", humano, nameof(HumanoAutomatico.OnCante));
            Suscribir(tipo, tm, "OnCambioTriunfo", humano, nameof(HumanoAutomatico.OnCambio));
            Suscribir(tipo, tm, "OnFinPartida", humano, nameof(HumanoAutomatico.OnFin));

            // Reparto igual que GameManager
            var baraja = new GestorBaraja();
            baraja.Barajar();
            var manos = new List<Carta>[4];
            for (int i = 0; i < 4; i++) manos[i] = new List<Carta>();
            for (int ronda = 0; ronda < 6; ronda++)
                for (int j = 0; j < 4; j++)
                    manos[j].Add(baraja.RobarCarta());
            Carta triunfo = baraja.RobarCarta();

            tipo.GetMethod("ConfigurarDificultad").Invoke(tm, new object[] { nivel });
            tipo.GetMethod("Inicializar").Invoke(
                tm, new object[] { manos, baraja, triunfo, GamePhase.FaseRobo, UnityEngine.Random.Range(0, 4) });

            MethodInfo procesar = tipo.GetMethod("ProcesarJugadaHumano");
            int frames = 0;
            while (!humano.PartidaTerminada && frames++ < MaximoDeFrames)
            {
                yield return null;
                if (!humano.TurnoPendiente) continue;

                humano.TurnoPendiente = false;
                bool jugada = false;
                foreach (Carta carta in manos[0].ToArray())
                {
                    if ((bool)procesar.Invoke(tm, new object[] { carta })) { jugada = true; break; }
                }
                Assert.IsTrue(jugada, "El humano no encontro ninguna jugada valida");
            }

            alTerminar(humano);
            UnityEngine.Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator PartidaCompleta_ConCadaNivel_TerminaYReparte130PuntosMasCantes(
            [ValueSource(nameof(Niveles))] NivelDificultadIA nivel)
        {
            HumanoAutomatico resultado = null;
            yield return JugarPartida(nivel, true, true, h => resultado = h);

            Assert.IsTrue(resultado.PartidaTerminada, $"La partida con IA {nivel} no termino");
            int total = resultado.PuntosA + resultado.PuntosB;
            Assert.GreaterOrEqual(total, 130);
            Assert.AreEqual(0, (total - 130) % 20, "Los cantes suman de 20 en 20 (o 40)");
        }

        [UnityTest]
        public IEnumerator HumanoQueRechazaCantarYCambiar_LaPartidaContinuaYNoCantaNiCambia()
        {
            int preguntas = 0;
            for (int i = 0; i < 12; i++)
            {
                HumanoAutomatico h = null;
                yield return JugarPartida(NivelDificultadIA.Normal, false, false, r => h = r);

                Assert.IsTrue(h.PartidaTerminada);
                Assert.AreEqual(0, h.CantesHumano, "El humano rechazo todos los cantes");
                Assert.AreEqual(0, h.CambiosHumano, "El humano rechazo todos los cambios");
                preguntas += h.PreguntasCante + h.PreguntasCambio;
            }

            Assert.Greater(preguntas, 0, "En 12 partidas deberia haberse preguntado al humano alguna vez");
        }

        [UnityTest]
        public IEnumerator HumanoQueAceptaTodo_CadaPreguntaSeConvierteEnCanteOCambio()
        {
            int preguntas = 0;
            for (int i = 0; i < 12; i++)
            {
                HumanoAutomatico h = null;
                yield return JugarPartida(NivelDificultadIA.Normal, true, true, r => h = r);

                Assert.IsTrue(h.PartidaTerminada);
                Assert.AreEqual(h.PreguntasCante, h.CantesHumano, "Cada pregunta de cante aceptada produce un cante");
                Assert.AreEqual(h.PreguntasCambio, h.CambiosHumano, "Cada pregunta de cambio aceptada produce un cambio");
                preguntas += h.PreguntasCante + h.PreguntasCambio;
            }

            Assert.Greater(preguntas, 0);
        }
    }
}
