namespace Mibo.Elmish.Graphics3D

open System.Numerics
open Raylib_cs
open Mibo.Elmish

/// <summary>
/// Provides controlled access to raylib's 3D rendering state for render commands and pipelines.
/// This is the narrow waist between universal geometry commands and pipeline-specific shading.
/// </summary>
/// <remarks>
/// The implementation is internal to the active <see cref="T:Mibo.Elmish.Graphics3D.IRenderPipeline3D"/>.
/// Commands receive this context via <see cref="M:Mibo.Elmish.Graphics3D.IRenderCommand3D.Render"/>.
///
/// Lighting methods (<c>AddPointLight</c>, <c>AddDirectionalLight</c>, <c>SetAmbientLight</c>)
/// are advisory. Unsupported pipelines implement them as no-ops.
/// </remarks>
type IRenderContext3D =

  /// <summary>The game context for the current frame.</summary>
  abstract GameContext: GameContext

  /// <summary>
  /// Begins a 3D camera transform. If a different camera is already active,
  /// the current batch is flushed, the previous camera is ended, and the new camera is started.
  /// Safe to call multiple times with the same camera.
  /// </summary>
  abstract BeginCamera: camera: Camera3D -> unit

  /// <summary>
  /// Ends the currently active 3D camera transform. Flushes any pending draw batch
  /// before ending. Safe to call when no camera is active.
  /// </summary>
  abstract EndCamera: unit -> unit

  /// <summary>
  /// Draws a mesh with the given world transform and material.
  /// The pipeline binds the appropriate shader and uploads uniforms.
  /// </summary>
  abstract DrawMesh: mesh: Mesh * transform: Matrix4x4 * material: Material3D -> unit

  /// <summary>
  /// Draws a billboard (camera-facing quad) with the given texture.
  /// The pipeline handles the billboard matrix and shader binding.
  /// </summary>
  abstract DrawBillboard: texture: Texture2D * position: Vector3 * size: Vector2 * color: Color -> unit

  /// <summary>Draws a 3D line between two world-space points.</summary>
  abstract DrawLine3D: start: Vector3 * finish: Vector3 * color: Color -> unit

  /// <summary>
  /// Draws a skinned mesh with bone matrix data.
  /// Pipelines that do not support GPU skinning may use a CPU fallback.
  /// </summary>
  abstract DrawSkinnedMesh: mesh: Mesh * transform: Matrix4x4 * material: Material3D * boneMatrices: Matrix4x4[] -> unit

  /// <summary>
  /// Draws multiple instances of the same mesh with different transforms.
  /// Prefer this over individual <see cref="M:Mibo.Elmish.Graphics3D.IRenderContext3D.DrawMesh"/>
  /// calls when rendering many copies of the same mesh (e.g. foliage, debris).
  /// </summary>
  abstract DrawMeshInstanced: mesh: Mesh * transforms: Matrix4x4[] * material: Material3D * instanceCount: int -> unit

  /// <summary>
  /// Draws multiple billboards in a single batch.
  /// Prefer this over individual <see cref="M:Mibo.Elmish.Graphics3D.IRenderContext3D.DrawBillboard"/>
  /// calls when rendering many sprites at once (e.g. particles).
  /// </summary>
  abstract DrawBillboardBatch: textures: Texture2D[] * positions: Vector3[] * sizes: Vector2[] * colors: Color[] * count: int -> unit

  /// <summary>
  /// Adds a point light to the scene. Advisory — unsupported pipelines no-op.
  /// </summary>
  abstract AddPointLight: light: PointLight3D -> unit

  /// <summary>
  /// Adds a directional light to the scene. Advisory — unsupported pipelines no-op.
  /// </summary>
  abstract AddDirectionalLight: light: DirectionalLight3D -> unit

  /// <summary>
  /// Sets the ambient light for the scene. Advisory — unsupported pipelines no-op.
  /// </summary>
  abstract SetAmbientLight: light: AmbientLight3D -> unit

  /// <summary>
  /// Flushes raylib's internal render batch, temporarily exits camera mode,
  /// executes the given action, then restores camera state.
  /// </summary>
  /// <remarks>
  /// Use this for direct rlgl calls or any rendering that must not be batched.
  /// After the action completes, the camera mode that was active before the call is restored.
  /// </remarks>
  abstract DrawImmediate: action: (unit -> unit) -> unit
