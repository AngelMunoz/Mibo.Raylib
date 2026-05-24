# Mibo.Raylib

Mibo.Raylib is a port of the [Mibo](https://github.com/anomalyco/Mibo) micro-framework from **MonoGame** to **raylib-cs**, designed to allow **F#** developers to write games using familiar Elmish patterns for all kinds of game genres and sizes — no Content Pipeline, no C# tooling, no `.mgcb` files.

Mibo aims to solve 90% of use cases for enabling developers to focus on game logic rather than boilerplate code, providing guidelines and architecture for structuring game code, handling input, rendering, asset management, and time management among others.

## What's in the box?

- **Elmish runtime** (MVU loop) with `Cmd`, `Sub`, optional **fixed timestep**, and **frame-bounded dispatch**
- **Input** — raw input (`Keyboard`, `Mouse`) + semantic mapping via `InputMap` / `ActionState`
- **Assets** — texture, font, sound, and model loading with dictionary-based caches (no Content Pipeline)
- **Rendering** — `IRenderer<'Model>` and `RenderBuffer<'Key, 'Cmd>`:
  - 2D batch renderer with layers and multi-camera support
  - 3D batch renderer with opaque/transparent passes and custom shader switching
  - Escape hatches (`DrawCustom`) for custom GPU work
- **Camera** helpers (`Camera2D`, `Camera3D`) with screen-to-world, orbit, and ray casting
- **Culling** utilities for both 2D (quadtree) and 3D (frustum)
- **Layout** — 2D procedural grid layout (`CellGrid2D`) with platformer, top-down, and geometric primitives
- **Layout3D** — 3D voxel-style grid layout (`CellGrid3D`) with terrain, interior rooms, corridors, stairs, and procedural generation
- **Animation** — sprite sheet slicing, `AnimatedSprite` state machines, and grid-based animation definitions
- **Input Mapper** — push-based input mapping via reactive `IInput` observables

## Documentation

The docs live in `docs/` and are the authoritative reference.

| Topic | File |
|-------|------|
| Elmish runtime | `docs/elmish.md` |
| System pipeline (phases + snapshot) | `docs/system.md` |
| Scaling ladder | `docs/scaling.md` |
| Input | `docs/input.md` |
| Assets | `docs/assets.md` |
| Camera | `docs/camera.md` |
| Animation | `docs/animation.md` |
| Layout (2D) | `docs/layout.md` |
| Layout3D | `docs/layout3d.md` |
| Culling | `docs/culling.md` |
| Commands | `docs/commands.md` |

To build the docs site locally:

```bash
dotnet tool restore
dotnet fsdocs build
# or for live editing:
dotnet fsdocs watch
```

## Getting started

Prerequisites:
- **.NET SDK 8** or later
- A working OpenGL setup

```bash
dotnet --version
dotnet tool restore
dotnet restore
dotnet build
dotnet test
```

### Run the samples

**2D Platformer:**
```bash
dotnet run --project samples/PlatformerSample
```
Controls: **WASD / Arrows** to move, **Space** to jump, **R** to respawn.

**3D Platformer:**
```bash
dotnet run --project samples/ThreeDSample
```
Controls: **WASD** (camera-relative), **Space** to jump.

## Project structure

```
src/
├── Mibo.Raylib/           # Core framework library
│   ├── Elmish.*.fs        # MVU runtime (Cmd, Sub, Time, Program, System)
│   ├── Graphics2D.fs      # 2D render commands + batch renderer
│   ├── Graphics3D.fs      # 3D render commands + batch renderer
│   ├── Input.fs           # Raw input polling (Keyboard, Mouse)
│   ├── InputMapper.fs     # Reactive input mapping (IInput, ActionState)
│   ├── Camera.fs          # Camera2D, Camera3D, Ray
│   ├── Animation.fs       # Sprite sheets, animated sprite state
│   ├── Culling.fs         # Frustum / quadtree culling
│   ├── Layout/            # 2D grid layout (CellGrid2D, platformer, top-down)
│   └── Layout3D/          # 3D voxel layout (CellGrid3D, terrain, interior)
├── Mibo.Raylib.Tests/     # Unit tests (Expecto, 110+ tests)
samples/
├── PlatformerSample/      # 2D platformer sample
└── ThreeDSample/          # 3D platformer sample
docs/                      # Documentation source (fsdocs)
```

## Design principles

- **Pure `update`** — keep your model logic functional and testable
- **Declarative `view`** — submit render commands to a `RenderBuffer` rather than calling draw functions directly
- **Ladder of complexity** — start with simple sprites, progressively adopt shaders, lighting, and post-processing without rewrites
- **Zero-cost abstractions** — structs, arrays, `ArrayPool`, and `InlineIfLambda` where performance matters
- **No Content Pipeline** — all assets loaded at runtime from loose files

## License

Mibo.Raylib is distributed under the [zlib/libpng License](LICENSE).

## Built on

Mibo.Raylib is built on top of:

- [raylib](https://github.com/raysan5/raylib) — the cross-platform graphics library that powers the rendering, input, and audio layers
- [raylib-cs](https://github.com/raylib-cs/raylib-cs) — the C# bindings that make raylib accessible from .NET

## Feedback

Issues and PRs are very welcome. If you're interested in using F# for game development beyond simple 2D games, Mibo.Raylib aims to be a practical, batteries-included framework that scales with your ambition.
