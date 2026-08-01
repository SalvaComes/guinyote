# AGENTS.md - Guía de Proyecto Guiñote para Antigravity AI

## 1. Visión General del Proyecto
**Guiñote** es un juego de cartas tradicional aragonés/español desarrollado en **Unity 2D (C#)** para 4 jugadores (2 parejas: Jugador 1 + 3 vs Jugador 2 + 4).

## 2. Arquitectura del Código
El proyecto sigue una separación estricta de responsabilidades (Capa Limpia / MVC para Unity):

```
Assets/Scripts/
├── Core/              # Lógica pura del juego (C# Puro, 0 dependencias de UnityEngine)
│   ├── GuinoteCore.cs     # Enums (Palo, Rango, GamePhase), modelo Carta, GestorBaraja
│   ├── EstadoBaza.cs      # Estado y cálculo de la baza actual
│   ├── ValidadorReglas.cs # Reglas de arrastre (Asistir, Montar, Fallar)
│   └── IAJugador.cs       # Estrategias de la Inteligencia Artificial (IAGreedy)
├── Managers/          # Coordinadores MonoBehavior y Corrutinas de Unity
│   ├── GameManager.cs        # Punto de entrada, inicialización de partida
│   ├── TurnManager.cs        # Orquestación de turnos, corrutinas de delays, robo y fases
│   └── CartaVisualManager.cs # Fábrica y posicionamiento visual de cartas
├── View/              # Componentes visuales asociados a Prefabs
│   └── CardView.cs           # Representación de carta 2D, animaciones Lerp, input del ratón
└── Editor/            # Utilidades para el Editor de Unity
    └── PlaceholderCardGenerator.cs # Generador procedural de cartas temporales
```

## 3. Reglas Inviolables de Arquitectura

### 🛑 Regla 1: Core Debe Ser C# Puro
- Los scripts bajo `GuinoteApp.Core` **JAMÁS** deben importar `UnityEngine` ni heredar de `MonoBehaviour`.
- Deben ser testeables de forma unitaria fuera de Unity.

### 🔄 Regla 2: Comunicación Basada en Eventos
- Los componentes visuales y gestores se comunican a través de eventos (`System.Action` / `event Action<T>`).
- Evitar acoplamiento directo o llamadas en cascada entre clases de la capa View y Core.

### 🎴 Regla 3: Dominio y Dominancia del Guiñote
- **Baraja:** 40 cartas (Oros, Copas, Espadas, Bastos).
- **Puntuación:** As (11), Tres (10), Rey (4), Sota (3), Caballo (2). Total en juego: 120 pts + 10 de últimas = 130 pts.
- **Fases:** `Reparto` → `FaseRobo` → `FaseArrastre` → `FinDePartida`.
- **Reglas de Arrastre (en `ValidadorReglas`):**
  1. *Asistir:* Si tienes del palo de salida, debes jugarlo.
  2. *Montar:* Si asistes, debes superar la mejor carta del mismo palo si puedes.
  3. *Fallar:* Si no puedes asistir, debes jugar triunfo. Si ya hay triunfo en mesa, debes superarlo si puedes.
  4. Si no puedes asistir ni fallar, cualquier carta es válida.

## 4. Estilo de Código y Convenciones
- **Espacios de nombres:** Cada script debe pertenecer a su namespace correspondiente (`GuinoteApp.Core`, `GuinoteApp.Managers`, `GuinoteApp.View`, `GuinoteApp.Editor`).
- **Nomenclatura:** Usar terminología del juego en español (`Carta`, `Palo`, `Rango`, `Baza`, `Triunfo`, `Arrastre`, `Robo`, `Pareja`).
- **Campos privados:** Usar `camelCase` o prefijo explicativo.
- **Propiedades y Métodos Públicos:** `PascalCase`.
- **Git:** Ignorar siempre carpetas generadas de Unity (`Library/`, `Temp/`, `Logs/`, `UserSettings/`).

## 5. Reglas Completas del Juego
Consultar siempre `REGLAS_GUINOTE.md` en la raíz del proyecto para las reglas completas del Guiñote, incluyendo la tabla de estado de implementación (qué está hecho y qué falta).

## 6. Funcionalidades Pendientes (Referencia Rápida)
- **Cantes (20 y 40):** Rey + Sota del mismo palo. 20 pts si no es triunfo, 40 pts si es triunfo.
- **Cambio del Siete de Triunfo:** Intercambiar el 7 de triunfo por la pinta.
- **Tute:** 4 Reyes o 4 Sotas → victoria instantánea.
- **Partida a 101 puntos:** Multi-mano con acumulación de puntos.
- **Regla de las 30 malas:** Mínimo 30 pts de juego (sin cantes) para poder ganar.
- **Desempate por últimas en 101:** Si ambas parejas pasan de 101 en la misma mano.
