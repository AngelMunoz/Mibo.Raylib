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

The 2D renderer consumes cameras via `Draw.beginCamera`. The `Camera3D` module provides helpers that produce a renderer-agnostic `Camera` struct (view + projection matrices) for use with the 3D pipeline when available.

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

> The 3D rendering pipeline is pending — these helpers are ready now and produce a `Camera` you can use with a custom renderer or store for later.

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
