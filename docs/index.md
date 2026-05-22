---
title: Welcome to Mibo.Raylib
category: Documentation
index: 0
---

# Mibo.Raylib: A Functional Game Framework for F#

Mibo.Raylib is a lightweight, Elmish-based game framework built on top of raylib. It brings the power of the **Model-View-Update (MVU)** architecture to game development, encouraging pure game logic and predictable state management.

## Getting Started

To get started with Mibo.Raylib, you need the [dotnet SDK](https://get.dot.net) installed.

Start by cloning the repository and running one of the samples:

```bash
git clone https://github.com/your-org/Mibo.Raylib
cd Mibo.Raylib
dotnet run --project samples/PlatformerSample
```

Or the 3D sample:

```bash
dotnet run --project samples/ThreeDSample
```

The projects in `samples/PlatformerSample` and `samples/ThreeDSample` show complete, working setups.

You can then start building your game using any of the following:

- [VsCode](https://code.visualstudio.com/) with the
  - [Ionide extension](https://marketplace.visualstudio.com/items?itemName=Ionide.Ionide-fsharp) (MS Registry)
  - [Ionide extension](https://open-vsx.org/extension/Ionide/Ionide-fsharp) (Open VSX Registry)
- [JetBrains Rider](https://www.jetbrains.com/rider/)
- [Visual Studio](https://visualstudio.microsoft.com/)

## Why Mibo.Raylib?

Traditional game engines often rely heavily on mutable state and complex object hierarchies. Mibo.Raylib offers an alternative:

- **Functional First**: Write your game logic as pure functions that transform state.
- **Predictable State**: The entire game state (the Model) is centralized and immutable.
- **Elmish Architecture**: Leverage the robust MVU pattern for clear separation of concerns.
- **raylib Power**: Benefit from the performance and simplicity of raylib.
- **Deferred Rendering**: Built-in 2D and 3D batchers that handle sorting and lighting for you.

## Core Patterns

### The Elmish Loop

Shader-based multi-pass rendering. Every Mibo.Raylib game follows a simple loop:

1. **Init**: Define your initial state.
2. **Update**: Purely calculate the next state based on messages (input, timers, etc.).
3. **View**: Describe what should be rendered based on the current state.
4. **Subscribe**: Listen to external events like keyboard or touch input.

### Semantic Input Mapping

Instead of checking for specific keys in your player logic, Mibo.Raylib encourages mapping keys to **Actions**. This allows for easy input rebinding and multi-device support.

### Deferred Batched Rendering

All rendering is deferred into a `RenderBuffer`, sorted by layer, and executed in a single pass per renderer. This enables efficient 2D lighting and post-processing without coupling render logic to the update loop.

## Getting Started

Run one of the samples, then copy its program setup (composition root) into your own project.

## Documentation

- Architecture
  - [Elmish (MVU) runtime](elmish.html)
  - [Programs & composition](program.html)
  - [System pipeline (phases + snapshot)](system.html)
  - [Service composition](services.html)
  - [Scaling Mibo.Raylib (Simple → Complex)](scaling.html)

 - Rendering
   - [Rendering 2D](rendering2d.html)
   - [Rendering 3D](3d-rendering/overview.html)

 - Input
   - [Input (raw + mapped)](input.html)

 - Assets
   - [Assets (loading + caching)](assets.html)
