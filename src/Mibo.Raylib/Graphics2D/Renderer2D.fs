namespace Mibo.Elmish.Graphics2D

open System
open System.Numerics
open Raylib_cs
open Mibo.Elmish

/// <summary>Configuration for a single-pass tint post-processing effect. Used by the legacy 3D renderer.</summary>
type PostProcessConfig = {
  /// <summary>Shader applied to the scene render texture.</summary>
  Shader: Shader
  /// <summary>Color tint mixed with the scene.</summary>
  TintColor: Color
  /// <summary>Blend amount between the scene and the tint color. 0 = scene only, 1 = tint only.</summary>
  TintAmount: float32
}

/// <summary>A single post-processing pass applied to the rendered scene.</summary>
[<Struct>]
type PostProcessPass = {

  /// <summary>Shader used for this pass. Receives the scene/render-texture as <c>texture0</c>.</summary>
  Shader: Shader

  /// <summary>
  /// Optional callback to set shader uniforms before rendering the fullscreen quad.
  /// Called once per frame when this pass executes. The raylib <see cref="T:Raylib_cs.Shader"/>
  /// is already active via <c>BeginShaderMode</c> when this callback runs.
  /// </summary>
  OnSetup: (Shader -> GameContext -> unit) voption
}

/// <summary>Configuration for the <see cref="T:Mibo.Elmish.Graphics2D.Renderer2D`1"/>.</summary>
[<Struct>]
type Renderer2DConfig = {

  /// <summary>
  /// Optional post-processing passes. Applied in order after the scene is rendered
  /// to a render texture, chaining via pooled render textures between passes.
  /// The last pass renders directly to the backbuffer.
  /// </summary>
  PostProcess: PostProcessPass[] voption

  /// <summary>
  /// Background clear color applied before rendering commands.
  /// <see cref="F:Microsoft.FSharp.Core.ValueOption`1.ValueNone"/> uses <see cref="P:Raylib_cs.Color.Black"/>.
  /// </summary>
  ClearColor: Color voption
}

/// <summary>Convenience values and functions for <see cref="T:Mibo.Elmish.Graphics2D.Renderer2DConfig"/>.</summary>
module Renderer2DConfig =

  /// <summary>
  /// Default configuration: no post-processing, black clear color.
  /// Suitable for most 2D games that don't need screen-space effects.
  /// </summary>
  let defaults: Renderer2DConfig = {
    PostProcess = ValueNone
    ClearColor = ValueNone
  }

/// <summary>
/// A deferred 2D renderer that sorts commands by layer and executes them
/// through the <see cref="T:Mibo.Elmish.Graphics2D.IRenderContext"/> state tracker.
/// </summary>
/// <remarks>
/// <para>
/// Commands are accumulated each frame via the <c>view</c> function into a
/// <see cref="T:Mibo.Elmish.Graphics2D.RenderBuffer2D"/>, sorted by
/// <see cref="P:Mibo.Elmish.Graphics2D.IRenderCommand2D.Layer"/>, then executed
/// in order. raylib handles internal draw-call batching automatically.
/// </para>
/// <para>
/// When <see cref="P:Mibo.Elmish.Graphics2D.Renderer2DConfig.PostProcess"/> is
/// configured, the scene renders to a <see cref="T:Raylib_cs.RenderTexture2D"/>
/// and each pass is applied sequentially via ping-pong render textures from the
/// <see cref="T:Mibo.Elmish.Graphics2D.IRenderTargetPool"/>.
/// </para>
/// <para>
/// Register via <c>Program.withRenderer</c>:
/// <code lang="fsharp">
/// Program.mkProgram init update view
/// |> Program.withRenderer(fun () -> Renderer2D.create view)
/// </code>
/// </para>
/// </remarks>
/// <typeparam name="Model">The application model type, passed to the view function.</typeparam>
type Renderer2D<'Model>
  (
    view: GameContext -> 'Model -> RenderBuffer2D -> unit,
    config: Renderer2DConfig
  ) =

  let buffer = RenderBuffer2D(capacity = 4096)
  let rtPool: IRenderTargetPool = new RenderTargetPool()

  let mutable _ctx: GameContext = Unchecked.defaultof<GameContext>
  let mutable _camera: Camera2D voption = ValueNone
  let mutable _shader: Shader voption = ValueNone

  let internalRenderContext =
    { new IRenderContext with
        member _.GameContext = _ctx
        member _.CurrentCamera = _camera

        member _.BeginCamera(c) =
          Rlgl.DrawRenderBatchActive()
          if _camera.IsSome then Raylib.EndMode2D()
          Raylib.BeginMode2D(c)
          _camera <- ValueSome c

        member _.EndCamera() =
          if _camera.IsSome then
            Rlgl.DrawRenderBatchActive()
            Raylib.EndMode2D()
            _camera <- ValueNone

        member _.BeginShader(s) =
          match _shader with
          | ValueSome cur when cur.Id = s.Id -> ()
          | _ ->
            Rlgl.DrawRenderBatchActive()

            if _shader.IsSome then
              Raylib.EndShaderMode()

            Raylib.BeginShaderMode(s)
            _shader <- ValueSome s

        member _.EndShader() =
          if _shader.IsSome then
            Rlgl.DrawRenderBatchActive()
            Raylib.EndShaderMode()
            _shader <- ValueNone

        member _.DrawImmediate(action) =
          Rlgl.DrawRenderBatchActive()
          let savedCam = _camera
          let savedShader = _shader

          if _shader.IsSome then
            Raylib.EndShaderMode()
            _shader <- ValueNone

          if _camera.IsSome then
            Raylib.EndMode2D()
            _camera <- ValueNone

          try
            action()
          finally
            match savedCam with
            | ValueSome c ->
              Raylib.BeginMode2D(c)
              _camera <- savedCam
            | ValueNone -> ()

            match savedShader with
            | ValueSome s ->
              Raylib.BeginShaderMode(s)
              _shader <- savedShader
            | ValueNone -> ()
    }

  let executeCommands() =
    for i = 0 to buffer.Count - 1 do
      buffer[i].Render(internalRenderContext)

    internalRenderContext.EndShader()
    internalRenderContext.EndCamera()

  let applyPostProcess
    (ctx: GameContext, sceneTarget: RenderTexture2D, passes: PostProcessPass[])
    =
    let mutable src = sceneTarget
    let w = ctx.WindowWidth
    let h = ctx.WindowHeight

    for i = 0 to passes.Length - 1 do
      let pass = passes[i]
      let isLast = i = passes.Length - 1

      let dst: RenderTexture2D voption =
        if isLast then
          ValueNone
        else
          ValueSome(rtPool.Acquire(w, h))

      match dst with
      | ValueSome target ->
        Raylib.BeginTextureMode(target)
        Raylib.ClearBackground(Color.Black)
      | ValueNone -> ()

      Raylib.BeginShaderMode(pass.Shader)

      match pass.OnSetup with
      | ValueSome f -> f pass.Shader ctx
      | ValueNone -> ()

      let sourceRect = Raylib_cs.Rectangle(0f, 0f, float32 w, float32 -h)

      let destRect = Raylib_cs.Rectangle(0f, 0f, float32 w, float32 h)

      Raylib.DrawTexturePro(
        src.Texture,
        sourceRect,
        destRect,
        Vector2.Zero,
        0f,
        Color.White
      )

      Raylib.EndShaderMode()

      match dst with
      | ValueSome target ->
        Raylib.EndTextureMode()
        src <- target
      | ValueNone -> ()

  interface IRenderer<'Model> with
    member _.Draw(ctx, model, _gameTime) =
      buffer.Clear()

      _ctx <- ctx

      view ctx model buffer
      buffer.Sort()

      match config.PostProcess with
      | ValueNone ->
        Raylib.ClearBackground(
          match config.ClearColor with
          | ValueSome c -> c
          | ValueNone -> Color.Black
        )

        executeCommands()
      | ValueSome passes ->
        let sceneRT = rtPool.Acquire(ctx.WindowWidth, ctx.WindowHeight)

        Raylib.BeginTextureMode(sceneRT)

        Raylib.ClearBackground(
          match config.ClearColor with
          | ValueSome c -> c
          | ValueNone -> Color.Black
        )

        executeCommands()
        Raylib.EndTextureMode()

        applyPostProcess(ctx, sceneRT, passes)
        rtPool.ReleaseAll()

  interface IDisposable with
    member _.Dispose() =
      (rtPool :?> System.IDisposable).Dispose()

/// <summary>Convenience constructors for <see cref="T:Mibo.Elmish.Graphics2D.Renderer2D`1"/>.</summary>
module Renderer2D =

  /// <summary>
  /// Creates a renderer with default configuration (no post-processing, black clear color).
  /// </summary>
  /// <param name="view">
  /// The view function that populates the render buffer each frame.
  /// Receives the game context, current model, and a mutable buffer.
  /// </param>
  let create
    (view: GameContext -> 'Model -> RenderBuffer2D -> unit)
    : IRenderer<'Model> =
    new Renderer2D<'Model>(view, Renderer2DConfig.defaults)
    :> IRenderer<'Model>

  /// <summary>
  /// Creates a renderer with the specified configuration.
  /// </summary>
  /// <param name="config">Renderer configuration including post-process passes and clear color.</param>
  /// <param name="view">
  /// The view function that populates the render buffer each frame.
  /// </param>
  let createWithConfig
    (config: Renderer2DConfig)
    (view: GameContext -> 'Model -> RenderBuffer2D -> unit)
    : IRenderer<'Model> =
    new Renderer2D<'Model>(view, config) :> IRenderer<'Model>
