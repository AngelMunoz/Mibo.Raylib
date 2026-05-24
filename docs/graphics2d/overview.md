---
title: 2D Rendering Overview
category: Rendering
categoryindex: 3
index: 11
---

# 2D Rendering

The 2D rendering pipeline is a **deferred command system**: each frame, your view function populates a `RenderBuffer2D` with `IRenderCommand2D` values, and the `Renderer2D<'Model>` sorts them by layer and executes them in order.

## What and Why

A deferred renderer means you describe *what to draw* without worrying about *when to draw it*. The renderer handles:

- **Layer ordering** — Commands are sorted by `int<RenderLayer>` so backgrounds draw before foregrounds.
- **Camera transforms** — `Draw.beginCamera` / `Draw.endCamera` bracket world-space content.
- **Shader modes** — `Draw.beginShader` / `Draw.endShader` enable per-section effects.
- **Post-processing** — Screen-space shader passes applied after the scene renders.
- **GPU batching** — raylib auto-batches standard draw calls; the renderer never interferes.

This is especially useful for:

- **2D lighting** — Light commands and sprites are interleaved in the same buffer and processed together.
- **UI overlays** — Draw UI on a higher layer (and optionally a separate camera) above your game world.
- **Debug visualization** — Toggle debug shapes on/off by adding or removing commands.
- **Portability** — The same view function works with any renderer configuration.

## When to use deferred vs immediate

| Situation | Approach |
|-----------|----------|
| Sprites, text, shapes, tiles | Use `Draw.*` commands (deferred) |
| Custom rlgl meshes, instancing | Use `Draw.drawImmediate` (escape hatch) |
| One-off GPU operations | Prefer deferred; use immediate only when raylib lacks the API |

## How it works

```
Program.mkProgram init update
|> Program.withRenderer (fun () -> Renderer2D.create myView)
```

Each frame, the runtime calls `myView ctx model buffer`. Your view adds commands, the renderer sorts and executes:

1. `buffer.Clear()` — wipe previous frame's commands
2. `myView ctx model buffer` — populate with this frame's draw commands
3. `buffer.Sort()` — sort by layer (ascending)
4. Execute in order, managing camera/shader state transitions

## Command API layers

Two ways to add commands to the buffer:

| Layer | When to use |
|-------|-------------|
| `Draw.*` DSL | Everyday use — pipe-friendly, supports partial application |
| `Command2D.*` factories | When you need to store or reuse commands without a buffer |
| `IRenderCommand2D` directly | Custom rendering logic (see [Custom Commands](custom-commands.html)) |

## Lighting

The 2D lighting system (`Mibo.Elmish.Graphics2D.Lighting`) produces light commands that mix into the render buffer alongside draw commands. Wrap draw calls between light commands:

```fsharp
buffer
|> LightDraw.litSprite lightingCtx spriteState
|> LightDraw.ambientLight lightingCtx color
|> Draw.fillRect (10<RenderLayer>, Color.Red) rect
```

See the PlatformerSample for a complete lighting setup.

## Next steps

- [Buffer & Commands](buffer-and-commands.html) — How to build every type of draw command
- [Custom Commands](custom-commands.html) — Implementing IRenderCommand2D
- [Performance](performance.html) — Writing efficient rendering code
- [Camera](../camera.html) — Cameras and coordinate transforms
