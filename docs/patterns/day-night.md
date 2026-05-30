---
title: Day/Night Cycle
category: Patterns
categoryindex: 6
index: 66
---

# Day/Night Cycle

## What and Why

A day/night cycle changes sky color, ambient light, and directional light over time. The naive approach computes everything in the view function — but that couples time logic to rendering and makes it hard to test.

The pattern: run time as a system, pre-compute all lighting values on the model, and let the view function read them without calculation.

## When to use

- Your game has a time-of-day that affects visuals.
- You want smooth transitions between lighting states (dawn, day, dusk, night).
- You need time-based logic elsewhere (enemy behavior, shop hours, etc.).
- You want to test lighting values without running the renderer.

## Quick Start

### Two systems

```fsharp
// System 1: advance the clock
let dayNightSystem (dt: float32) (model: GameModel) : struct (GameModel * Cmd<Msg>) =
  let newTime = (model.TimeOfDay + dt * (24.0f / model.DayDuration)) % 24.0f
  model.TimeOfDay <- newTime
  struct (model, Cmd.none)

// System 2: compute lighting from time
let lightingSystem (dt: float32) (model: GameModel) : struct (GameModel * Cmd<Msg>) =
  let time = model.TimeOfDay
  let l = model.Lighting
  l.SkyColor <- getSkyColor time
  l.AmbientColor <- getAmbientColor time
  l.AmbientIntensity <- getAmbientIntensity time
  l.LightDirection <- getLightDirection time
  l.LightColor <- getLightColor time
  l.LightIntensity <- getLightIntensity time
  struct (model, Cmd.none)
```

### In the pipeline

```fsharp
System.start model
|> System.pipeMutable (inputSystem dt)
|> System.pipeMutable (physicsSystem dt)
|> System.pipeMutable (dayNightSystem dt)    // clock first
|> System.pipeMutable (lightingSystem dt)    // then compute from clock
|> System.finish id
```

## Deep Dive

### Time accumulation

The clock wraps around 24 hours:

```fsharp
let newTime = (model.TimeOfDay + dt * (24.0f / model.DayDuration)) % 24.0f
```

`DayDuration` is the real-time seconds for one full cycle. A 60-second duration means 1 game day = 1 real minute. Adjust for your game's pacing.

### Pre-computed state on the model

A `LightingModel` holds all values the renderer needs:

```fsharp
type LightingModel() =
  member val SkyColor = Color.Black with get, set
  member val AmbientColor = Color.Black with get, set
  member val AmbientIntensity = 0.0f with get, set
  member val LightDirection = Vector3.Zero with get, set
  member val LightColor = Color.White with get, set
  member val LightIntensity = 0.0f with get, set
```

The view reads these directly — no computation:

```fsharp
let view (ctx: GameContext) (model: GameModel) (buffer: RenderBuffer3D) =
  let l = model.Lighting
  buffer
  |> Draw3D.beginCameraWith (Camera3D.render camera |> Camera3D.withClear l.SkyColor)
  |> Draw3D.setAmbientLight { Color = l.AmbientColor; Intensity = l.AmbientIntensity }
  |> Draw3D.addDirectionalLight {
      Direction = l.LightDirection
      Color = l.LightColor
      Intensity = l.LightIntensity
      CastsShadows = true }
  // ...
```

### Smooth transitions via interpolation

Use piecewise linear interpolation between time checkpoints:

```fsharp
let getSkyColor time : Color =
  if time < 6.0f then
    Color(10uy, 10uy, 30uy)                    // night
  elif time < 8.0f then
    lerpColor                                   // dawn transition
      (Color(10uy, 10uy, 30uy))
      (Color(100uy, 149uy, 237uy))
      ((time - 6.0f) / 2.0f)
  elif time < 16.0f then
    Color(100uy, 149uy, 237uy)                  // day
  elif time < 18.0f then
    lerpColor                                   // dusk transition
      (Color(100uy, 149uy, 237uy))
      (Color(50uy, 50uy, 100uy))
      ((time - 16.0f) / 2.0f)
  else
    Color(10uy, 10uy, 30uy)                     // night
```

The same pattern applies to ambient color, light color, and intensity. Define your own color palette and time breakpoints to match your game's aesthetic.

### The sun arc

A single directional light simulates both sun and moon on a 190-degree arc:

```fsharp
let getLightDirection (time: float32) : Vector3 =
  let arcRadius = 100.0f
  if time >= 6.0f && time <= 18.0f then
    celestialArc ((time - 6.0f) / 12.0f) arcRadius  // sun
  else
    let t = if time > 18.0f then (time - 18.0f) / 12.0f else (time + 6.0f) / 12.0f
    celestialArc t arcRadius                          // moon
```

The 10-degree overlap at each end creates a fade where both bodies share the sky — one fading in, the other fading out.

### Intensity fade at arc edges

```fsharp
let getLightIntensity (time: float32) : float32 =
  // Sun: full intensity in middle, fades at edges
  // Moon: max 30% of sun intensity
```

This prevents hard light transitions. The sun fades out gradually as it sets, and the moon fades in.

> _**TIP**_: Keep the interpolation logic in a separate module. It's pure functions with no dependencies on the game model — easy to test with unit tests.

> _**NOTE**_: The lighting system runs after `dayNightSystem` in the pipeline so it reads the updated time-of-day. Order matters.

### For a complete example

See `samples/ThreeDSample/DayNight.fs` and `samples/ThreeDSample/Lighting.fs` for full implementations.

See also: [System Pipeline](system-pipeline.html), [3D Rendering Overview](graphics3d/overview.html), [3D Lighting](graphics3d/overview.html#lighting).
