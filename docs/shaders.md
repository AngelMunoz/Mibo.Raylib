---
title: Shaders
category: Rendering
categoryindex: 3
index: 15
---

# Shaders

Shaders are GPU programs that transform vertices and determine pixel colors. They run on the graphics card in parallel, making them efficient for complex visual effects.

## What They Are

- **Vertex shaders** transform 3D model vertices into screen space
- **Fragment shaders** (also called pixel shaders) determine the final color of each pixel
- **Shaders** (in raylib terminology) package vertex+fragment pairs with parameters

## Why Use Them

Use shaders when you need visual effects beyond what built-in rendering provides:

- Custom lighting models (toon shading, stylized PBR)
- Post-processing effects (bloom, tone mapping, color grading)
- Special effects (holograms, distortion, pixelation)
- Optimized rendering for specific art styles

## When to Write Them

You don't need custom shaders to start. Mibo.Raylib's built-in renderers work without them:

- **2D games**: Use `Graphics2D` with standard raylib drawing (no shaders required)
- **3D games**: Use `Graphics3D` with standard raylib model rendering (works without custom shaders)

Write shaders when:
- You have specific visual requirements
- You need performance optimizations for your target hardware
- You're building advanced rendering features

## Loading Shaders

Shaders are GLSL strings loaded at runtime using `Raylib.LoadShaderFromMemory` or `Raylib.LoadShader`. There is no content pipeline — shaders are plain `.fs`/`.vs` files or embedded strings.

```fsharp
open Raylib_cs

// Load from file
let myShader = Raylib.LoadShader("shaders/vertex.vs", "shaders/fragment.fs")

// Or load from memory (GLSL strings)
let fragCode = """
#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;

uniform vec4 tint;

void main() {
    vec4 texel = texture(texture0, fragTexCoord);
    finalColor = texel * tint;
}
"""

let myShader = Raylib.LoadShaderFromMemory(null, fragCode)
```

## Setting Parameters

Set shader parameters using `Raylib.SetShaderValue`:

```fsharp
open System.Numerics
open Raylib_cs

// Set a float uniform
Raylib.SetShaderValue(myShader, Raylib.GetShaderLocation(myShader, "tint"), Vector4.One, ShaderUniformDataType.ShaderUniformVec4)

// Set a matrix uniform
let world = Matrix4x4.Identity
Raylib.SetShaderValue(myShader, Raylib.GetShaderLocation(myShader, "world"), world, ShaderUniformDataType.ShaderUniformMat4)
```

| Uniform Type | `ShaderUniformDataType` |
|---|---|
| `float` | `ShaderUniformFloat` |
| `Vector2` | `ShaderUniformVec2` |
| `Vector3` | `ShaderUniformVec3` |
| `Vector4` | `ShaderUniformVec4` |
| `Matrix4x4` | `ShaderUniformMat4` |

## Where to Learn More

Shader binding contracts (what parameters each shader type expects) are documented in the rendering sections:

- **2D rendering shaders**: See [Rendering 2D](rendering2d.html) for shader contracts
- **3D rendering shaders**: See [3D Custom Shaders](3d-rendering/custom-shaders.html) for shader contracts

See also: [raylib shaders documentation](https://www.raylib.com/examples/shaders/loader.html?name=shaders_basic_lighting)
