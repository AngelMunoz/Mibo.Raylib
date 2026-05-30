---
title: 3D Buffer & Commands
category: Rendering
categoryindex: 3
index: 21
---

# 3D Buffer & Commands

Every frame, your view function receives a `RenderBuffer3D` and populates it with commands. This page covers all command types and how to chain them.

## The buffer lifecycle

```fsharp
val view : GameContext -> 'Model -> RenderBuffer3D -> unit
```

The buffer is **pre-cleared** by the renderer each frame. Just add commands:

```fsharp
let view (ctx: GameContext) (model: Model) (buffer: RenderBuffer3D) =
    buffer
    |> Draw3D.beginCamera camera
    |> Draw3D.drawModel model.PlayerModel model.PlayerTransform
    |> Draw3D.endCamera
    |> Draw3D.drop
```

`Draw3D.drop` at the end silences the unused-value warning. It does nothing.

## Pipeline pattern

Every 3D view follows the same structure:

```
buffer
|> Draw3D.beginCamera camera    // start camera transform
|> Draw3D.setAmbientLight ...   // lighting setup
|> Draw3D.addDirectionalLight ...
|> Draw3D.drawMesh ...          // geometry
|> Draw3D.drawModel ...
|> Draw3D.endCamera             // end camera transform
|> Draw3D.drop                  // terminal
```

Geometry drawn outside `beginCamera` / `endCamera` renders in screen space (rarely desired).

## Two ways to build commands

| Layer | When to use |
|-------|-------------|
| `Draw3D.*` DSL | Everyday use — pipe-friendly, buffer-last for chaining |
| `Command3D.*` factories | When you need to store or reuse commands without a buffer |

```fsharp
// DSL — pipe-friendly
buffer
|> Draw3D.drawMesh mesh transform material
|> Draw3D.drawModel model transform

// Factory — store for later
let cmd = Command3D.drawMesh mesh transform material
buffer.Add(cmd)
```

## Command reference

### Geometry

| Function | Description |
|----------|-------------|
| `Draw3D.drawMesh mesh transform material` | Draw a single mesh with a material |
| `Draw3D.drawModel model transform` | Draw a raylib Model (auto-converts materials) |
| `Draw3D.drawBillboard texture position size color` | Camera-facing textured quad |
| `Draw3D.drawBillboardBatch textures positions sizes colors count` | Batch of billboards, one draw call |
| `Draw3D.drawLine3D start finish color` | 3D line between two points |
| `Draw3D.drawSkinnedMesh mesh transform material bones` | Skinned mesh with bone matrices |
| `Draw3D.drawMeshInstanced mesh transforms material count` | Many copies of same mesh, one draw call |

### Camera

| Function | Description |
|----------|-------------|
| `Draw3D.beginCamera camera` | Start 3D camera transform |
| `Draw3D.beginCameraWith config` | Start camera with explicit viewport/clear/post-process |
| `Draw3D.endCamera` | End camera transform |

### Lighting

| Function | Description |
|----------|-------------|
| `Draw3D.setAmbientLight light` | Set scene ambient light |
| `Draw3D.addDirectionalLight light` | Add a directional light |
| `Draw3D.addPointLight light` | Add a point light |
| `Draw3D.addSpotLight light` | Add a spot light |

### Shadows

| Function | Description |
|----------|-------------|
| `Draw3D.setShadowOrigin origin` | Set shadow map origin for this frame |
| `Draw3D.enableShadows` | Enable shadow casting for subsequent geometry |
| `Draw3D.disableShadows` | Disable shadow casting for subsequent geometry |

### Escape hatches

| Function | Description |
|----------|-------------|
| `Draw3D.drawImmediate action` | Flush batch, run raw raylib/rlgl calls, restore state |
| `Draw3D.drop` | Terminal — discard buffer reference (no-op) |

## Camera with explicit config

Use `beginCameraWith` when you need viewport control, clear color, or post-process pass selection:

```fsharp
buffer
|> Draw3D.beginCameraWith(
    Camera3D.render camera |> Camera3D.withClear Color.SkyBlue
)
|> Draw3D.drawModel model transform
|> Draw3D.endCamera
|> Draw3D.drop
```

`Camera3DConfig` fields:

| Field | Type | Description |
|-------|------|-------------|
| `Camera` | `Camera3D` | The raylib camera |
| `Viewport` | `Rectangle voption` | Normalized screen coords (0-1), `ValueNone` = fullscreen |
| `ClearColor` | `Color voption` | `ValueSome color` to clear, `ValueNone` to skip |
| `PostProcessPasses` | `int[] voption` | Which post-process passes to apply |

## Command3D DU

All commands are stored as a `[<Struct>]` discriminated union for zero-allocation:

```fsharp
[<RequireQualifiedAccess; Struct>]
type Command3D =
    | DrawMesh of mesh: Mesh * transform: Matrix4x4 * material: Material3D
    | DrawModel of model: Model * transform: Matrix4x4
    | DrawBillboard of texture: Texture2D * position: Vector3 * size: Vector2 * color: Color
    | DrawLine3D of start: Vector3 * finish: Vector3 * color: Color
    | DrawSkinnedMesh of mesh: Mesh * transform: Matrix4x4 * material: Material3D * bones: Matrix4x4[]
    | DrawMeshInstanced of mesh: Mesh * transforms: Matrix4x4[] * material: Material3D * instanceCount: int
    | DrawBillboardBatch of textures: Texture2D[] * positions: Vector3[] * sizes: Vector2[] * colors: Color[] * count: int
    | BeginCamera of camera: Camera3D
    | BeginCameraConfig of config: Camera3DConfig
    | EndCamera
    | SetShadowOrigin of origin: Vector3
    | SetAmbientLight of aLight: AmbientLight3D
    | AddDirectionalLight of AddDlight: DirectionalLight3D
    | AddPointLight of AddPlight: PointLight3D
    | AddSpotLight of AddSlight: SpotLight3D
    | EnableShadows
    | DisableShadows
    | DrawImmediate of action: (unit -> unit)
```

## See also

- [Overview](overview.html) — Architecture and pipeline setup
- [Lighting](lighting.html) — Light types and configuration
- [Materials](materials.html) — PBR material system
- [Instancing](instancing.html) — GPU instanced rendering
