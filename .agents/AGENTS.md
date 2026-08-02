# AGENTS.md - Guía de Proyecto Guiñote para Antigravity AI

## 1. Visión General del Proyecto
**Guiñote** es un juego de cartas tradicional aragonés/español desarrollado en **Unity 2D (C#)** para 4 jugadores (2 parejas: Jugador 1 [Humano] + 3 [IA] vs Jugador 2 [IA] + 4 [IA]).

## 2. Arquitectura del Código
El proyecto sigue una separación estricta de responsabilidades (Capa Limpia / MVC para Unity):

```
Assets/
├── Scripts/
│   ├── Core/              # Lógica pura del juego (C# Puro, 0 dependencias de UnityEngine)
│   │   ├── GuinoteCore.cs     # Enums (Palo, Rango, GamePhase), modelo Carta, GestorBaraja
│   │   ├── EstadoBaza.cs      # Estado y cálculo de la baza actual
│   │   ├── ValidadorReglas.cs # Reglas de arrastre (Asistir, Montar, Fallar), cantes y cambios
│   │   ├── IAJugador.cs       # Interfaz IEstrategiaIA, NivelDificultadIA, IAFactory
│   │   └── IAFacil.cs / IANormal.cs / IADificil.cs # Las 3 estrategias de IA (por dificultad)
│   ├── Managers/          # Coordinadores MonoBehaviour y Corrutinas de Unity
│   │   ├── GameManager.cs        # Punto de entrada, inicialización de partida
│   │   ├── TurnManager.cs        # Orquestación de turnos, corrutinas de delays, robo, cantes y fases
│   │   └── CartaVisualManager.cs # Fábrica y posicionamiento visual de cartas
│   ├── View/              # Componentes visuales asociados a Prefabs
│   │   └── CardView.cs           # Representación de carta 2D, animaciones Lerp, input del ratón
│   ├── UI/                # Sistema de Interfaz de Usuario y Pantallas
│   │   ├── DesignTokens.cs       # Paleta de colores, tipografía y tokens de diseño
│   │   ├── UIManager.cs          # Gestor de navegación entre pantallas
│   │   ├── PantallaInicio.cs     # Menú principal (Jugar, Reglas, Opciones)
│   │   ├── PantallaSeleccion.cs  # Configuración de partida y dificultad de IAs
│   │   ├── PantallaReglas.cs     # Resumen de reglas del juego
│   │   └── HUDMesaDeJuego.cs     # HUD durante la partida (marcadores, cantes, triunfo)
│   └── Editor/            # Utilidades para el Editor de Unity
│       └── PlaceholderCardGenerator.cs # Generador procedural de cartas temporales
└── Tests/
    └── EditMode/          # Tests Unitarios (NUnit EditMode) para GuinoteApp.Core
        ├── CartaTests.cs
        ├── EstadoBazaTests.cs
        ├── GestorBarajaTests.cs
        ├── ValidadorReglasTests.cs
        ├── IAFacilTests.cs
        ├── IANormalTests.cs
        └── IADificilTests.cs
```

Diseño visual de referencia disponible en `design_handoff_guinote/`.

## 3. Reglas Inviolables de Arquitectura

### 🛑 Regla 1: Core Debe Ser C# Puro
- Los scripts bajo `GuinoteApp.Core` **JAMÁS** deben importar `UnityEngine` ni heredar de `MonoBehaviour`.
- Deben ser testeables de forma unitaria fuera de Unity (ver `Assets/Tests/EditMode/`).

### 🔄 Regla 2: Comunicación Basada en Eventos
- Los componentes visuales, UI y gestores se comunican a través de eventos (`System.Action` / `event Action<T>`).
- Evitar acoplamiento directo o llamadas en cascada entre clases de las capas View/UI y Core.

### 🎴 Regla 3: Dominio y Dominancia del Guiñote
- **Baraja:** 40 cartas (Oros, Copas, Espadas, Bastos).
- **Puntuación:** As (11), Tres (10), Rey (4), Sota (3), Caballo (2). Total en juego: 120 pts + 10 de últimas = 130 pts.
- **Fases:** `Reparto` → `FaseRobo` → `FaseArrastre` → `FinDePartida`.
- **Reglas de Arrastre (en `ValidadorReglas`):**
  1. *Asistir:* Si tienes del palo de salida, debes jugarlo.
  2. *Montar:* Si asistes, debes superar la mejor carta del mismo palo si puedes.
  3. *Fallar:* Si no puedes asistir, debes jugar triunfo. Si ya hay triunfo en mesa, debes superarlo si puedes.
  4. Si no puedes asistir ni fallar, cualquier carta es válida.
- **Acciones Especiales:**
  - **Cantes (20 y 40):** Rey + Sota del mismo palo tras ganar baza. Obliga a salir con una de las dos cartas.
  - **Cambio del 7:** Cambiar el 7 de triunfo por la pinta visible tras ganar baza.
  - **Variante Cambio del 2:** Si la pinta es ≤ 7 (4, 5, 6, 7), se puede cambiar por el 2 de triunfo.
- **Niveles de IA:**
  - `IAFacil`: Movimientos válidos aleatorios.
  - `IANormal`: Estrategia codiciosa (greedy), busca ganar baza con la carta más alta o descartar la menor.
  - `IADificil`: Conciencia de pareja, conteo de triunfos jugados y conservación de cartas de valor ("ganar barato").

## 4. Estilo de Código y Convenciones
- **Espacios de nombres:** Cada script debe pertenecer a su namespace correspondiente (`GuinoteApp.Core`, `GuinoteApp.Managers`, `GuinoteApp.View`, `GuinoteApp.UI`, `GuinoteApp.Editor`).
- **Nomenclatura:** Usar terminología del juego en español (`Carta`, `Palo`, `Rango`, `Baza`, `Triunfo`, `Arrastre`, `Robo`, `Pareja`, `Cante`, `Cambio`).
- **Campos privados:** Usar `camelCase` o prefijo explicativo.
- **Propiedades y Métodos Públicos:** `PascalCase`.
- **Git:** Ignorar siempre carpetas generadas de Unity y herramientas (`Library/`, `Temp/`, `Logs/`, `UserSettings/`, `.claude/`, `Assets/_Recovery/`).

## 5. Reglas Completas del Juego
Consultar siempre `REGLAS_GUINOTE.md` en la raíz del proyecto para las reglas completas del Guiñote, incluyendo la tabla de estado de implementación (qué está hecho y qué falta).

## 6. Funcionalidades Pendientes (Referencia Rápida)
- **Tute:** 4 Reyes o 4 Sotas → victoria instantánea.
- **Partida a 101 puntos:** Multi-mano con acumulación de puntos.
- **Regla de las 30 malas:** Mínimo 30 pts de juego (sin cantes) para poder ganar.
- **Desempate por últimas en 101:** Si ambas parejas pasan de 101 en la misma mano.
