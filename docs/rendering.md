---
title: Rendering Overview
category: Rendering
categoryindex: 3
index: 10
---

# Rendering Overview

Mibo.Raylib uses a **deferred, layer-sorted rendering pipeline**. Instead of calling raylib draw functions directly, you build a list of commands each frame, and the renderer sorts and executes them in one pass.

## The Pipeline

1. Your view function builds `IRenderCommand2D` commands and adds them to a `RenderBuffer2D`
2. The renderer sorts commands by `Layer` (ascending)
3. The renderer executes commands in order
4. raylib auto-batches GPU draw calls; optional post-processing passes run after

## Why Deferred Rendering?

- **Separation of concerns**: Your view doesn't have to worry about draw order, batching, or GPU state.
- **Lighting**: Commands can be interleaved with light commands for 2D lighting (see [Lighting](graphics2d/overview.html#lighting)).
- **Post-processing**: Screen-space shader passes run after the scene is rendered.
- **Predictable ordering**: Every command declares its layer explicitly.

## 2D Rendering

The 2D pipeline is built on `Renderer2D<'Model>` in the `Mibo.Elmish.Graphics2D` namespace. See:

- [2D Rendering Overview](graphics2d/overview.html) — What, Why, When
- [Buffer & Commands](graphics2d/buffer-and-commands.html) — Building and issuing draw commands
- [Lighting & Shadows](graphics2d/lighting.html) — 2D lights and soft shadows
- [Particles](graphics2d/particles.html) — Batched particle rendering
- [Custom Commands](graphics2d/custom-commands.html) — `IRenderCommand2D` and escape hatches
- [Performance](graphics2d/performance.html) — Writing performant 2D rendering code
- [Camera](camera.html) — Cameras and coordinate systems
- [Culling](culling.html) — Visibility testing

## 3D Rendering

The 3D pipeline (coming soon) is built on `Batch3DRenderer<'Model>` in the `Mibo.Elmish.Graphics3D` namespace.
