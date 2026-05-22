---
title: Programs & Composition
category: Architecture
categoryindex: 1
index: 2
---

# Programs & Composition

A `Program<'Model,'Msg>` is a **declarative configuration pipeline** for your Mibo.Raylib game. It defines how the runtime should orchestrate your state, services, and rendering loop.

Instead of heavy inheritance or global state, you build your program by starting with a core and layering capabilities using high-level combinators.

## Core Definition

Every program starts with `Program.mkProgram init update`.

- **`init`**: Receives a `GameContext` and returns your starting state. This is where you load initial assets and trigger startup commands.
- **`update`**: The heart of your game logic. Receives a message and the current model, returning the next state.

## Typical Composition

Most Mibo.Raylib games follow this "standard" setup in `Program.fs`:

```fsharp
let program =
  Program.mkProgram init update
  // 1. Configure window settings via GameConfig
  |> Program.withConfig (fun cfg ->
      cfg.Width <- 1280
      cfg.Height <- 720
      cfg.Title <- "My Game"
      cfg.TargetFPS <- 60)
  // 2. Add Mibo.Raylib services
  |> Program.withAssets   // No-op currently; caching is automatic in IAssets
  |> Program.withTick Tick // Enqueue a message every frame
  // 3. Define the view
  |> Program.withRenderer (fun () -> Batch3DRenderer.create view3d)
  |> Program.withRenderer (fun () -> Batch2DRenderer.create viewUi)

// Run the game
let game = new RaylibGame<Model, Msg>(program)
game.Run()
```

---

## Amenities & Services

### `withAssets`
Planned. Currently a no-op. Asset loading and caching are handled automatically through `IAssets` (via `ctx.Assets.Texture(...)`, `ctx.Assets.Font(...)`, etc.) without needing explicit opt-in.

### `withInput`
Does not exist yet. Input is handled either via direct polling with `Keyboard.poll` in your `update` function, or through subscriptions (future release).

### `withSubscription`
Connects your Elmish subscriptions to the runtime. The subscription function is re-evaluated every time your model changes, allowing you to dynamically start/stop listeners.

```fsharp
let subscribe (ctx: GameContext) (model: Model) =
    Sub.batch [ ... ]

Program.mkProgram init update
|> Program.withSubscription subscribe
```

See [The Subscription](elmish.html#the-subscription) in the Elmish guide for a detailed breakdown.

---

## Runtime & Performance Knobs

Mibo.Raylib gives you fine-grained control over how the game loop behaves.

### `withTick`
Standard per-frame update. Pass a constructor (e.g., `Tick`) and the runtime will dispatch it every frame with the current `GameTime`. Use this for UI animations, camera smoothing, or simple timers.

### `withFixedStep`
Ideal for physics or simulation stability. Unlike `withTick`, which runs exactly once per frame, `withFixedStep` might run zero, one, or many times per frame to maintain a precise simulation frequency.

```fsharp
|> Program.withFixedStep {
    StepSeconds = 1f / 60f
    MaxStepsPerFrame = 5
    MaxFrameSeconds = ValueSome 0.25f
    Map = PhysicsTick
}
```

### `withDispatchMode`
Controls when messages are processed.
- `DispatchMode.Immediate` (Default): Messages dispatched during `update` are processed immediately.
- `DispatchMode.FrameBounded`: Deferred to the next frame. Use this if you want to strictly prevent "re-entrant" updates within a single frame.

---

## Raylib Integration

### `withRenderer`
Adds an `IRenderer` to the stack. Renderers run in the **order they are added**. It is common to add a 3D renderer first, followed by a 2D UI renderer.

```fsharp
|> Program.withRenderer (fun () -> Batch2DRenderer.create view)
```

### `withComponent`
Does not exist yet. Planned for future release. Will serve as the "escape hatch" for integrating custom raylib subsystems.

### `withComponentRef`
Does not exist yet. Planned for future release. Will provide type-safe access to custom components without globals.

---

## Advanced Configuration

### `withConfig`
Gives you direct access to the `GameConfig` record before the game initializes.

```fsharp
|> Program.withConfig (fun cfg ->
    cfg.Width <- 1280
    cfg.Height <- 720
    cfg.Title <- "My Game"
    cfg.TargetFPS <- 60)
```

> _**TIP**_: **Cumulative Pipeline**: You can call `withConfig` multiple times; each callback is executed in the order it was added, allowing you to layer configuration.

> _**IMPORTANT**_: **Platform Specifics**: This is where you should put logic that varies by platform. For example, your Desktop project might set a fixed window size, while your Mobile project might handle screen orientation or full-screen modes.
