namespace Mibo.Elmish.Graphics2D

/// <summary>
/// A single 2D render command executed by the <see cref="T:Mibo.Elmish.Graphics2D.Renderer2D`1"/>.
/// </summary>
/// <remarks>
/// Implement this interface to create custom draw commands, lighting passes,
/// particle systems, or any other rendering behavior that integrates into the
/// sorted layer pipeline.
///
/// Commands are sorted by <see cref="P:Mibo.Elmish.Graphics2D.IRenderCommand2D.Layer"/> before execution.
/// Lower values draw first (background), higher values draw last (foreground).
///
/// For object-expression implementations:
/// <code lang="fsharp">
/// let myCommand =
///   { new IRenderCommand2D with
///       member _.Layer = 10&lt;RenderLayer&gt;
///       member _.Render ctx =
///         ctx.BeginShader(myShader)
///         Raylib.DrawTexturePro(...)
///         ctx.EndShader()
///   }
/// </code>
/// </remarks>
type IRenderCommand2D =

  /// <summary>
  /// The render layer that determines this command's draw order.
  /// Lower values draw first (background), higher values draw last (foreground).
  /// </summary>
  abstract Layer: int<RenderLayer>

  /// <summary>
  /// Executes this command using the provided render context.
  /// Implementations may call standard raylib draw functions directly
  /// (batched automatically by raylib) or use
  /// <see cref="M:Mibo.Elmish.Graphics2D.IRenderContext.DrawImmediate"/>
  /// for custom rlgl work.
  /// </summary>
  abstract Render: context: IRenderContext -> unit
