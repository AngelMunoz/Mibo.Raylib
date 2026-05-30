---
title: 3D Particle System
category: Patterns
categoryindex: 6
index: 64
---

# 3D Particle System

## What and Why

Particles add juice — confetti on jumps, dust on landings, sparks on hits. You need hundreds of them without killing frame rate or triggering garbage collection.

This pattern uses pre-allocated parallel arrays (positions, velocities, sizes, colors) and a fade-and-compact loop that removes dead particles in-place. No lists, no allocations per particle, no GC pressure.

## When to use

- You need short-lived visual effects (explosions, confetti, dust, sparks).
- You want hundreds of particles at 60+ FPS.
- You're already using the 3D rendering pipeline with billboards.
- You care about GC pauses (console games, VR, competitive).

## Quick Start

### Data model

```fsharp
type ParticleModel() =
  member val Positions = Array.zeroCreate<Vector3> 512 with get, set
  member val Velocities = Array.zeroCreate<Vector3> 512 with get, set
  member val Sizes = Array.zeroCreate<Vector2> 512 with get, set
  member val Colors = Array.zeroCreate<Color> 512 with get, set
  member val Count = 0 with get, set
  member val Texture = Unchecked.defaultof<Texture2D> with get, set
```

All arrays are pre-allocated to a max capacity. `Count` tracks how many slots are alive.

### Spawn particles by writing directly into arrays

```fsharp
let spawnConfetti (model: GameModel) =
  let rng = System.Random.Shared
  let p = model.Particles
  let mutable pc = p.Count

  for _ in 0..100 do
    if pc < p.Positions.Length then
      p.Positions[pc] <- model.PlayerPosition + offset
      p.Sizes[pc] <- Vector2(0.05f, 0.05f)
      p.Colors[pc] <- confettiColors[rng.Next(confettiColors.Length)]
      p.Velocities[pc] <- Vector3(cos angle * speed, upSpeed, sin angle * speed)
      pc <- pc + 1

  p.Count <- pc
```

No `ResizeArray.add`, no list cons, no allocation. Just array index writes.

### Update: physics + fade-and-compact

```fsharp
let particleSystem (dt: float32) (model: GameModel) : struct (GameModel * Cmd<Msg>) =
  let p = model.Particles
  let positions = p.Positions
  let velocities = p.Velocities
  let colors = p.Colors
  let mutable count = p.Count

  // Physics: apply gravity, integrate position
  for i = 0 to count - 1 do
    let vel = velocities[i]
    let newVel = Vector3(vel.X, vel.Y + gravity * dt * 0.6f, vel.Z)
    velocities[i] <- newVel
    positions[i] <- positions[i] + newVel * dt

  // Fade and compact: reduce alpha, remove dead particles
  let fadeAmount = 130.0f * dt
  let mutable writeIdx = 0

  for readIdx = 0 to count - 1 do
    let c = colors[readIdx]
    let newAlpha = MathF.Max(0.0f, float32 c.A - fadeAmount)

    if newAlpha > 0.0f then
      positions[writeIdx] <- positions[readIdx]
      velocities[writeIdx] <- velocities[readIdx]
      colors[writeIdx] <- Color(c.R, c.G, c.B, byte newAlpha)
      writeIdx <- writeIdx + 1

  p.Count <- writeIdx
  struct (model, Cmd.none)
```

## Deep Dive

### Why parallel arrays instead of a Particle array?

A `Particle[]` with fields `{ Position; Velocity; Size; Color }` would work, but parallel arrays have better cache locality for the physics pass — you iterate positions and velocities without touching colors or sizes. The compact pass needs all fields, but it runs less frequently (only when particles die).

For < 1000 particles, the difference is negligible. Parallel arrays are the convention in this codebase.

### Fade-and-compact explained

Instead of marking particles as dead and skipping them, the compact pass uses a read/write pointer:

```
readIdx:  0  1  2  3  4  5
alpha:    50  0  30  0  10  0
writeIdx starts at 0

readIdx=0: alpha=50 > 0 → copy to writeIdx=0, writeIdx=1
readIdx=1: alpha=0  → skip
readIdx=2: alpha=30 > 0 → copy to writeIdx=1, writeIdx=2
readIdx=3: alpha=0  → skip
readIdx=4: alpha=10 > 0 → copy to writeIdx=2, writeIdx=3
readIdx=5: alpha=0  → skip

Final count = 3
```

This is O(n) with no allocations. Dead particles vanish; live ones shift to the front.

### Rendering

Each particle renders as a billboard — a camera-facing quad:

```fsharp
let p = model.Particles

for i = 0 to p.Count - 1 do
  Draw3D.drawBillboard p.Texture p.Positions[i] p.Sizes[i] p.Colors[i] buffer
  |> Draw3D.drop
```

> _**TIP**_: Use a 1x1 white texture for solid-color particles. Tint with the `color` parameter. For textured particles (smoke, fire), load a sprite atlas.

### Performance characteristics

| Aspect | Cost |
|--------|------|
| Spawn | O(n) array writes, no allocation |
| Update | O(n) physics + O(n) compact |
| Render | O(n) draw calls (use `drawBillboardBatch` for larger counts) |
| Memory | Fixed: 4 arrays × capacity × sizeof(element) |
| GC | Zero — no allocations after init |

> _**NOTE**_: For thousands of particles, switch from per-particle `drawBillboard` to `drawBillboardBatch` which sends all particles in a single draw call.

### Triggering particles

In the sample, confetti spawns on jump:

```fsharp
if model.IsGrounded && model.Actions.Started.Contains(GameAction.Jump) then
  spawnConfetti model
  // apply jump velocity...
```

You can spawn from any system. The particle system doesn't care where particles come from.

See also: [3D Rendering Overview](graphics3d/overview.html), [System Pipeline](system-pipeline.html).
