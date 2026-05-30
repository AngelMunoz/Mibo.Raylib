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

## Deep Dive: ThreeDSample's 9-system pipeline

The `ThreeDSample` game runs 9 systems every frame. Here is the actual pipeline from `Systems.fs:370`:

```fsharp
System.start model
|> System.pipeMutable (inputSystem dt)
|> System.pipeMutable (physicsSystem dt)
|> System.pipeMutable (chunkSystem dt)
|> System.pipeMutable (particleSystem dt)
|> System.pipeMutable (minimapSystem dt)
|> System.pipeMutable (dayNightSystem dt)
|> System.pipeMutable (lightingSystem dt)
|> System.pipeMutable (mushroomLightSystem dt)
|> System.pipeMutable (diagnosticsSystem dt)
|> System.finish id
```

### System responsibilities

| System | What it does | Emits commands? |
|--------|-------------|-----------------|
| `inputSystem` | Reads held actions, updates camera yaw/pitch | No |
| `physicsSystem` | Gravity, movement, collision, camera follow | No |
| `chunkSystem` | Loads/unloads chunks around the player | Yes (`ChunkCreated`) |
| `particleSystem` | Physics + fade-and-compact for confetti | No |
| `minimapSystem` | Regenerates minimap image periodically | Yes (`MinimapReady`) |
| `dayNightSystem` | Advances time-of-day clock | No |
| `lightingSystem` | Pre-computes sky, ambient, and sun colors | No |
| `mushroomLightSystem` | Collects nearby glowing mushrooms | Yes (`MushroomLightsReady`) |
| `diagnosticsSystem` | Copies stats into diagnostics model | No |

### Why `pipeMutable` and not `pipe`?

`pipeMutable` passes the model by reference. Systems that mutate fields directly (like updating `model.CameraYaw`) work without allocating new records. This matters when you have many systems running every frame at 60+ FPS.

Use `pipe` when you want immutable updates (functional style). Use `pipeMutable` when your model is a class with mutable members.

### Ordering matters

The pipeline runs top-to-bottom. In ThreeDSample the order is:

1. **Input first** — so physics sees fresh input.
2. **Physics second** — so chunks and particles see the new player position.
3. **Chunks third** — so the minimap sees loaded chunks.
4. **Lighting last** — so it reads the final time-of-day.

> _**TIP**_: Put systems that produce data before systems that consume it. Input before physics. Physics before rendering.

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

See also: [System Pipeline API](system.html), [Scaling Mibo.Raylib](scaling.html).
