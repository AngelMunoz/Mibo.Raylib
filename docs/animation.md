---
title: Animation
category: Amenities
categoryindex: 5
index: 22
---

# Animation (2D Sprite Animation)

> **⚠️ PLANNED**
>
> The `Mibo.Animation` module has not been ported to Mibo.Raylib yet. This page documents the planned API, which will closely follow the original Mibo animation system using raylib texture types.

Mibo provides a format-agnostic 2D animation system in `Mibo.Animation`. It integrates directly with the existing `Graphics2D` rendering primitives.

## Core Types

| Type | Purpose |
| ---- | ------- |
| `Animation` | A struct holding frame rectangles, duration, and loop flag |
| `GridAnimationDef` | Definition for animations in grid-based spritesheets |
| `SpriteSheet` | Texture + named animations with O(1) index-based access |
| `AnimatedSprite` | Runtime state (current frame, time, visual properties) |

## Quick Start

```fsharp
open Mibo.Animation

// 1. Create a SpriteSheet from a uniform grid
let sheet = SpriteSheet.fromGrid texture 32 32 8 [|
  { Name = "idle"; Row = 0; StartCol = 0; FrameCount = 1; Fps = 1.0f; Loop = false }
  { Name = "walk"; Row = 1; StartCol = 0; FrameCount = 4; Fps = 8.0f; Loop = true }
|]

// 2. Create an AnimatedSprite
let sprite = AnimatedSprite.create sheet "idle"

// 3. Update each frame (in your animation system)
let updatedSprite = AnimatedSprite.update deltaTime sprite

// 4. Draw (in your view)
sprite |> AnimatedSprite.draw position layer buffer
```

## SpriteSheet Factory Functions

### `SpriteSheet.fromGrid` – Uniform Grid Layouts

```fsharp
let sheet = SpriteSheet.fromGrid texture 48 48 4 [|
  { Name = "idle";   Row = 0; StartCol = 0; FrameCount = 1; Fps = 1.0f;  Loop = false }
  { Name = "walk";   Row = 1; StartCol = 0; FrameCount = 4; Fps = 8.0f;  Loop = true }
  { Name = "attack"; Row = 2; StartCol = 0; FrameCount = 6; Fps = 12.0f; Loop = false }
|]
```

The `GridAnimationDef` struct:

```fsharp
[<Struct>]
type GridAnimationDef = {
  Name: string
  Row: int
  StartCol: int
  FrameCount: int
  Fps: float32
  Loop: bool
}
```

### `SpriteSheet.single` – Explicit Frame Rectangles

```fsharp
let frames = [|
  Rectangle(0, 0, 64, 64)
  Rectangle(64, 0, 64, 64)
  Rectangle(128, 0, 64, 64)
|]
let sheet = SpriteSheet.single texture frames 10.0f true
```

### `SpriteSheet.fromFrames` – Full Control

```fsharp
let idleAnim: Animation = {
  Frames = [| Rectangle(0, 0, 48, 48) |]
  FrameDuration = 1.0f
  Loop = false
}

let walkAnim: Animation = {
  Frames = [| for i in 0..3 -> Rectangle(i * 48, 48, 48, 48) |]
  FrameDuration = 1.0f / 8.0f
  Loop = true
}

let sheet = SpriteSheet.fromFrames texture (Vector2(24.0f, 24.0f)) [|
  "idle", idleAnim
  "walk", walkAnim
|]
```

### `SpriteSheet.static'` – Single Static Frame

```fsharp
let sheet = SpriteSheet.static' texture (Rectangle(0, 0, 32, 32))
let sprite = AnimatedSprite.create sheet "default"
```

### Animation Index Queries

```fsharp
let walkIdx =
  match SpriteSheet.tryGetAnimationIndex "walk" sheet with
  | ValueSome idx -> idx
  | ValueNone -> 0

let sprite = oldSprite |> AnimatedSprite.playByIndex walkIdx
```

## AnimatedSprite API

### Creation and Animation Control

```fsharp
let sprite = AnimatedSprite.create sheet "idle"
let colored = AnimatedSprite.createWith sheet "idle" Color.Red 1.5f
let walkingSprite = sprite |> AnimatedSprite.play "walk"
let sprite = sprite |> AnimatedSprite.playIfNot "walk"
let sprite = sprite |> AnimatedSprite.restart
let isWalking = sprite |> AnimatedSprite.isPlaying "walk"
```

### Update

```fsharp
let updated = AnimatedSprite.update deltaTime sprite
```

### Visual Properties

```fsharp
sprite
|> AnimatedSprite.withScale 2.0f
|> AnimatedSprite.withColor Color.Red
|> AnimatedSprite.withRotation (MathF.PI / 4.0f)
|> AnimatedSprite.flipX true
|> AnimatedSprite.facingLeft
```

### Drawing

```fsharp
sprite |> AnimatedSprite.draw position layer buffer
sprite |> AnimatedSprite.drawWithDepth position 0.5f layer buffer
sprite |> AnimatedSprite.drawRect destRect layer buffer
```

### Manual Frame Access

```fsharp
let sourceRect = AnimatedSprite.currentSource sprite
let texture = sprite.Sheet.Texture

buffer.Add(layer, DrawSprite {
    Texture = texture
    Dest = Rectangle(position.X, position.Y, float32 sourceRect.Width, float32 sourceRect.Height)
    Source = sourceRect
    Origin = Vector2.Zero
    Rotation = 0f
    Color = Color.White
    Layer = layer
})
```

## Animation Type

```fsharp
[<Struct>]
type Animation = {
  Frames: Rectangle[]
  FrameDuration: float32
  Loop: bool
}
```

### Helpers

```fsharp
let totalTime = Animation.duration anim
let spriteTime = AnimatedSprite.duration sprite
let finished = AnimatedSprite.isFinished sprite
```

## Performance Tips

1. **Resolve animation names once**: Use `AnimationIndices` + `playByIndex` to avoid string allocations in update loops
2. **Share SpriteSheets**: create sheets once at init, reuse for all instances

```fsharp
// At init time
let walkIndex = sheet.AnimationIndices["walk"]

// In update (zero allocations)
let sprite = oldSprite |> AnimatedSprite.playByIndex walkIndex
```

## Texture Atlases & Sprite Management

Mibo is format-agnostic: a `SpriteSheet` is simply a **Texture** plus a set of **Source Rectangles**.

When ported, textures will be `Raylib_cs.Texture2D` and rectangles will be `Raylib_cs.Rectangle`.

```fsharp
// Example: pseudo-code for a custom loader
let loadHero (ctx: GameContext) =
    let tex = Raylib.LoadTexture "hero_atlas"
    let frames = MyJsonParser.parse "hero_metadata.json"
    SpriteSheet.fromFrames tex (Vector2(32.f, 32.f)) frames
```

## See Also

- [Rendering 2D](rendering2d.html)
- [Rendering overview](rendering.html)
