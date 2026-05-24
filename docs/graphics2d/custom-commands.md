---
title: Custom Commands & Escape Hatches
category: Rendering
categoryindex: 3
index: 16
---

# Custom Commands & Escape Hatches

When the built-in `Draw.*` commands don't cover your use case, you have two options: implement `IRenderCommand2D` directly, or use the `DrawImmediate` escape hatch.

## IRenderCommand2D

The `IRenderCommand2D` interface is what every command implements. You can create your own by implementing two members:

```fsharp
type IRenderCommand2D =
  abstract Layer: int<RenderLayer>
  abstract Render: context: IRenderContext -> unit
```

### Object expression (recommended for one-off commands)

```fsharp
let fadeScreen (color: Color, alpha: float32, layer: int<RenderLayer>) =
  { new IRenderCommand2D with
      member _.Layer = layer
      member _.Render _ =
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
          Color(int color.R, int color.G, int color.B, int(alpha * 255f)))
  }

// Usage:
buffer.Add(fadeScreen(Color.Black, 0.5f, 50<RenderLayer>))
```

### Struct command (performance-sensitive, many instances)

For commands that are created in bulk (e.g., a tile renderer), prefer a struct to avoid heap allocations:

```fsharp
[<Struct>]
type TileCommand(texture: Texture2D, source: Rectangle, dest: Rectangle, layer: int<RenderLayer>) =
  interface IRenderCommand2D with
    member _.Layer = layer
    member _.Render _ =
      Raylib.DrawTexturePro(texture, source, dest, Vector2.Zero, 0f, Color.White)
```

### Custom lighting pass

You can interleave with the lighting system by accessing the light context via `IRenderContext.GameContext`:

```fsharp
let customLightPass (lightCtx: LightContext2D) (layer: int<RenderLayer>) =
  { new IRenderCommand2D with
      member _.Layer = layer
      member _.Render ctx =
        // Access game context for services etc.
        let gc = ctx.GameContext
        // ... custom light logic
  }
```

## The render context

Your command's `Render` method receives an `IRenderContext` that provides:

| Member | Purpose |
|--------|---------|
| `GameContext` | Access to services, window size, input |
| `CurrentCamera` | Active camera, if any |
| `BeginCamera(c)` | Start camera transform (flushes batch, ends previous) |
| `EndCamera()` | End camera transform (flushes batch) |
| `BeginShader(s)` | Start shader mode (flushes batch, ends previous) |
| `EndShader()` | End shader mode (flushes batch) |
| `DrawImmediate(action)` | Run raw rlgl code (see below) |

The context tracks camera and shader state to prevent nested `BeginMode2D`/`EndMode2D` conflicts. You generally don't need to call these from commands — use `Draw.beginCamera`/`Draw.endCamera` at the buffer level instead.

## DrawImmediate (escape hatch)

`DrawImmediate` flushes raylib's internal batch, temporarily exits camera and shader modes, runs your action, then restores the previous state. Use this for:

- Direct rlgl calls (`Rlgl.Begin`, `Rlgl.Vertex2f`, etc.)
- Custom mesh rendering
- Any GPU operation that must not be batched with standard draw calls

```fsharp
buffer
|> Draw.drawImmediate 0<RenderLayer> (fun () ->
    Rlgl.Begin(DrawMode.Quads)
    Rlgl.Color4f(1f, 0f, 0f, 1f)
    Rlgl.Vertex2f(0f, 0f)
    Rlgl.Vertex2f(100f, 0f)
    Rlgl.Vertex2f(100f, 100f)
    Rlgl.Vertex2f(0f, 100f)
    Rlgl.End()
  )
```

**Cost**: Each `DrawImmediate` call forces a batch flush before and after. Use sparingly — prefer built-in `Draw.*` commands whenever possible.

## When to use which

| Scenario | Approach |
|----------|----------|
| Standard sprites, text, shapes | `Draw.*` (zero effort) |
| Many instances of the same primitive | Struct implementing `IRenderCommand2D` |
| One-off special effect | Object expression |
| Direct rlgl / instancing / compute | `DrawImmediate` |
| Interop with third-party renderers | `DrawImmediate` or custom struct command |
