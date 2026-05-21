# Mibo Raylib PoC — Outcomes & Decision Record

## Context

This repository contains a **proof-of-concept** port of the Mibo F# micro-framework from **MonoGame** to **raylib-cs** (raylib 5.5). The goal was to validate whether raylib could replace MonoGame as the low-level backend while preserving Mibo's MVU/Elmish architecture and, critically, its **ladder-of-complexity** design philosophy: users start with simple 2D sprites and can progressively adopt advanced features like shaders, lighting, and post-processing without hitting architectural walls.

## What Was Built

### Core Framework (`src/Mibo.Raylib`)

- **Elmish loop**: `Cmd`, `Sub`, `Time`, `DispatchQueue`, and a `RaylibGame` host that bridges the imperative raylib window loop with a functional `init / update / view / subscribe` API.
- **Render buffering**: `RenderBuffer<'Cmd>` with layer sorting (`int<RenderLayer>`) so that `view` remains pure and declarative.
- **Asset service**: Texture, Font, Sound, and **Model** loading with dictionary-based caches — no MonoGame Content Pipeline (`.mgcb`) required.
- **Input abstraction**: `ActionState`, `InputMap`, and `Keyboard.poll` that map raw keys to domain actions.
- **2D batch renderer**: `Batch2DRenderer` implementing `IRenderer<'Model>`. Consumes a sorted buffer of `RenderCmd2D` and submits everything through raylib's `DrawTexturePro`.
- **3D batch renderer**: `Batch3DRenderer` with `RenderCmd3D` — `DrawModel`, `DrawModelEx`, `DrawCube`, `DrawLine3D`, camera setup, and custom shader switching.
- **Lighting system (2D)**: CPU-per-sprite light accumulation supporting ambient, directional (sun/moon), and point lights (torches) with distance attenuation.
- **Lighting system (3D)**: GPU Phong shader with per-pixel ambient + directional + up to 4 point lights. Shader strings embedded in `DefaultShaders.fs`, uniforms uploaded by `Batch3DRenderer`.
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

### 3D Platformer Sample (`samples/ThreeDSample`)

- **Procedural 3D level**: 12×12 grid of 4×4 floor tiles, elevated 2×2 platforms, staircases, floating challenge blocks. Level generated from original Mibo 3DSample `.obj` assets (Kenney platformer pack).
- **Player ball**: loaded `ball_blue.obj`, rendered with `DrawModelEx` and rotation offset.
- **Camera-relative controls**: WASD mapped to camera-forward/right vectors for intuitive isometric movement.
- **Smooth movement**: acceleration/friction model matching original 3DSample (`acceleration = 25`, `friction = 8`).
- **3D AABB collision**: ground/ceiling Y-axis resolution only; player X/Z is free (no wall-snapping). Physics position derived from actual `GetModelBoundingBox` at runtime.
- **Per-platform wireframe grids**: distance-faded line grids rendered just below each platform, inherited from original 3DSample's grid effect.
- **Day/night cycle (GPU)**: Phong shader ambient + animated sun direction (arcs east→west) + moon directional light + torch point lights. Sky `ClearBackground` color shifts with time.
- **Model material shader patching**: `ModelHelper.setMaterialShader` uses `NativePtr` to overwrite `Model.Materials[0].Shader` so that `DrawModel` uses the custom Phong shader instead of raylib's default.

## Key Technical Findings

| Topic | Finding |
|-------|---------|
| **Content Pipeline** | ❌ Not needed. raylib loads `.png`, `.ttf`, `.ogg`, `.obj` directly at runtime. This removes a major friction point for F# developers. |
| **Shader embedding** | ✅ **Killer feature.** GLSL shaders can be defined as plain strings in the framework library (`DefaultShaders.fs`) and loaded via `LoadShaderFromMemory`. Userland consumes them through a typed record (`PostProcessConfig`). This is exactly the ladder-of-complexity pattern Mibo needs: the framework owns the hard shader code; the user just flips a toggle or sets a tint color. |
| **F# interop** | ⚠️ `CBool` requires explicit `CBool.op_Implicit()` casts. `BlendMode` and `ShaderUniformDataType` use .NET-style naming (not C `#define` macros). Once wrapped, the API is clean. |
| **2D shape rendering** | ⚠️ `DrawRectangleRec`, `DrawTriangle`, and other raylib shape functions fail to render inside an active `BeginMode2D` context when called after `DrawTexturePro` texture batches. This is an `rlgl` batch state limitation, not a bug in our code. **Workaround:** all primitives (including shadows) are rendered as textured quads via `DrawTexturePro`, which is fully compatible with the camera transform. |
| **3D custom shaders** | ⚠️ `DrawModel` ignores `BeginShaderMode` and uses the model material's own shader. **Workaround:** patch `Model.Materials[0].Shader` directly via `NativePtr` so the model carries the custom shader. This proved the 3D lighting pipeline is viable but requires pointer manipulation in F#. |
| **Camera2D** | Mutable struct in raylib-cs. Must be initialized as `let mutable c = Camera2D()` before setting fields. |
| **Camera3D** | Same pattern — mutable struct; `FovY` field (not `Fov`). |
| **RenderTexture2D blit** | `sourceRect` height must be negative (`-th`) to flip Y for screen-space blitting. |
| **Performance** | No issues observed at 60 FPS with dozens of lights, hundreds of tiles, and a full post-process pass on integrated AMD graphics. |

## Architecture Decisions

1. **`RenderCmd2D` / `RenderCmd3D` as non-`[<Struct>]` discriminated unions** — avoids F# struct union field-name collision errors and keeps the declarative API readable. The same `RenderBuffer<'Key, 'Cmd>` generic works for both 2D and 3D.
2. **CPU-side light accumulation (2D)** — `Lighting2D.computeLightColor` runs per sprite-center rather than per-pixel. Simpler for the PoC; a shader-based deferred/pass approach can be layered in later without changing the user-facing `AddPointLight` / `SetLighting` API.
3. **GPU-side Phong lighting (3D)** — custom vertex+fragment shader with uniform arrays for point lights. The renderer scans the buffer for light commands and uploads uniforms when `SetShader3D` is encountered. This mirrors the 2D CPU-accumulation pattern but moves the work to the GPU where it belongs for 3D.
4. **Shadows as sprite quads (2D)** — contact shadows are rendered via `DrawSprite` (textured quads) rather than raylib shape primitives. This proved that occluder data flows correctly from `model.Occluders` → `RenderBuffer` → renderer, even if the final geometry is deferred to a future shader implementation.
5. **Model material shader patching (3D)** — `ModelHelper.setMaterialShader` overwrites the material's shader pointer directly because raylib's `DrawModel` will not respect `BeginShaderMode`. This is a necessary evil; a future wrapper could hide the `NativePtr` behind a safe API.
6. **No C# interop wrappers** — everything is idiomatic F#. Helper functions live in `RaylibHelpers.fs`.

## 3D Shader Patching — Why It Was Needed

### The Problem

raylib's `DrawModel` **ignores `BeginShaderMode`**. Internally, `DrawModel` extracts the shader from `model.materials[meshMaterial].shader` and binds it directly. No matter what shader you activate globally, the model wins.

This means our `Batch3DRenderer` would call:

```fsharp
Raylib.BeginShaderMode(phongShader)      // sets global shader to Phong (ID 6)
Raylib.DrawModel(model, pos, 1.0f, White) // IGNORES global shader, uses default (ID 3)
```

The Phong shader never gets used. Models render with flat default lighting.

### The PoC Workaround

`ModelHelper.setMaterialShader` performs direct memory surgery on the loaded model:

```fsharp
let matPtr = model.Materials
let mutable mat = NativePtr.read matPtr   // read Material[0]
mat.Shader <- shader                       // replace shader
NativePtr.write matPtr mat                 // write back
```

This permanently replaces the model's material shader with our Phong shader. After patching, `DrawModel` uses Phong because the model itself carries it.

### Impact on Userland (PoC)

In the sample, the user must manually patch every loaded model:

```fsharp
let phong = loadPhong3DShader()
ModelHelper.setMaterialShader playerModel phong
ModelHelper.setMaterialShader platformModel phong
// ... repeat for every distinct model
```

This leaks `NativePtr` and an implementation detail (`DrawModel` ignores `BeginShaderMode`) into userland.

### Selected Solution for the Main Repo: Option A + B

The framework will hide this entirely inside the asset cache.

**Option A:** `IAssets` gains a `ModelWithShader(path, shader)` method that loads the model, patches all materials internally, caches the result, and returns it.

**Option B:** The cache is shader-aware so the same `.obj` can coexist with different shaders:

```fsharp
type IAssets =
    abstract Model: path: string -> Model                    // default shader
    abstract ModelWithShader: path: string * shader: Shader -> Model  // patched

// Userland — completely safe, no pointers, no leaks
let ball = ctx.Assets.ModelWithShader("Models/ball_blue.obj", phong)
```

The `NativePtr` manipulation becomes a **framework-internal implementation detail** inside `Assets.fs`. Userland never sees it. Multi-material models are supported by looping all `materialCount` entries.

---

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
6. **Retain the `RenderBuffer` / `IRenderer` abstraction** — it has proven clean enough to support 2D, 3D, and mixed renderers without changing the Elmish loop.
7. **Hide `ModelHelper.setMaterialShader` behind `IAssets.ModelWithShader`** (Option A + B) so userland never touches `NativePtr`. The asset cache patches materials at load time and supports the same `.obj` with different shaders via a shader-aware cache key.
8. **Only after parity is proven**: begin advanced 3D shader work (SSAO, deferred rendering, post-process chains) that raylib's runtime shader loading makes practical.

## Architectural Roadmap: Shared Core, Divergent Renderers

The PoC proved that **2D and 3D can share the same Elmish core and diverge only at the renderer boundary**:

```
┌─────────────────────────────────────────┐
│  Mibo.Core (shared)                     │
│  ─────────────────                      │
│  Cmd, Sub, Time, DispatchQueue          │
│  RenderBuffer<'Key, 'Cmd>              │
│  IRenderer<'Model>                     │
│  GameContext, InputMap, Keyboard       │
│  AssetCache (Texture, Font, Sound,      │
│              Model, Shader)              │
└─────────────────────────────────────────┘
                    │
      ┌─────────────┼─────────────┐
      ▼             ▼             ▼
┌──────────┐ ┌──────────┐ ┌──────────────┐
│ 2D Sprite│ │ 3D Model │ │ 2D+3D Mixed  │
│ Renderer │ │ Renderer │ │ Renderer     │
│          │ │          │ │ (e.g. UI     │
│ RenderCmd│ │ RenderCmd│ │  overlay on  │
│ 2D       │ │ 3D       │ │  3D scene)   │
└──────────┘ └──────────┘ └──────────────┘
```

Because `IRenderer<'Model>` only demands `Draw(GameContext, 'Model, GameTime)`, you can register **multiple renderers** in the Elmish program. A mixed 2D/3D game would simply do:

```fsharp
Program.withRenderer(fun () -> Batch3DRenderer.create scene3DView)
|> Program.withRenderer(fun () -> Batch2DRenderer.create hud2DView)
```

raylib allows `BeginMode3D` → `EndMode3D` → `BeginMode2D` → `EndMode2D` in the same frame, so the execution order is safe.

### What Is Possible with This Architecture

| Feature | Feasibility | Notes |
|---------|-------------|-------|
| **Shared `RenderBuffer`** | ✅ Proven | Same generic buffer sorts by `RenderLayer` for both 2D and 3D. |
| **Mixed 2D/3D scenes** | ✅ Proven | Register two renderers; raylib supports sequential 3D→2D per frame. |
| **Particles** | ✅ Trivial | `RenderCmd3D.DrawCube` / `DrawSphere` / `DrawModel` with random positions each frame. CPU or GPU (compute shader) both possible. |
| **Quad batches** | ✅ Trivial | `DrawTexturePro` in 2D, `DrawModelEx` with flat billboard quads in 3D. raylib's `rlgl` API exposes `rlVertex3f` for custom mesh submission if needed. |
| **Animated sprites** | ✅ Proven | `sourceRect` animation works exactly like MonoGame. |
| **Skeletal animation** | ⚠️ Needs work | raylib loads `.gltf`/`.fbx` but animation playback requires `UpdateModelAnimation` — not yet wrapped in F#. |
| **Shadow mapping** | ⚠️ Needs work | Requires depth `RenderTexture2D`, custom depth shader, light-space matrix math. All doable, but not in PoC. |
| **Deferred rendering** | ⚠️ Needs work | Requires MRT (Multiple Render Targets). raylib supports `LoadRenderTexture` with depth; G-buffer needs custom FBO setup via `rlgl`. |
| **Post-process chains** | ✅ Proven | `RenderTexture2D` capture + shader blit already works for 2D. Same pipeline works for 3D (capture 3D scene to texture, then blit with effects). |
| **Bloom / SSAO** | ⚠️ Needs work | Standard multi-pass shader techniques. Requires ping-pong between two `RenderTexture2D`s. Framework code, not API limitation. |

## Running the PoC

**2D Platformer:**
```bash
cd samples/PlatformerSample
dotnet run
```
Controls: **WASD / Arrows** to move, **Space** to jump, **R** to respawn.

**3D Platformer:**
```bash
cd samples/ThreeDSample
dotnet run
```
Controls: **WASD** (camera-relative), **Space** to jump. Watch the sky darken, sun arc across the sky, and torches glow at night.

---

*PoC completed: 2026-05-20*  
*Decision: Invest further in raylib-cs as a side-by-side backend; target API parity with MonoGame, then unlock advanced shader-driven 3D features.*
*Effort cost: ~$7.18 USD via Kimi K2.6 Model (OpenCode Zen / opencode cli).*
*3D extension: custom Phong shader, model loading, camera-relative controls, day/night GPU lighting.*
