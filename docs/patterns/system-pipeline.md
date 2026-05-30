---
title: System Pipeline Pattern
category: Patterns
categoryindex: 6
index: 62
---

# System Pipeline Pattern

## What and Why

The system pipeline organizes your game's per-frame update into a chain of independent functions. Each function — a _system_ — receives the model, does its work, and returns an updated model plus any commands.

Instead of one massive `update` function, you get small, testable units composed in order.

## When to use

- Your `update` function has grown past 100 lines.
- You have multiple update phases: input, physics, particles, AI, lighting, etc.
- You want predictable ordering between subsystems.
- You're building anything with continuous simulation (platformers, RPGs, RTS games).

## Quick Start

```fsharp
let update (msg: Msg) (model: Model) : struct (Model * Cmd<Msg>) =
  match msg with
  | Tick gt ->
    let dt = float32 gt.ElapsedGameTime.TotalSeconds

    System.start model
    |> System.pipeMutable (inputSystem dt)
    |> System.pipeMutable (physicsSystem dt)
    |> System.pipeMutable (particleSystem dt)
    |> System.finish id
  | _ -> struct (model, Cmd.none)
```

Each system is a standalone function with the same signature:

```fsharp
let inputSystem (dt: float32) (model: Model) : struct (Model * Cmd<Msg>) =
  // read input, mutate model
  struct (model, Cmd.none)
```

## Deep Dive

### System responsibilities

A typical game might run these systems each frame:

| System | What it does | Emits commands? |
|--------|-------------|-----------------|
| `inputSystem` | Reads held actions, updates camera or character state | No |
| `physicsSystem` | Gravity, movement, collision detection | No |
| `chunkSystem` | Loads/unloads world chunks around the player | Yes (`ChunkCreated`) |
| `particleSystem` | Physics integration + fade-and-compact for particles | No |
| `aiSystem` | Runs NPC behavior, pathfinding | No |
| `dayNightSystem` | Advances time-of-day clock | No |
| `lightingSystem` | Pre-computes sky, ambient, and directional light colors | No |
| `minimapSystem` | Regenerates minimap image periodically | Yes (`MinimapReady`) |
| `diagnosticsSystem` | Copies stats into diagnostics model for overlay | No |

You don't need all of these — pick the systems your game requires and compose them.

### Why `pipeMutable` and not `pipe`?

`pipeMutable` passes the model by reference. Systems that mutate fields directly (like updating `model.CameraYaw`) work without allocating new records. This matters when you have many systems running every frame at 60+ FPS.

Use `pipe` when you want immutable updates (functional style). Use `pipeMutable` when your model is a class with mutable members.

### Ordering matters

The pipeline runs top-to-bottom. The general rule:

1. **Input first** — so downstream systems see fresh input.
2. **Simulation second** — physics, AI, particles, world streaming.
3. **Derived state third** — lighting, minimap, diagnostics.
4. **Rendering last** — reads the final model state.

> _**TIP**_: Put systems that produce data before systems that consume it. Input before physics. Physics before rendering.

### Adding or removing systems

To add a new system, write a function with the standard signature and insert it at the right position in the pipeline:

```fsharp
System.start model
|> System.pipeMutable (inputSystem dt)
|> System.pipeMutable (weatherSystem dt)   // new
|> System.pipeMutable (physicsSystem dt)
|> System.pipeMutable (particleSystem dt)
|> System.finish id
```

To disable a system, comment it out. No other code changes needed.

### Snapshot boundaries

For large games, you can add a type-enforced snapshot between mutable and readonly phases:

```fsharp
System.start model
|> System.pipeMutable (inputSystem dt)
|> System.pipeMutable (physicsSystem dt)
|> System.snapshot Model.toReadonly
|> System.pipe (aiSystem dt)
|> System.finish Model.fromReadonly
```

After the snapshot, you can't accidentally call a mutable phase. The compiler enforces it.

### For a complete example

See `samples/ThreeDSample/Systems.fs` for a full implementation with nine systems.

See also: [System Pipeline API](system.html), [Scaling Mibo.Raylib](scaling.html).
