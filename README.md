# Mibo Raylib PoC — Outcomes & Decision Record

## Context

This repository contains a **proof-of-concept** port of the Mibo F# micro-framework from **MonoGame** to **raylib-cs** (raylib 5.5). The goal was to validate whether raylib could replace MonoGame as the low-level backend while preserving Mibo's MVU/Elmish architecture and, critically, its **ladder-of-complexity** design philosophy: users start with simple 2D sprites and can progressively adopt advanced features like shaders, lighting, and post-processing without hitting architectural walls.

## What Was Built

### Core Framework (`src/Mibo.Raylib`)

- **Elmish loop**: `Cmd`, `Sub`, `Time`, `DispatchQueue`, and a `RaylibGame` host that bridges the imperative raylib window loop with a functional `init / update / view / subscribe` API.
- **Render buffering**: `RenderBuffer<'Cmd>` with layer sorting (`int<RenderLayer>`) so that `view` remains pure and declarative.
- **Asset service**: Texture, Font, and Sound loading with dictionary-based caches — no MonoGame Content Pipeline (`.mgcb`) required.
- **Input abstraction**: `ActionState`, `InputMap`, and `Keyboard.poll` that map raw keys to domain actions.
- **2D batch renderer**: `Batch2DRenderer` implementing `IRenderer<'Model>`. Consumes a sorted buffer of `RenderCmd2D` and submits everything through raylib's `DrawTexturePro`.
- **Lighting system**: CPU-per-sprite light accumulation supporting ambient, directional (sun/moon), and point lights (torches) with distance attenuation.
- **Post-processing pipeline**: `RenderTexture2D` capture + embedded GLSL fragment shader (sepia tint) applied as a full-screen pass. The shader source lives in **library code** (`DefaultShaders.fs`) and is exposed to userland through a simple `PostProcessConfig` record.

### Platformer Sample (`samples/PlatformerSample`)

- **Procedural terrain**: segmented ground with gaps, elevated platforms, and full AABB collision resolution.
- **Player controller**: horizontal movement, variable jump, gravity integration, grounded state, facing direction.
- **Sprite animation**: idle, walk (2-frame), jump, fall with horizontal flip via negative source-rectangle width.
- **Camera**: 2D follow with left-edge constraint.
- **Audio**: jump SFX via `Raylib.PlaySound`.
- **Day / night cycle**: dynamic sky gradient, ambient light temperature, sun and moon directional lights that fade in/out based on time-of-day.
- **Dynamic point lights**: torch lights attached to platforms with warm color and radius attenuation.
- **Contact shadows**: sprite-based shadow quads rendered beneath platforms using a procedurally generated 1×1 alpha texture. Proves shadow data flow without fighting raylib's shape batching limitations.

## Key Technical Findings

| Topic | Finding |
|-------|---------|
| **Content Pipeline** | ❌ Not needed. raylib loads `.png`, `.ttf`, `.ogg` directly at runtime. This removes a major friction point for F# developers. |
| **Shader embedding** | ✅ **Killer feature.** GLSL shaders can be defined as plain strings in the framework library (`DefaultShaders.fs`) and loaded via `LoadShaderFromMemory`. Userland consumes them through a typed record (`PostProcessConfig`). This is exactly the ladder-of-complexity pattern Mibo needs: the framework owns the hard shader code; the user just flips a toggle or sets a tint color. |
| **F# interop** | ⚠️ `CBool` requires explicit `CBool.op_Implicit()` casts. `BlendMode` and `ShaderUniformDataType` use .NET-style naming (not C `#define` macros). Once wrapped, the API is clean. |
| **2D shape rendering** | ⚠️ `DrawRectangleRec`, `DrawTriangle`, and other raylib shape functions fail to render inside an active `BeginMode2D` context when called after `DrawTexturePro` texture batches. This is an `rlgl` batch state limitation, not a bug in our code. **Workaround:** all primitives (including shadows) are rendered as textured quads via `DrawTexturePro`, which is fully compatible with the camera transform. |
| **Camera2D** | Mutable struct in raylib-cs. Must be initialized as `let mutable c = Camera2D()` before setting fields. |
| **RenderTexture2D blit** | `sourceRect` height must be negative (`-th`) to flip Y for screen-space blitting. |
| **Performance** | No issues observed at 60 FPS with dozens of lights, hundreds of tiles, and a full post-process pass on integrated AMD graphics. |

## Architecture Decisions

1. **`RenderCmd2D` as a non-`[<Struct>]` discriminated union** — avoids F# struct union field-name collision errors and keeps the declarative API readable.
2. **CPU-side light accumulation** — `Lighting2D.computeLightColor` runs per sprite-center rather than per-pixel. Simpler for the PoC; a shader-based deferred/pass approach can be layered in later without changing the user-facing `AddPointLight` / `SetLighting` API.
3. **Shadows as sprite quads** — contact shadows are rendered via `DrawSprite` (textured quads) rather than raylib shape primitives. This proved that occluder data flows correctly from `model.Occluders` → `RenderBuffer` → renderer, even if the final geometry is deferred to a future shader implementation.
4. **No C# interop wrappers** — everything is idiomatic F#. Helper functions live in `RaylibHelpers.fs`.

## Outcomes

### ✅ Worth Further Investment

**This PoC is a green light to commit more effort and time to a raylib backend — not an immediate replacement of MonoGame.**

The most compelling signal is **shader ownership**. In MonoGame, shaders are compiled offline via the Content Pipeline and referenced by opaque asset names. In raylib, shaders are **plain GLSL strings** that can be authored in library code and loaded at runtime with `LoadShaderFromMemory(null, fragmentCode)`. This means:

- The framework can ship built-in effects (post-process, lighting, water, etc.) as embedded resources.
- The user opts in by setting a field on a config record — no `.fx` compilation, no content project, no C# tooling.
- Advanced users can still inject their own shader code without leaving F#.

This maps perfectly to Mibo's **ladder of complexity**:

```
User: DrawSprite → SetLighting → PostProcessConfig → CustomShader
        ↑              ↑                ↑                  ↑
Framework:  Core  →  Lighting2D  →  DefaultShaders  →  Shader API
```

That capability is what makes raylib worth pursuing: once we reach API parity with the existing MonoGame backend, we can layer in advanced shader-driven 3D features (SSAO, deferred lighting, screen-space reflections) that are prohibitively awkward under MonoGame's Content Pipeline model.

### What This Unlocks for Mibo — *After* Parity

1. **Immediate mode, no pipeline** — Artists can drop `.png` files into a folder and run. The edit-compile-run cycle is seconds, not minutes.
2. **Shader-native framework** — Effects are first-class code, not second-class assets. This is essential for a framework that wants to provide high-quality defaults out of the box.
3. **Cross-platform desktop** — raylib targets Windows, Linux, macOS, and WebAssembly with the same API surface.
4. **Small dependency footprint** — A single NuGet package (`raylib-cs`) replaces the MonoGame + Content Pipeline + mgcb toolchain.

## Recommended Next Steps for the Main Repo

1. **Create `Mibo.Raylib` as a side-by-side library project** in the main Mibo repository, alongside the existing MonoGame backend. Do **not** remove MonoGame yet.
2. **Drive toward 100% API parity** — every `RenderCmd`, asset loader, and input helper that exists in the MonoGame backend must have a raylib equivalent so that existing user projects compile against either backend with minimal changes.
3. **Expand `DefaultShaders.fs`** with a library of reusable effects: bloom, CRT scanlines, palette swapping, normal-map lighting.
4. **Replace CPU light accumulation with a shader-based forward lighting pass** once parity is achieved and the shadow technique graduates to a full shadow-map or SDF approach.
5. **Add `LoadShaderFromMemory` wrapper** that accepts vertex + fragment F# strings and returns a typed `ShaderHandle`, hiding `rlgl` details.
6. **Retain the `RenderCmd2D` / `IRenderer` abstraction** — it has proven clean enough to support both 2D and (future) 3D renderers without changing the Elmish loop.
7. **Only after parity is proven**: begin advanced 3D shader work (SSAO, deferred rendering, post-process chains) that raylib's runtime shader loading makes practical.

## Running the PoC

```bash
cd samples/PlatformerSample
dotnet run
```

Controls: **WASD / Arrows** to move, **Space** to jump, **R** to respawn.

---

*PoC completed: 2026-05-19*  
*Decision: Invest further in raylib-cs as a side-by-side backend; target API parity with MonoGame, then unlock advanced shader-driven 3D features.*
*Effort cost: ~$7.18 USD via Kimi K2.6 Model (OpenCode Zen / opencode cli).*
