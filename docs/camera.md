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

Both 2D and 3D renderers consume cameras via render commands (`SetCamera2D` / `SetCamera3D`).

## 2D cameras (`Camera2D`)

Raylib's `Camera2D` is a mutable struct:

```fsharp
let mutable cam = Camera2D()
cam.Target <- Vector2(400f, 300f)   // world position to center on
cam.Offset <- Vector2(640f, 360f)   // screen offset (typically center)
cam.Rotation <- 0f                  // rotation in radians
cam.Zoom <- 1.0f                    // zoom factor
```

In the Mibo.Raylib 2D renderer, you use a `Camera2DState` record:

```fsharp
buffer.Add(0<RenderLayer>, SetCamera2D {
    Position = Vector2(400f, 300f)
    Zoom = 1.0f
    Layer = 0<RenderLayer>
})
```

The renderer constructs the raylib `Camera2D` internally, centering the view on the given position using the window dimensions.

> **Planned:**
> - `Camera2D.create` helper function
> - `Camera2D.screenToWorld` / `worldToScreen`
> - `Camera2D.viewportBounds` for culling

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

> **Planned:**
> - `Camera3D.lookAt`, `Camera3D.orbit`, `Camera3D.create` helper functions
> - `Mibo.Rendering.Graphics3D.Camera` module with rich builder pattern and matrix caching
> - Screen-to-ray picking helpers
> - Bounding frustum computation for culling

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
