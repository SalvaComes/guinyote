# Guía de Setup en Unity Editor

> Referencia: Unity 6000.3.3f1 · Proyecto Guiñote

Esta guía describe paso a paso cómo configurar las escenas y los GameObjects en Unity para que los scripts de UI y gameplay funcionen con el diseño visual.

---

## 1. Configuración del proyecto

### 1.1 Orientación landscape
1. `Edit → Project Settings → Player → Resolution and Presentation`
2. **Default Orientation** → `Landscape Left`
3. **Allowed Orientations** → desmarcar todo excepto `Landscape Left` y `Landscape Right`

### 1.2 Instalar TextMeshPro (si no está)
1. `Window → Package Manager → TextMeshPro` → Install
2. Al importar el proyecto por primera vez → `Import TMP Essentials`

### 1.3 Fuente Source Serif 4 (diseño original)
1. Descargar de [Google Fonts – Source Serif 4](https://fonts.google.com/specimen/Source+Serif+4)
2. Importar los archivos `.ttf` en `Assets/Fonts/`
3. Crear TMP Font Assets: clic derecho → `Create → TextMeshPro → Font Asset`
   - `SourceSerif4-Regular.ttf` → `SourceSerif4-Regular SDF.asset`
   - `SourceSerif4-SemiBold.ttf` → `SourceSerif4-SemiBold SDF.asset`
4. Configurar como fuente por defecto: `Edit → Project Settings → TextMesh Pro → Default Font Asset`

---

## 2. Estructura de la escena principal

La escena principal (`SampleScene.unity`) debe tener la siguiente jerarquía:

```
Main Camera
EventSystem
─────────────────────────────────────────
[GameManager_Root]          ← GameObject vacío (scripts de lógica)
│  └─ GameManager.cs
│  └─ TurnManager.cs
│
[CartaVisualManager_Root]   ← GameObject vacío (scripts visuales)
│  └─ CartaVisualManager.cs
│     ├─ Anchors (GameObjects vacíos de posicionamiento)
│     │   ├─ AnchorajeManoLocal      (abajo, centro)
│     │   ├─ AnchorajeManoArriba     (compañero, arriba)
│     │   ├─ AnchorajeManoIzquierda  (rival izquierdo)
│     │   ├─ Anchoraje ManoDerecha   (rival derecho)
│     │   ├─ AnclajeTriunfo          (centro, ligeramente derecha)
│     │   └─ AnclajeMazo             (centro, ligeramente izquierda)
│     └─ BazaPositions (GameObjects vacíos de posiciones de cartas jugadas)
│         ├─ PosCartaJ0  (abajo, centro de baza)
│         ├─ PosCartaJ1  (izquierda del centro)
│         ├─ PosCartaJ2  (arriba del centro)
│         └─ PosCartaJ3  (derecha del centro)
│
[Canvas] (Screen Space – Camera)
│  ├─ UIManager.cs (en el Canvas root)
│  │
│  ├─ [Panel_Inicio]         ← PantallaInicio.cs
│  │   ├─ Header
│  │   │   ├─ TMP: Logo "GUIÑOTE"
│  │   │   ├─ TMP: "Edición familiar" (italic)
│  │   │   └─ Divider Line (Image, negro, 3px alto)
│  │   ├─ SubHeader
│  │   │   └─ TMP: "Jugador: X · Baraja española"
│  │   ├─ Body (Horizontal Layout)
│  │   │   ├─ LeftColumn
│  │   │   │   ├─ TMP: Título "El juego de..."  (font 56px, bold)
│  │   │   │   ├─ ButtonsRow
│  │   │   │   │   ├─ Button "Jugar con la IA"   (btn-primary: bg #0088b0)
│  │   │   │   │   └─ Button "Multijugador"       (btn-secondary: borde, disabled)
│  │   │   │   └─ Button "Cómo se juega →"       (btn-ghost: color #0088b0)
│  │   │   └─ RightColumn (cartas en abanico)
│  │   │       ├─ Image HeroCard1  (110×158, rotation Z = -8°)
│  │   │       ├─ Image HeroCard2  (110×158, rotation Z = 2°,  z-index alto)
│  │   │       └─ Image HeroCard3  (110×158, rotation Z = 11°)
│  │
│  ├─ [Panel_Seleccion]      ← PantallaSeleccion.cs
│  │   ├─ Header
│  │   │   ├─ TMP: "Elige tu mesa" (h2 32px)
│  │   │   └─ SegControl_Modo
│  │   │       ├─ Btn "Contra la IA"  (selected: bg #0088b0, text white)
│  │   │       └─ Btn "Multijugador"  (disabled)
│  │   ├─ Grid 3×2 (GridLayoutGroup)
│  │   │   ├─ Card_Rival1     (avatar 44px circular, texto "Rival · Normal")
│  │   │   ├─ Card_Companero  (avatar 44px, borde dashed #0088b0, "+")
│  │   │   ├─ Card_Rival2     (avatar 44px circular, texto "Rival · Normal")
│  │   │   ├─ Empty           (celda vacía)
│  │   │   ├─ Card_Tu         (avatar 48px, borde solid #d6006c, texto "TÚ")
│  │   │   └─ Empty           (celda vacía)
│  │   └─ Footer
│  │       ├─ SegControl_Dificultad
│  │       │   ├─ Btn "Fácil"
│  │       │   ├─ Btn "Normal"   (selected por defecto)
│  │       │   └─ Btn "Difícil"
│  │       └─ Button "Comenzar partida" (btn-primary)
│  │
│  ├─ [Panel_Reglas]         ← PantallaReglas.cs
│  │   ├─ Header
│  │   │   ├─ TMP: "Cómo se juega" (h2)
│  │   │   └─ TMP: "Dos parejas enfrentadas · gana...101 puntos"
│  │   ├─ Body (Horizontal Layout, 2 columnas)
│  │   │   ├─ LeftColumn
│  │   │   │   ├─ TMP: "VALOR DE LAS CARTAS" (caption uppercase)
│  │   │   │   └─ Filas de cartas (x6 TMP_Text)
│  │   │   └─ RightColumn
│  │   │       ├─ TMP: "CANTES" (caption uppercase)
│  │   │       ├─ TMP: Las 20
│  │   │       ├─ TMP: Las 40
│  │   │       └─ TMP: Arrastre
│  │   └─ Button "← Volver"
│  │
│  └─ [Panel_MesaDeJuego]    ← HUDMesaDeJuego.cs
│      ├─ HUD_Header (40px, bottom border 1px)
│      │   ├─ ScoreLeft
│      │   │   ├─ TMP: "Nosotros" (bold)
│      │   │   └─ TMP: [puntosA]  (color #006786)
│      │   ├─ ScoreRight
│      │   │   ├─ TMP: "Ellos" (bold)
│      │   │   └─ TMP: [puntosB]  (color #aa0b56)
│      │   ├─ Tag_Cante        (outline #0088b0, hidden initially)
│      │   │   └─ TMP: "¡Las 40 en bastos!"
│      │   └─ Tag_Mazo         (neutral bg #f8f4f4)
│      │       └─ TMP: "Mazo: 15"
│      └─ Panel_FinPartida     (hidden initially, overlay)
│          ├─ TMP: Resultado   ("¡Victoria!" / "Derrota" / "Empate")
│          ├─ TMP: Puntos finales
│          └─ Button "Volver al menú"
```

---

## 3. Configuración del Tapete (Mesa de juego)

El tapete verde se implementa mediante un **SpriteRenderer** en un GameObject de la escena 2D (no en el Canvas):

```
[Tapete]                         ← GameObject de escena, NO en Canvas
│  ├─ SpriteRenderer (sprite: cuadrado blanco, color: #1f5e3c)
│  │   → Ajustar al tamaño de la cámara en landscape
│  │
│  ├─ [BordeSuperior]           ← Image strip de 0.3u de alto
│  │   └─ SpriteRenderer (color #8a5a2b)
│  │
│  └─ [BordeInferior]           ← Image strip de 0.3u de alto
│      └─ SpriteRenderer (color #8a5a2b)
```

> **Tip:** Para el gradiente radial del diseño, puedes crear un material con un Shader Graph de gradiente radial o usar una textura de gradiente importada. El color plano `#1f5e3c` es un buen punto de partida sin shader.

---

## 4. Prefab de Carta

El prefab de carta debe tener:

```
[CartaPrefab]
├─ SpriteRenderer          (sorting layer: "Cards")
├─ BoxCollider2D            (size: ~1×1.4 para proporción carta)
└─ CardView.cs              (script adjunto)
```

Asignar el prefab en el campo `cartaPrefab` de `CartaVisualManager`.

---

## 5. Posiciones de anclas en la escena

Coordenadas de referencia para **aspect ratio 16:9 landscape** (cámara ortográfica size 5):

| Ancla                    | Posición sugerida         |
|--------------------------|--------------------------|
| `AnchorajeManoLocal`     | (0, -4.0, 0)             |
| `AnchorajeManoArriba`    | (0,  3.5, 0)             |
| `AnchorajeManoIzquierda` | (-7.0, 0, 0)             |
| `Anchoraje ManoDerecha`  | ( 7.0, 0, 0)             |
| `AnclajeTriunfo`         | ( 0.8, 0, 0)             |
| `AnclajeMazo`            | (-0.8, 0, 0)             |
| `PosCartaJ0`             | ( 0, -1.5, 0)            |
| `PosCartaJ1`             | (-2.0, 0, 0)             |
| `PosCartaJ2`             | ( 0,  1.5, 0)            |
| `PosCartaJ3`             | ( 2.0, 0, 0)             |

> Ajustar según la resolución real y el tamaño de la cámara del proyecto.

---

## 6. Asignación de referencias en Inspector

### GameManager
| Campo | Asignar |
|-------|---------|
| `cartaVisualManager` | `CartaVisualManager_Root` |
| `turnManager` | `GameManager_Root` (mismo GO) |
| `uiManager` | `Canvas` |
| `hudMesa` | `Canvas/Panel_MesaDeJuego` |
| `pantallaSeleccion` | `Canvas/Panel_Seleccion` |

### CartaVisualManager
| Campo | Asignar |
|-------|---------|
| `cartaPrefab` | `Assets/Prefabs/CartaPrefab.prefab` |
| `puntoAnclajeManoPrincipal` | `AnchorajeManoLocal` |
| `puntoAnclajeManoArriba` | `AnchorajeManoArriba` |
| `puntoAnclajeManoIzquierda` | `AnchorajeManoIzquierda` |
| `puntoAnclajeManoDerecha` | `Anchoraje ManoDerecha` |
| `puntoAnclajeTriunfo` | `AnclajeTriunfo` |
| `puntoAnclajeMazo` | `AnclajeMazo` |
| `posCartaJugadaJ0..J3` | `PosCartaJ0..J3` |

### UIManager
| Campo | Asignar |
|-------|---------|
| `panelInicio` | `Canvas/Panel_Inicio` |
| `panelSeleccion` | `Canvas/Panel_Seleccion` |
| `panelReglas` | `Canvas/Panel_Reglas` |
| `panelMesaDeJuego` | `Canvas/Panel_MesaDeJuego` |

---

## 7. Colores de botones (sin TextMeshPro Styles)

Configura los colores directamente en el componente `Button` → `Colors`:

| Botón | Normal | Highlighted | Pressed |
|-------|--------|-------------|---------|
| `.btn-primary` | `#0088b0` | `#1186ac` | `#006786` |
| `.btn-secondary` | transparent | `rgba(#201e1d, 7%)` | `rgba(#201e1d, 14%)` |
| `.btn-ghost` | transparent | `rgba(#0088b0, 10%)` | `rgba(#0088b0, 18%)` |

---

## 8. Verificación rápida

Al dar Play:
1. ✅ Se debe ver la **Pantalla de Inicio** con el botón "Jugar con la IA" activo.
2. ✅ Click "Jugar con la IA" → navega a **Selección de mesa** con fade.
3. ✅ Click "Comenzar partida" → navega a **Mesa de juego** y arranca la partida.
4. ✅ El tapete verde es visible, las cartas del jugador aparecen abajo en abanico.
5. ✅ Las cartas de los otros 3 jugadores se muestran como dorsos en sus posiciones.
6. ✅ El marcador "Nosotros 0 — Ellos 0" es visible en el HUD.
7. ✅ Al finalizar la partida, aparece el panel de resultado.
