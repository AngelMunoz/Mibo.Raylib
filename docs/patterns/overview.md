---
title: Patterns Overview
category: Patterns
categoryindex: 6
index: 61
---

# Patterns Overview

Real-world patterns extracted from the Mibo.Raylib samples. Each page shows a concrete technique you can apply to your own game.

These are not API references. They are working recipes — copied from code that runs in the `ThreeDSample` and `PlatformerSample` projects.

## Available Patterns

| Pattern | What it solves |
|---------|---------------|
| [System Pipeline](system-pipeline.html) | Organizing many update phases into a composable chain |
| [Async Chunk Loading](async-chunks.html) | Background world generation without blocking the main thread |
| [3D Particles](particles-3d.html) | Billboard particle system with zero GC pressure |
| [Multi-Renderer Compositing](multi-renderer.html) | Layering 2D HUD on top of a 3D scene |
| [Day/Night Cycle](day-night.html) | Time-based lighting transitions as a standalone system |

## How to read these pages

Each pattern follows the same structure:

1. **What and Why** — What the pattern does and when you need it.
2. **When to use** — Concrete signals that this pattern applies.
3. **Quick Start** — Minimal code to get it running.
4. **Deep Dive** — How it works internally, with real sample code.
5. **See also** — Cross-links to related docs.

## Source references

All code samples come from `samples/ThreeDSample/`. You can read the full implementation there.

See also: [System Pipeline](system.html) (API reference), [Scaling Mibo.Raylib](scaling.html) (architecture ladder).
