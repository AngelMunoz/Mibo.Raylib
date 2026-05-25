namespace Mibo.Elmish.Graphics3D

/// <summary>
/// A single 3D render command executed by the <see cref="T:Mibo.Elmish.Graphics3D.Renderer3D`1"/>.
/// </summary>
/// <remarks>
/// Implement this interface to create custom draw commands, lighting passes,
/// or any other rendering behavior that integrates into the 3D pipeline.
///
/// Commands are accumulated in a <see cref="T:Mibo.Elmish.Graphics3D.RenderBuffer3D"/>
/// and executed in insertion order. The pipeline may re-sort if needed for state efficiency.
///
/// For object-expression implementations:
/// <code lang="fsharp">
/// let myCommand =
///   { new IRenderCommand3D with
///       member _.Render ctx =
///         ctx.DrawMesh(myMesh, myTransform, myMaterial)
///   }
/// </code>
/// </remarks>
type IRenderCommand3D =

  /// <summary>
  /// Executes this command using the provided render context.
  /// Implementations may call standard raylib draw functions directly
  /// or use <see cref="M:Mibo.Elmish.Graphics3D.IRenderContext3D.DrawImmediate"/>
  /// for custom rlgl work.
  /// </summary>
  abstract Render: context: IRenderContext3D -> unit
