namespace Mibo.Elmish.Graphics3D

open System.Numerics
open Raylib_cs

/// <summary>
/// Pre-generated primitive meshes for 3D rendering.
/// Generated once at module initialization to avoid runtime mesh generation overhead.
/// </summary>
/// <remarks>
/// Use these with <see cref="T:Mibo.Elmish.Graphics3D.Pipelines.ForwardPbrPipeline"/>
/// instead of calling <c>Raylib.DrawSphere</c>, <c>Raylib.DrawCube</c>, etc.
/// This ensures the active pipeline's shader is bound, not raylib's default shader.
/// </remarks>
module Primitive3D =

  /// <summary>A unit sphere mesh (radius 1, 32x32 segments).</summary>
  let sphere: Mesh = Raylib.GenMeshSphere(1.0f, 32, 32)

  /// <summary>A unit cube mesh (1x1x1).</summary>
  let cube: Mesh = Raylib.GenMeshCube(1.0f, 1.0f, 1.0f)

  /// <summary>A unit cylinder mesh (radius 1, height 1, 32 segments).</summary>
  let cylinder: Mesh = Raylib.GenMeshCylinder(1.0f, 1.0f, 32)

  /// <summary>A unit plane mesh (1x1, 1x1 subdivisions).</summary>
  let plane: Mesh = Raylib.GenMeshPlane(1.0f, 1.0f, 1, 1)

  /// <summary>A torus mesh (inner radius 0.5, outer radius 1, 32x32 segments).</summary>
  let torus: Mesh = Raylib.GenMeshTorus(0.5f, 1.0f, 32, 32)

  /// <summary>A unit cone mesh (radius 1, height 1, 32 segments).</summary>
  let cone: Mesh = Raylib.GenMeshCone(1.0f, 1.0f, 32)
