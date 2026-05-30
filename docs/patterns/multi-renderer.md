---
title: Multi-Renderer Compositing
category: Patterns
categoryindex: 6
index: 65
---

# Multi-Renderer Compositing

## What and Why

Most games need a 3D world with 2D elements on top — HUD text, minimaps, debug overlays, health bars. Mibo.Raylib handles this with multiple renderers that composite in order.

The critical detail: **renderers execute in registration order**, and the 2D overlay renderer must not clear the screen — otherwise it erases the 3D scene underneath.

## When to use

- You have a 3D scene and need 2D UI on top.
- You want a minimap, diagnostics overlay, or HUD.
- You want to separate rendering concerns into independent view functions.
- You need different rendering pipelines for different layers.

## Quick Start

```fsharp
Program.mkProgram init update
|> Program.withRenderer (fun () ->
    Renderer2D.createWith Renderer2DConfig.noClear overlayView)
|> Program.withRenderer (fun () ->
    Renderer3D.create pipeline sceneView)
```

> _**IMPORTANT**_: Register the 2D overlay **first**, then the 3D scene. The 3D renderer clears the screen. The 2D renderer draws on top without clearing.

## Deep Dive

### Why this order works

1. **3D renderer runs** — clears the screen, renders the 3D scene.
2. **2D renderer runs** — draws minimap and diagnostics text on top, without clearing.

If you reversed the order, the 3D clear would erase the 2D content.

### The overlay view function

The overlay view is a normal 2D view function. It draws on a `RenderBuffer2D`:

```fsharp
let overlayView (ctx: GameContext) (model: GameModel) (buffer: RenderBuffer2D) =
  drawMinimap model buffer
  drawDiagnostics model buffer
  drawHealthBar model buffer
```

Each helper draws its own content — the overlay view just composes them.

### `Renderer2DConfig.noClear`

```fsharp
Renderer2DConfig.noClear
```

This tells the 2D renderer to skip clearing the framebuffer. Without it, the 2D pass would fill the screen with a solid color, hiding the 3D scene.

### What each renderer handles

| Renderer | Clears screen? | Draws to | Typical content |
|----------|---------------|----------|-----------------|
| `Renderer3D.create` | Yes | 3D buffer | World, models, lights, shadows |
| `Renderer2D.createWith noClear` | No | 2D buffer | HUD, minimap, text, debug |

### Use cases

| Use case | How |
|----------|-----|
| HUD overlay | `Renderer2D.createWith noClear` + text/sprite drawing |
| Minimap | 2D renderer with a texture generated from world data |
| Debug overlay | 2D renderer drawing lines, text, and shapes |
| Split-screen | Two `Renderer3D.create` with different viewports |
| Post-process | Single 3D renderer with a custom pipeline |

> _**TIP**_: You can add more than two renderers. Each one composites on top of the previous. Just make sure only the first (or the 3D one) clears the screen.

### Alternative: 2D overlay inside 3D

For some UI elements (health bars above enemies), you might want to draw 2D content inside the 3D view using `Draw3D.drawBillboard`. Use multi-renderer compositing for screen-space UI (HUD, minimap) and billboards for world-space UI.

### For a complete example

See `samples/ThreeDSample/Program.fs` for a full renderer setup.

See also: [3D Rendering Overview](graphics3d/overview.html), [Rendering Overview](rendering.html).
