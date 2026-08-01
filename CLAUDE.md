# CLAUDE.md - Instructions for Claude Code & AI Assistants

## Project Overview
**Guiñote** is a 2D Spanish card game developed in **Unity (C#)** for 4 players in 2 teams (Player 1 [Human] + Player 3 [AI] vs Player 2 [AI] + Player 4 [AI]).

## Architecture & Layer Boundaries

The codebase enforces strict Separation of Concerns (Clean Architecture / MVC pattern):

1. **`GuinoteApp.Core` (`Assets/Scripts/Core/`)**:
   - **PURE C# ONLY.** Absolute rule: **NO `UnityEngine` dependencies or `MonoBehaviour` inheritance**.
   - Contains domain models (`Carta`, `Palo`, `Rango`, `GamePhase`), card deck manager (`GestorBaraja`), trick state (`EstadoBaza`), rules validator (`ValidadorReglas`), and AI strategy interfaces (`IAJugador`).
   - Must be fully unit testable without Unity runtime.

2. **`GuinoteApp.Managers` (`Assets/Scripts/Managers/`)**:
   - Unity `MonoBehaviour` components orchestrating game flow, timing delays, coroutines, and event bridging.
   - `GameManager`: Entry point and high-level coordinator.
   - `TurnManager`: Turn orchestration, coroutines for AI delays, card drawing phase, trick resolution.
   - `CartaVisualManager`: Visual factory for instantiating, positioning, and destroying card GameObjects.

3. **`GuinoteApp.View` (`Assets/Scripts/View/`)**:
   - Attached to Unity 2D Prefabs (`CardView`).
   - Handles visual presentation (face/back sprite switching), smooth `Vector3.Lerp` movements, selection elevation, mouse hover scaling, and click callbacks.

4. **`GuinoteApp.Editor` (`Assets/Scripts/Editor/`)**:
   - Unity Editor utilities and procedural placeholder card generators.

## Key Game Rules (Guiñote Domain)

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

## Code Style & Conventions

- **Namespaces**: Always declare explicit namespaces matching file location (`GuinoteApp.Core`, `GuinoteApp.Managers`, `GuinoteApp.View`, `GuinoteApp.Editor`).
- **Domain Language**: Use standard Spanish Guiñote terminology for domain concepts (`Carta`, `Palo`, `Rango`, `Baza`, `Triunfo`, `Arrastre`, `Robo`, `Pareja`).
- **Event-Driven UI**: Communicate between Managers and View using C# events (`event Action<...>`). Avoid tight direct coupling.
- **Git Compliance**: Keep `.gitignore` updated for Unity (never commit `Library/`, `Temp/`, `UserSettings/`, `Logs/`).

## Useful Commands / Workflow
- **Git status & commits**: Stage specific asset changes (`Assets/Scripts/`) and write clear commit messages in Spanish.
- **Build / Verification**: Ensure pure C# Core logic compiles clean without Unity editor warnings.

## Complete Game Rules
See `REGLAS_GUINOTE.md` at the project root for the full rules of Guiñote, including an implementation status table showing what's done and what's pending.

## Pending Features (Quick Reference)
- **Tute:** Holding all 4 Kings or all 4 Jacks → instant win.
- **Match to 101 points:** Multi-hand game with score accumulation.
- **30 Malas Rule:** Must earn at least 30 trick-points (excluding cantes) to be able to win.
- **Tiebreak by últimas at 101:** If both teams exceed 101 in the same hand.
