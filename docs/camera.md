---
title: Camera
category: Rendering
categoryindex: 3
index: 13
---

# Camera

Mibo.Raylib uses raylib's built-in camera types for both 2D and 3D rendering:

- **2D rendering**: raylib's `Camera2D` mutable struct
- **3D rendering**: raylib's `Camera3D` mutable struct

The 2D renderer consumes cameras via `Draw.beginCamera`. The `Camera3D` module provides helpers that produce a renderer-agnostic `Camera` struct (view + projection matrices) for use with the 3D pipeline.

Both 2D and 3D cameras support config-based rendering via `Camera2DConfig` and `Camera3DConfig`, enabling viewport-based rendering, split-screen, and overlay patterns.

## 2D cameras (`Camera2D`)

Raylib's `Camera2D` is a mutable struct:

```fsharp
let mutable cam = Camera2D()
cam.Target <- Vector2(400f, 300f)   // world position to center on
cam.Offset <- Vector2(640f, 360f)   // screen offset (typically center)
cam.Rotation <- 0f                  // rotation in radians
cam.Zoom <- 1.0f                    // zoom factor
```

In the Mibo.Raylib 2D renderer, you use `Draw.beginCamera` with raylib's `Camera2D` struct:

```fsharp
let camera = Camera2D.create (Vector2(400f, 300f)) 1.0f viewportSize

buffer
|> Draw.beginCamera 0<RenderLayer> camera
|> // ... draw world content ...
|> Draw.endCamera 1000<RenderLayer>
```

The `Camera2D` module in `Mibo.Elmish` provides helpers:

- `Camera2D.create position zoom viewportSize` — create a camera centered on a position
- `Camera2D.screenToWorld camera screenPos` — convert screen to world coordinates
- `Camera2D.worldToScreen camera worldPos` — convert world to screen coordinates
- `Camera2D.viewportBounds camera width height` — get visible world rectangle (for culling)
- `Camera2D.smoothFollow &camera target speed` — smooth camera tracking
- `Camera2D.clampTarget &camera minX minY maxX maxY` — clamp within bounds

## `Camera` (renderer-agnostic)

The core library provides a `Camera` struct with precomputed view and projection matrices, plus helper modules to build them:

```fsharp
type Camera = {
    View: Matrix4x4
    Projection: Matrix4x4
}
```

### `Camera3D` module helpers

> These helpers produce a renderer-agnostic `Camera` struct. Use them with `Draw3D.beginCamera` or store for later.

```fsharp
// Look-at camera
let cam = Camera3D.lookAt
    (Vector3(0f, 10f, 20f))  // position
    Vector3.Zero              // target
    Vector3.Up                // up
    (MathF.PI / 4.0f)        // 45° FOV
    (16f / 9f)                // aspect ratio
    0.1f                      // near plane
    1000f                     // far plane

// Orbit camera
let orbiting = Camera3D.orbit target yaw pitch radius fov aspect near far

// Screen-to-ray picking
let ray = Camera3D.screenPointToRay camera mousePos screenWidth screenHeight
```

### Camera3D rendering config

`Camera3DConfig` controls viewport, clear color, and post-processing per camera. Use `Camera3D.render` to create one, then chain `with*` modifiers:

```fsharp
let mainCamera = Camera3D.lookAt (Vector3(0f, 10f, 20f)) Vector3.Zero Vector3.Up (MathF.PI / 4f) (16f/9f) 0.1f 1000f

let config =
    Camera3D.render mainCamera
    |> Camera3D.withClear Color.SkyBlue
```

Use `Draw3D.beginCameraWith` to apply a config in your view:

```fsharp
buffer
|> Draw3D.beginCameraWith config
|> Draw3D.drawModel playerModel playerTransform
|> Draw3D.endCamera
```

Available modifiers:

| Modifier | Description |
|----------|-------------|
| `Camera3D.withViewport rect` | Set viewport in normalized screen coordinates (0–1) |
| `Camera3D.withClear color` | Clear with this color before rendering |
| `Camera3D.withPostProcess passes` | Use only specific post-process pass indices |
| `Camera3D.withoutPostProcess` | Disable post-processing for this camera |

### Split-screen (3D)

```fsharp
let leftConfig = Camera3D.splitScreenLeft player1Camera Color.SkyBlue
let rightConfig = Camera3D.splitScreenRight player2Camera Color.SkyBlue

buffer
|> Draw3D.beginCameraWith leftConfig
|> // ... player 1 scene ...
|> Draw3D.endCamera
|> Draw3D.beginCameraWith rightConfig
|> // ... player 2 scene ...
|> Draw3D.endCamera
```

### Picture-in-picture overlay (3D)

```fsharp
let minimapRect = Rectangle(0.75f, 0.0f, 0.25f, 0.25f)
let minimapConfig = Camera3D.overlay topDownCamera minimapRect

buffer
|> Draw3D.beginCameraWith mainConfig
|> // ... main scene ...
|> Draw3D.endCamera
|> Draw3D.beginCameraWith minimapConfig
|> // ... minimap scene ...
|> Draw3D.endCamera
```

### Camera2D rendering config

`Camera2DConfig` works the same way for 2D cameras:

```fsharp
let hudCamera = Camera2D.create Vector2.Zero 1.0f viewportSize

let hudConfig =
    Camera2D.render hudCamera
    |> Camera2D.withClear Color.Black

buffer
|> Draw.beginCameraWith 0<RenderLayer> config
|> // ... world content ...
|> Draw.endCamera 1000<RenderLayer>
```

Split-screen helpers: `Camera2D.splitScreenLeft`, `splitScreenRight`, `splitScreenTop`, `splitScreenBottom`, and `Camera2D.overlay`.

### Multi-camera patterns

Combine multiple cameras for split-screen, minimaps, or layered rendering:

```fsharp
// Split-screen 2D
let left = Camera2D.splitScreenLeft cam1 Color.CornflowerBlue
let right = Camera2D.splitScreenRight cam2 Color.DarkGreen

buffer
|> Draw.beginCameraWith 0<RenderLayer> left
|> // ... left viewport content ...
|> Draw.endCamera 100<RenderLayer>
|> Draw.beginCameraWith 200<RenderLayer> right
|> // ... right viewport content ...
|> Draw.endCamera 300<RenderLayer>
```

### Camera2D module helpers

The `Camera2D` module works with raylib's `Camera2D` struct and the 2D renderer:

- `Camera2D.create position zoom viewportSize`
- `Camera2D.screenToWorld camera screenPos`
- `Camera2D.worldToScreen camera worldPos`
- `Camera2D.viewportBounds camera width height`
- `Camera2D.smoothFollow &camera target speed`
- `Camera2D.clampTarget &camera minX minY maxX maxY`

## Camera Movement Examples

### Smooth follow (2D)

```fsharp
let mutable cam = Camera2D.create startPos 1.0f viewportSize

// Each frame:
Camera2D.smoothFollow &cam targetPos 0.1f
Camera2D.clampTarget &cam 0f 0f worldWidth worldHeight
```

See also: [Culling](culling.html) and [Rendering overview](rendering.html).
