namespace Mibo.Elmish.Graphics3D

open System.Numerics
open Raylib_cs

/// <summary>
/// Pipe-friendly drawing DSL for 3D rendering. Each function takes a
/// <see cref="T:Mibo.Elmish.Graphics3D.RenderBuffer3D"/> as its last argument,
/// adds the corresponding command, and returns the buffer for chaining.
/// </summary>
/// <remarks>
/// <para>
/// Commands are built via <see cref="T:Mibo.Elmish.Graphics3D.Command3D"/> and added to the buffer.
/// </para>
/// <para>
/// Usage:
/// <code lang="fsharp">
/// buffer
/// |> Draw3D.beginCamera worldCamera
/// |> Draw3D.drawModel model transform
/// |> Draw3D.addPointLight { Position = pos; Color = Color.White; Radius = 10f }
/// |> Draw3D.endCamera
/// |> Draw3D.drop
/// </code>
/// </para>
/// </remarks>
module Draw3D =

  // ──────────────────────────────────────────────
  // Geometry
  // ──────────────────────────────────────────────

  /// <summary>Draws a mesh with a world transform and material.</summary>
  let inline drawMesh
    (mesh: Mesh)
    (transform: Matrix4x4)
    (material: Material3D)
    (buffer: RenderBuffer3D)
    =
    buffer.Add(Command3D.drawMesh mesh transform material)
    buffer

  /// <summary>
  /// Draws a raylib model with a world transform.
  /// Each sub-mesh is drawn with its corresponding raylib material,
  /// converted to <see cref="T:Mibo.Elmish.Graphics3D.Material3D"/> automatically.
  /// </summary>
  let inline drawModel
    (model: Model)
    (transform: Matrix4x4)
    (buffer: RenderBuffer3D)
    =
    buffer.Add(Command3D.drawModel model transform)
    buffer

  /// <summary>Draws a billboard (camera-facing quad) with a texture.</summary>
  let inline drawBillboard
    (texture: Texture2D)
    (position: Vector3)
    (size: Vector2)
    (color: Color)
    (buffer: RenderBuffer3D)
    =
    buffer.Add(Command3D.drawBillboard texture position size color)
    buffer

  /// <summary>Draws a 3D line between two points.</summary>
  let inline drawLine3D
    (start: Vector3)
    (finish: Vector3)
    (color: Color)
    (buffer: RenderBuffer3D)
    =
    buffer.Add(Command3D.drawLine3D start finish color)
    buffer

  /// <summary>Draws a skinned mesh with bone matrix data.</summary>
  let inline drawSkinnedMesh
    (mesh: Mesh)
    (transform: Matrix4x4)
    (material: Material3D)
    (bones: Matrix4x4[])
    (buffer: RenderBuffer3D)
    =
    buffer.Add(Command3D.drawSkinnedMesh mesh transform material bones)
    buffer

  // ──────────────────────────────────────────────
  // Camera
  // ──────────────────────────────────────────────

  /// <summary>Begins a 3D camera transform.</summary>
  let inline beginCamera (camera: Camera3D) (buffer: RenderBuffer3D) =
    buffer.Add(Command3D.beginCamera camera)
    buffer

  /// <summary>Ends the current 3D camera transform.</summary>
  let inline endCamera (buffer: RenderBuffer3D) =
    buffer.Add(Command3D.endCamera())
    buffer

  // ──────────────────────────────────────────────
  // Lighting
  // ──────────────────────────────────────────────

  /// <summary>Sets the ambient light for the scene.</summary>
  let inline setAmbientLight (light: AmbientLight3D) (buffer: RenderBuffer3D) =
    buffer.Add(Command3D.setAmbientLight light)
    buffer

  /// <summary>Adds a directional light to the scene.</summary>
  let inline addDirectionalLight (light: DirectionalLight3D) (buffer: RenderBuffer3D) =
    buffer.Add(Command3D.addDirectionalLight light)
    buffer

  /// <summary>Adds a point light to the scene.</summary>
  let inline addPointLight (light: PointLight3D) (buffer: RenderBuffer3D) =
    buffer.Add(Command3D.addPointLight light)
    buffer

  // ──────────────────────────────────────────────
  // Debug Drawing
  // ──────────────────────────────────────────────

  /// <summary>
  /// Draws a ground grid centered at world origin.
  /// Uses <see cref="M:Raylib_cs.Raylib.DrawGrid"/> internally via
  /// <see cref="M:Mibo.Elmish.Graphics3D.IRenderContext3D.DrawImmediate"/>.
  /// </summary>
  let inline drawGrid
    (slices: int)
    (spacing: float32)
    (buffer: RenderBuffer3D)
    =
    buffer.Add(Command3D.drawGrid slices spacing)
    buffer

  /// <summary>
  /// Draws a ground grid with a custom color.
  /// Uses <see cref="M:Raylib_cs.Raylib.DrawGrid"/> internally.
  /// Note: raylib's DrawGrid ignores the color parameter;
  /// it is included for API consistency.
  /// </summary>
  let inline drawGridWithColor
    (slices: int)
    (spacing: float32)
    (color: Color)
    (buffer: RenderBuffer3D)
    =
    buffer.Add(Command3D.drawGridWithColor slices spacing color)
    buffer

  /// <summary>Draws a bounding box wireframe for debugging.</summary>
  let inline drawBoundingBox
    (box: BoundingBox)
    (color: Color)
    (buffer: RenderBuffer3D)
    =
    buffer.Add(Command3D.drawBoundingBox box color)
    buffer

  /// <summary>Draws a single point in 3D space for debugging.</summary>
  let inline drawPoint3D
    (position: Vector3)
    (color: Color)
    (buffer: RenderBuffer3D)
    =
    buffer.Add(Command3D.drawPoint3D position color)
    buffer

  /// <summary>Draws a ray visualization for debugging.</summary>
  let inline drawRay
    (ray: Ray)
    (color: Color)
    (buffer: RenderBuffer3D)
    =
    buffer.Add(Command3D.drawRay ray color)
    buffer

  // ──────────────────────────────────────────────
  // Escape Hatches
  // ──────────────────────────────────────────────

  /// <summary>Flushes raylib's batch, exits camera, runs action, restores state.</summary>
  let inline drawImmediate (action: unit -> unit) (buffer: RenderBuffer3D) =
    buffer.Add(Command3D.drawImmediate action)
    buffer

  /// <summary>Terminal function that discards the buffer, silencing the unused-value warning. Does nothing.</summary>
  let inline drop(_buffer: RenderBuffer3D) = ()