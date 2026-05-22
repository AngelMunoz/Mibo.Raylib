---
title: Culling
category: Rendering
categoryindex: 3
index: 14
---

# Culling (visibility helpers)

`Mibo.Elmish.Culling` is a helper module that keeps _visibility math_ separate from your renderer and your spatial partitioning.

`Mibo.Elmish.Culling` is a tiny helper module that keeps _visibility math_ separate from your renderer and your spatial partitioning.

It operates on geometric primitives such as:

- Bounding frustums (computed from camera matrices)
- `BoundingSphere` / `BoundingBox` (from `System.Numerics`)
- 2D `Rectangle` overlap

## 3D: frustum culling

Create a `Frustum` from the camera's `View * Projection` matrix and test geometry:

```fsharp
// Extract frustum from camera matrices
let frustum = Frustum(Matrix4x4.Multiply(cam.View, cam.Projection))

if Culling.isVisible frustum entitySphere then
    // submit draw commands
    ()
```

Or for axis-aligned bounding boxes:

```fsharp
if Culling.isGenericVisible frustum nodeBounds then
    ()
```

## 2D: rectangle overlap

Use `Camera2D.viewportBounds` with `Culling.isVisible2D`:

```fsharp
let viewBounds = getViewportBounds camera viewportSize

if Culling.isVisible2D viewBounds spriteBounds then
    ()
```

## What this is _not_

This module doesn't try to be your spatial index.

- If you have many objects: use a grid / quadtree / BVH / octree.
- Use these helpers at the edge: "is this node/object worth considering for rendering?"

## Until ported

Until `Mibo.Elmish.Culling` is available, implement visibility checks manually using `System.Numerics` primitives or a third-party spatial library.

See also: [Camera](camera.html) and [Rendering overview](rendering.html).
