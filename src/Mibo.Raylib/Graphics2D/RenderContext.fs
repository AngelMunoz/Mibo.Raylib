namespace Mibo.Elmish.Graphics2D

open System.Numerics
open Raylib_cs
open Mibo.Elmish

/// <summary>
/// Provides controlled access to raylib's 2D rendering state for render commands.
/// Tracks active camera and shader modes to prevent conflicting Begin/End calls,
/// and provides a safe escape hatch for direct rlgl operations.
/// </summary>
/// <remarks>
/// The implementation is internal to <see cref="T:Mibo.Elmish.Graphics2D.LegacyBatch2DRenderer`1"/>.
/// Commands receive this context via <see cref="M:Mibo.Elmish.Graphics2D.IRenderCommand2D.Render"/>.
/// </remarks>
type IRenderContext =

  /// <summary>The game context for the current frame.</summary>
  abstract GameContext: GameContext

  /// <summary>
  /// Begins a 2D camera transform. If a different camera is already active,
  /// the current batch is flushed, the previous camera is ended, and the new camera is started.
  /// Safe to call multiple times with the same camera.
  /// </summary>
  abstract BeginCamera: camera: Camera2D -> unit

  /// <summary>
  /// Ends the currently active camera transform. Flushes any pending draw batch
  /// before ending. Safe to call when no camera is active.
  /// </summary>
  abstract EndCamera: unit -> unit

  /// <summary>
  /// Begins a shader mode. If a different shader is already active,
  /// the current batch is flushed, the previous shader is ended, and the new shader is started.
  /// Safe to call multiple times with the same shader.
  /// </summary>
  abstract BeginShader: shader: Shader -> unit

  /// <summary>
  /// Ends the currently active shader mode. Flushes any pending draw batch
  /// before ending. Safe to call when no shader is active.
  /// </summary>
  abstract EndShader: unit -> unit

  /// <summary>
  /// Flushes raylib's internal render batch, temporarily exits both camera and shader
  /// modes, executes the given action, then restores camera and shader state.
  /// </summary>
  /// <remarks>
  /// Use this for direct rlgl calls (instancing, custom meshes, quad batches) or any
  /// rendering that must not be batched with standard raylib draw functions.
  /// After the action completes, the camera and shader modes that were active
  /// before the call are restored.
  /// </remarks>
  abstract DrawImmediate: action: (unit -> unit) -> unit
