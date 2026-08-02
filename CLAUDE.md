# CLAUDE.md - Instructions for Claude Code & AI Assistants

## Project Overview
**Guiñote** is a 2D Spanish card game developed in **Unity (C#)** for 4 players in 2 teams (Player 1 [Human] + Player 3 [AI] vs Player 2 [AI] + Player 4 [AI]).

## Architecture & Layer Boundaries

The codebase enforces strict Separation of Concerns (Clean Architecture / MVC pattern):

1. **`GuinoteApp.Core` (`Assets/Scripts/Core/`)**:
   - **PURE C# ONLY.** Absolute rule: **NO `UnityEngine` dependencies or `MonoBehaviour` inheritance**.
   - Contains domain models (`Carta`, `Palo`, `Rango`, `GamePhase`), card deck manager (`GestorBaraja`), trick state (`EstadoBaza`), rules validator (`ValidadorReglas`), cantes/cambio logic, and AI strategy implementations (`IAJugador`, `IAFacil`, `IANormal`, `IADificil`, `IAFactory`).
   - Fully unit-testable outside Unity runtime.

2. **`GuinoteApp.Managers` (`Assets/Scripts/Managers/`)**:
   - Unity `MonoBehaviour` components orchestrating game flow, timing delays, coroutines, and event bridging.
   - `GameManager`: Entry point and high-level coordinator.
   - `TurnManager`: Turn orchestration, coroutines for AI delays, card drawing phase, trick resolution, cantes & triunfo exchange execution.
   - `CartaVisualManager`: Visual factory for instantiating, positioning, and destroying card GameObjects.

3. **`GuinoteApp.View` (`Assets/Scripts/View/`)**:
   - Attached to Unity 2D Prefabs (`CardView`).
   - Handles visual presentation (face/back sprite switching), smooth `Vector3.Lerp` movements, selection elevation, mouse hover scaling, and click callbacks.

4. **`GuinoteApp.UI` (`Assets/Scripts/UI/`)**:
   - UI screen management and design system implementation (`UIManager`, `PantallaInicio`, `PantallaSeleccion`, `PantallaReglas`, `HUDMesaDeJuego`, `DesignTokens`).
   - Matches visual design handoff specs located in `design_handoff_guinote/`.

5. **`GuinoteApp.Editor` (`Assets/Scripts/Editor/`)**:
   - Unity Editor utilities and procedural placeholder card generators (`PlaceholderCardGenerator`).

6. **`GuinoteApp.Core.Tests` (`Assets/Tests/EditMode/`)**:
   - NUnit EditMode unit tests for Core domain logic (`CartaTests`, `EstadoBazaTests`, `GestorBarajaTests`, `ValidadorReglasTests`, `IAFacilTests`, `IANormalTests`, `IADificilTests`).

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
  - **Cantes (20 y 40)**: Rey + Sota of same suit after winning a trick. 40 pts if Trump suit, 20 pts if non-Trump. Mandatory lead with Rey or Sota upon calling.
  - **Cambio del 7**: Swap 7 of Trump suit for turned-up Trump card after winning a trick.
  - **Cambio del 2**: Variant when turned-up Trump card is rank 7 or lower (4, 5, 6, 7), allows swapping with 2 of Trump suit.
- **AI Difficulty Levels**:
  - **Fácil (`IAFacil`)**: Plays legal moves randomly without tactical optimization.
  - **Normal (`IANormal`)**: Greedy strategy — plays highest winning card or lowest discard.
  - **Difícil (`IADificil`)**: Partner-aware (avoids overwriting partner's win), counts played trumps before leading, conserves high cards ("wins cheap").

## Code Style & Conventions

- **Namespaces**: Always declare explicit namespaces matching file location (`GuinoteApp.Core`, `GuinoteApp.Managers`, `GuinoteApp.View`, `GuinoteApp.UI`, `GuinoteApp.Editor`).
- **Domain Language**: Use standard Spanish Guiñote terminology for domain concepts (`Carta`, `Palo`, `Rango`, `Baza`, `Triunfo`, `Arrastre`, `Robo`, `Pareja`, `Cante`, `Cambio`).
- **Event-Driven UI**: Communicate between Managers and View/UI using C# events (`event Action<...>`). Avoid tight direct coupling.
- **Git Compliance**: Keep `.gitignore` updated for Unity and tooling (never commit `Library/`, `Temp/`, `UserSettings/`, `Logs/`, `.claude/`, `Assets/_Recovery/`).

## Complete Game Rules & Status
See `REGLAS_GUINOTE.md` at the project root for full Guiñote rules and implementation status table.

## Pending Features (Quick Reference)
- **Tute:** Holding all 4 Kings or all 4 Jacks → instant win.
- **Match to 101 points:** Multi-hand game with score accumulation across hands.
- **30 Malas Rule:** Must earn at least 30 trick-points (excluding cantes) to be able to win.
- **Tiebreak by últimas at 101:** If both teams exceed 101 in the same hand.
