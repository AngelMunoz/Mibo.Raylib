---
title: Patterns Overview
category: Patterns
categoryindex: 6
index: 61
---

# Patterns Overview

Common game development patterns for Mibo.Raylib. Each page presents a general, reusable technique you can apply to any game.

These are not API references. They are working recipes — concrete solutions to problems you will hit as your game grows.

## Available Patterns

| Pattern | What it solves |
|---------|---------------|
| [System Pipeline](system-pipeline.html) | Organizing per-frame updates into a composable chain of independent systems |
| [Async Chunk Loading](async-chunks.html) | Streaming large worlds in the background without blocking the main thread |
| [3D Particles](particles-3d.html) | High-performance billboard particles with zero GC pressure |
| [Multi-Renderer Compositing](multi-renderer.html) | Layering 2D HUD, minimaps, and debug overlays on top of a 3D scene |
| [Day/Night Cycle](day-night.html) | Time-based lighting transitions as a standalone, testable system |

## How to read these pages

Each pattern follows the same structure:

1. **What and Why** — What the pattern does and when you need it.
2. **When to use** — Concrete signals that this pattern applies.
3. **Quick Start** — Minimal code to get it running.
4. **Deep Dive** — How it works internally, with detailed explanations.
5. **See also** — Cross-links to related docs.

## Source references

For complete, working implementations of all these patterns, see the `samples/ThreeDSample/` project.

See also: [System Pipeline](system.html) (API reference), [Scaling Mibo.Raylib](scaling.html) (architecture ladder).
