# Handoff: App de cartas Guiñote (pantalla de inicio + gameplay)

## Overview
Diseños para una app familiar de Guiñote (juego de cartas español), en formato móvil horizontal. Incluye: pantalla de inicio, selección de mesa/compañero, reglas/tutorial y la mesa de juego (gameplay).

## About the Design Files
Los archivos de este paquete son **referencias de diseño hechas en HTML** — mockups estáticos que muestran el aspecto y la estructura previstos, no código de producción para copiar tal cual. La tarea es **recrear estos diseños HTML en el entorno objetivo** (React Native, Flutter, SwiftUI, Android nativo, etc. — lo que use o convenga al proyecto) usando sus patrones y librerías establecidos.

## Fidelity
**Alta fidelidad (hifi)**: colores, tipografía, espaciados y layout están definidos con precisión vía variables CSS del design system "Broadsheet" (ver `_ds/.../styles.css`). Los valores exactos (hex, tamaños, radios) deben tomarse de ahí.

Importante: **esta es una maqueta puramente visual y estática** — no hay interactividad real (no hay lógica de juego, ni validación de reglas, ni estado). El desarrollador debe implementar toda la lógica de juego (reparto, turnos, arrastre, cantes, tanteo) desde cero, usando estos mockups solo como referencia visual/estructural.

## Screens / Views

### 1. Inicio (`data-screen-label="Inicio"`)
- **Propósito**: pantalla de bienvenida; acceso a jugar contra la IA, multijugador, o ver las reglas.
- **Layout**: tarjeta 900×420 (proporción de referencia para landscape), padding `--space-6`, columna. Header superior con logo "GUIÑOTE", "Edición familiar" y línea con nombre de jugador. Cuerpo central: bloque de texto grande a la izquierda ("El juego de cartas de siempre") + 2 botones (`.btn-primary` "Jugar con la IA", `.btn-secondary` "Multijugador") + link "Cómo se juega". A la derecha, 3 `<image-slot>` en abanico (110×158px, rotados -8°/2°/11°) para las cartas ilustrativas (ver Assets).
- **Tipografía**: título `font: 700 56px/1.02 var(--font-heading)`.

### 2. Selección de mesa (`data-screen-label="Selección de compañero/rivales"`)
- **Propósito**: elegir modo (Contra la IA / Multijugador vía `.seg` segmented control), ver a los 4 jugadores en disposición de mesa (grid 3×2), elegir compañero, elegir dificultad (Fácil/Normal/Difícil), botón "Comenzar partida" (`.btn-primary`).
- **Layout**: grid `grid-template-columns: repeat(3,1fr)` con 2 filas; jugadores como `.card` circulares (44-48px avatar); la posición "Tú" resaltada con `--color-accent-2`.

### 3. Reglas/Tutorial (`data-screen-label="Reglas/Tutorial"`)
- **Propósito**: explicar puntuación y cantes antes de jugar.
- **Layout**: grid 2 columnas (1.1fr/1fr). Izquierda: tabla de valores de cartas (As=11, Tres=10, Rey=4, Caballo=3, Sota=2, resto=0) con badge de letra + `.tag-accent-2` de puntos. Derecha: texto de cantes (Las 20, Las 40, Arrastre).

### 4. Mesa de juego (`data-screen-label="Mesa de juego"`)
- **Propósito**: vista principal de gameplay.
- **Layout**: tarjeta 900×600. Header (40px) con tanteo "Nosotros"/"Ellos" en vivo, tag de cante activo (`.tag-outline` "¡Las 40 en bastos!"), y "Mazo: 8" (`.tag-neutral`).
- **Tapete verde** (`background: radial-gradient(... #2f7d52, #1f5e3c, #164a2f)`, bordes marrón `#8a5a2b`) que ocupa el resto de la tarjeta, con disposición **real de mesa**:
  - **Arriba (centro)**: mano del Compañero (dorsos de carta, boca abajo) + su carta jugada debajo, en el centro de la baza.
  - **Izquierda**: mano + carta jugada del Rival izquierdo.
  - **Derecha**: mano + carta jugada del Rival derecho.
  - **Centro exacto**: mazo para robar (dorsos apilados) + carta de triunfo boca arriba debajo/asomando.
  - **Abajo (centro)**: tu carta jugada, y debajo tu mano en abanico (boca arriba), todo sobre el tapete (no en una barra separada).
  - Todas las cartas de mano de rivales/compañero son del mismo tamaño que una carta jugada (48×68px de referencia) para relación de escala realista.

## Interactions & Behavior
Ninguna implementada (mockup estático). El desarrollador deberá construir:
- Reparto/robo de cartas y turnos rotativos (sentido de la partida).
- Reglas de guiñote clásico (2vs2) e individual (1vs1), arrastre obligatorio al agotar mazo.
- Cantes (Las 20 / Las 40) con validación y suma al marcador.
- Tanteo en tiempo real hasta 101 puntos.
- Selección de compañero/rivales y dificultad de IA.

## State Management
Sugerido (no implementado): estado de partida (mazo, mano de cada jugador, baza actual, marcador por pareja, palo de triunfo, turno actual, cantes disponibles), y estado de sesión/lobby (modo, jugadores, dificultad).

## Design Tokens
Tomar todos los valores de `_ds/broadsheet-3f1915db-077d-4d8f-8a37-cd3b9b901aed/styles.css` (sistema "Broadsheet"): colores (`--color-bg`, `--color-text`, `--color-accent` #0088b0, `--color-accent-2` #d6006c y sus rampas), tipografía Source Serif 4 (`--font-heading`/`--font-body`), espaciados `--space-1..8`, radios `--radius-*`, sombras `--shadow-sm/md/lg`.
Colores específicos de tapete/mesa (no forman parte del design system, son propios de esta pantalla): verde `#2f7d52 / #1f5e3c / #164a2f`, madera `#8a5a2b`, dorso de carta `#3a2a18`.

## Assets
- Las cartas de la baraja están representadas como `<image-slot>` (placeholders para arrastrar imágenes) — el usuario dijo que quiere usar la **baraja española tradicional de Heraclio Fournier**; no se generaron ilustraciones de cartas por derechos de autor. El desarrollador debe conseguir/licenciar escaneos o assets oficiales de esa baraja (u otra baraja española con licencia) e insertarlos en lugar de los slots vacíos.
- IDs de slots relevantes en pantalla 4: `trump-card-table` (triunfo central), `trick-1`/`trick-2` (cartas jugadas visibles), `hand-1..6` (mano del jugador). En pantalla 1: `hero-card-1/2/3`. En pantalla 3: badges de letra (no requieren imagen).

## Files
- `Guiñote App.dc.html` — el archivo de diseño completo (4 pantallas).
- `_ds/broadsheet-3f1915db-077d-4d8f-8a37-cd3b9b901aed/` — design system Broadsheet (styles.css, tokens, guía).
- `image-slot.js`, `support.js` — utilidades del entorno de diseño (no necesarias en producción; sirven solo para previsualizar este HTML).
