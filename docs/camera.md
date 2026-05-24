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

Both 2D and 3D renderers consume cameras via render commands (`Draw.beginCamera` / `SetCamera3D`).

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

## 3D cameras (`Camera3D`)

Raylib's `Camera3D` is a mutable struct:

```fsharp
type Camera3D = {
    mutable Position: Vector3
    mutable Target: Vector3
    mutable Up: Vector3
    mutable FovY: float32
    mutable Projection: CameraProjection
}
```

Fields:

| Field | Type | Description |
|-------|------|-------------|
| `Position` | `Vector3` | Camera position in world space |
| `Target` | `Vector3` | Point the camera is looking at |
| `Up` | `Vector3` | Up vector (typically `Vector3.UnitY`) |
| `FovY` | `float32` | Vertical field of view in degrees |
| `Projection` | `CameraProjection` | `Perspective` or `Orthographic` |

### Perspective camera

```fsharp
let mutable cam = Camera3D()
cam.Position <- Vector3(10f, 10f, 10f)
cam.Target <- Vector3.Zero
cam.Up <- Vector3.UnitY
cam.FovY <- 45f
cam.Projection <- CameraProjection.Perspective

// In your 3D view:
buffer.Add(0<RenderLayer3D>, SetCamera3D cam)
```

### Orthographic camera

```fsharp
let mutable cam = Camera3D()
cam.Position <- Vector3(10f, 10f, 10f)
cam.Target <- Vector3.Zero
cam.Up <- Vector3.UnitY
cam.FovY <- 20f
cam.Projection <- CameraProjection.Orthographic
```

> The `Camera3D` module in `Mibo.Elmish` provides helpers:
> - `Camera3D.lookAt position target up fov aspect near far` — create a look-at camera
> - `Camera3D.orbit target yaw pitch radius fov aspect near far` — orbiting camera
> - `Camera3D.screenPointToRay camera screenPos width height` — screen-to-ray picking

## Camera Movement Examples

### Orbit camera

```fsharp
let mutable yaw = 0f
let mutable pitch = 0.35f
let mutable distance = 12f

let updateOrbit (cam: byref<Camera3D>) target =
    let dir = Vector3(
        MathF.Cos(yaw) * MathF.Cos(pitch),
        MathF.Sin(pitch),
        MathF.Sin(yaw) * MathF.Cos(pitch)
    )
    cam.Position <- target + dir * distance
    cam.Target <- target
```

### Third-person follow camera

```fsharp
let updateFollow (cam: byref<Camera3D>) (playerPos: Vector3) (playerForward: Vector3) distance height =
    let behind = -playerForward * distance
    cam.Position <- playerPos + behind + Vector3(0f, height, 0f)
    cam.Target <- playerPos
```

### Zoom with scroll wheel

```fsharp
let updateZoom (cam: byref<Camera3D>) (delta: float32) =
    let dir = Vector3.Normalize(cam.Position - cam.Target)
    let currentDist = Vector3.Distance(cam.Position, cam.Target)
    let newDist = Math.Clamp(currentDist - delta * 2f, 1f, 100f)
    cam.Position <- cam.Target + dir * newDist
```

See also: [Culling](culling.html) and [Rendering overview](rendering.html).
