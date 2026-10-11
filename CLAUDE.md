# CLAUDE.md - Instructions for Claude Code & AI Assistants

## Project Overview
**Guiñote** is a 2D Spanish card game developed in **Unity (C#)** for 4 players in 2 teams (Player 1 [Human] + Player 3 [AI] vs Player 2 [AI] + Player 4 [AI]).

## Architecture & Layer Boundaries

The codebase enforces strict Separation of Concerns (Clean Architecture / MVC pattern):

1. **`GuinoteApp.Core` (`Assets/Scripts/Core/`)**:
   - **PURE C# ONLY.** Absolute rule: **NO `UnityEngine` dependencies or `MonoBehaviour` inheritance**.
   - Contains domain models (`Carta`, `Palo`, `Rango`, `GamePhase`), card deck manager (`GestorBaraja`), trick state (`EstadoBaza`), rules validator (`ValidadorReglas`), cantes/cambio rules (`ReglasEspeciales`), a hand simulator (`SimuladorMano`), and AI strategy implementations (`IAJugador`, `ContextoIA`, `HeuristicasIA`, `IAFacil`, `IANormal`, `IADificil`, `IAExperto`, `BuscadorMonteCarlo`, `IAFactory`).
   - Fully unit-testable outside Unity runtime.

2. **`GuinoteApp.Managers` (`Assets/Scripts/Managers/`)**:
   - Unity `MonoBehaviour` components orchestrating game flow, timing delays, coroutines, and event bridging.
   - `GameManager`: Entry point and high-level coordinator.
   - `TurnManager`: Turn orchestration, coroutines for AI delays, card drawing phase, trick resolution, cantes & triunfo exchange execution.
   - `CartaVisualManager`: Visual factory for instantiating, positioning, and destroying card GameObjects.

3. **`GuinoteApp.View` (`Assets/Scripts/View/`)**:
   - Attached to Unity 2D Prefabs (`CardView`).
   - Handles visual presentation (face/back sprite switching), smooth `Vector3.Lerp` movements, selection elevation, mouse hover scaling, and click callbacks.

   - `AudioManager` (singleton, sound effects by `TipoSonido`; `AudioManager.Reproducir(...)` is null-safe and called from `GameManager`).

3b. **`GuinoteApp.Network` (`Assets/Scripts/Network/`)**:
   - Transport: `RedManager` (singleton) picks `IRedGuiñoteServicio`: `BluetoothAndroidServicio` (RFCOMM via JNI, star topology, host accepts 1–3 guests) or `InternetRelayServicio` (WebSocket; relay in `Tools/RelayServer/server.js`). Messages are `MensajeRedGuiñote` (type, sender seat, destination seat, JSON payload); DTOs in `MensajesPartida.cs`.
   - Multiplayer (2 or 4 humans) is **host-authoritative**: the host (seat 0) runs the whole game with `TurnManager`/`GameManager`; `PartidaRedAnfitrion` (Managers) manages the room, broadcasts TurnManager events (each guest only gets its own cards), feeds guest moves into `TurnManager.ProcesarJugadaDe/ResponderCanteDe/ResponderCambioDe` and replaces silent guests (heartbeat) with AI via `TurnManager.SustituirPorIA`. Guests run `PartidaRedCliente`, a thin view that rotates seats so the local player is always at the bottom. `LobbyRed` (IMGUI) creates/joins rooms, opened from the "Multijugador" button of `PantallaSeleccion`.
   - `TurnManager` supports several human seats (`ConfigurarHumanos`, `EsHumano`); its decision events carry the seat.

4. **`GuinoteApp.UI` (`Assets/Scripts/UI/`)**:
   - UI screen management and design system implementation (`UIManager`, `PantallaInicio`, `PantallaSeleccion`, `PantallaReglas`, `HUDMesaDeJuego`, `DesignTokens`).
   - Matches visual design handoff specs located in `design_handoff_guinote/`.

5. **`GuinoteApp.Editor` (`Assets/Scripts/Editor/`)**:
   - Unity Editor utilities and procedural placeholder card generators (`PlaceholderCardGenerator`).

6. **`GuinoteApp.Core.Tests` (`Assets/Tests/EditMode/`)**:
   - NUnit EditMode unit tests for Core domain logic (`CartaTests`, `EstadoBazaTests`, `GestorBarajaTests`, `ValidadorReglasTests`, `ReglasEspecialesTests`, `SimuladorManoTests`, `HeuristicasIATests`, `IAFacilTests`, `IANormalTests`, `IADificilTests`, `IAExpertoTests`, `NivelesIATests`).

## Key Game Rules & Implementation Features

- **Deck**: 40 Spanish cards (Oros, Copas, Espadas, Bastos).
- **Points**: As (11), Tres (10), Rey (4), Sota (3), Caballo (2). Total = 120 points (+10 for last trick "las 10 de últimas" = 130 points total).
- **Game Phases**:
  - `Reparto`: Deal 6 cards per player + 1 turned-up Trump card (`triunfo`).
  - `FaseRobo`: Drawing Phase. Any card can be played. Players draw after each trick until deck is empty.
  - `FaseArrastre`: Dragging Phase (empty deck). Mandatory rules enforced by `ValidadorReglas.EsJugadaValidaArrastre`:
    1. **Asistir**: Must follow suit if player has cards of the led suit.
    2. **Montar**: If following suit, must beat current winning card of that suit if possible.
    3. **Fallar**: If unable to follow suit, must play a Trump card. If highest card on table is already Trump, must over-trump if possible.
    4. If unable to follow suit or trump, any card can be played.
  - `FinDePartida`: Game over, score evaluation (+10 bonus to last trick winner).
- **Special Actions**:
  - **Cantes (20 y 40)**: Rey + Sota of same suit after winning a trick. 40 pts if Trump suit, 20 pts if non-Trump. Either player of the team that won the trick may call (one suit per trick). Calling does NOT force any particular card to be played.
  - **Cambio del 7**: Swap 7 of Trump suit for turned-up Trump card after winning a trick.
  - **Cambio del 2**: Variant when turned-up Trump card is rank 7 or lower (4, 5, 6, 7), allows swapping with 2 of Trump suit.
- **AI Difficulty Levels**:
  - Every AI receives a `ContextoIA` (public information only: own hand, played cards, pinta, score, cante) and also decides **whether to sing a cante** (`DecidirCante`) and **whether to swap the pinta** (`DecidirCambio`). The human decides both through `TurnManager.ResponderCanteHumano` / `ResponderCambioHumano` (HUD prompt `PanelDecisionHumana`).
  - **Fácil (`IAFacil`)**: Legal moves, mostly random; tries to win blindly (even over its partner's card); often forgets to sing or swap.
  - **Normal (`IANormal`)**: Common-sense heuristics, no memory: leads cheap, wins the trick with the weakest winning card only if it is worth it, never wastes trumps for little, helps its partner only when last to play. Always sings/swaps.
  - **Difícil (`IADificil`)**: Normal + card memory (probability that a card is beaten), protects Rey+Sota pairs and the trump 7, and in Fase Arrastre runs a Monte Carlo search (`BuscadorMonteCarlo`) over possible hidden hands.
  - **Experto (`IAExperto`)**: Monte Carlo search (many simulated hands via `SimuladorMano`) in the whole game, not only the endgame. Never looks at hidden hands.
  - Each AI player (Rival 1 = J2, Compañero = J3, Rival 2 = J4) has its own level, chosen in `PantallaSeleccion` (a per-card "◀ Nivel ▶" button, plus the Fácil/Normal/Difícil/Experto buttons that set all three at once) and applied with `TurnManager.ConfigurarDificultades`.
  - `NivelesIATests` checks that each level beats the lower one in simulated hands.

## Scoring: Malas y Buenas, Juegos and Coto
- Points accumulate hand after hand in a `MarcadorEquipo` (Core): 0–49 = "Malas", 50+ = "Buenas" (shown as `PuntosTotales - 50`). A team wins the **Juego** when it exceeds 100 points.
- **Idas** (first hand of a Juego) are always played to the end. If both teams exceed 100, the team that made "las 10 de últimas" wins (a team under 30 trick-points without cantes loses) (`CotoPartida.GanadorDesempateIdas`).
- **Vueltas** (second hand onward in a Juego): the hand is cut the instant a team exceeds 100 total points, after a trick or a cante (`TurnManager.AlguienGanaElJuego`, enabled by `MatchManager.EsManoDeVueltas`). The first player of a new Juego is the winner of the last trick of the previous Juego.
- A **Coto** is a series of Juegos: the first team to win 3 or 5 Juegos (chosen on the selection screen via `MenuConfiguracion`) wins the match. Rules live in `CotoPartida` (Core); `MatchManager` (Managers) wraps them, logs the state after each hand (e.g. "Equipo 1: 15 Buenas | Equipo 2: 40 Malas") and raises events. `GameManager` chains hands: after each hand it asks to continue with the next hand/Juego, or shows the final screen when the Coto ends. Team 1 = players 1+3 (human), Team 2 = players 2+4.

## Code Style & Conventions

- **Namespaces**: Always declare explicit namespaces matching file location (`GuinoteApp.Core`, `GuinoteApp.Managers`, `GuinoteApp.View`, `GuinoteApp.UI`, `GuinoteApp.Editor`).
- **Domain Language**: Use standard Spanish Guiñote terminology for domain concepts (`Carta`, `Palo`, `Rango`, `Baza`, `Triunfo`, `Arrastre`, `Robo`, `Pareja`, `Cante`, `Cambio`).
- **Event-Driven UI**: Communicate between Managers and View/UI using C# events (`event Action<...>`). Avoid tight direct coupling.
- **Git Compliance**: Keep `.gitignore` updated for Unity and tooling (never commit `Library/`, `Temp/`, `UserSettings/`, `Logs/`, `.claude/`, `Assets/_Recovery/`).

## Complete Game Rules & Status
See `REGLAS_GUINOTE.md` at the project root for full Guiñote rules and implementation status table.

## Pending Features (Quick Reference)
- **Tute:** Holding all 4 Kings or all 4 Jacks → instant win.
- **30 Malas Rule:** Must earn at least 30 trick-points (excluding cantes) to be able to win.
