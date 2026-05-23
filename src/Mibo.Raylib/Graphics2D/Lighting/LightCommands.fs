namespace Mibo.Elmish.Graphics2D.Lighting

open Raylib_cs
open Mibo.Elmish.Graphics2D

// ═══════════════════════════════════════════════════════════════════
// Struct Commands
// ═══════════════════════════════════════════════════════════════════

/// <summary>No-op placeholder that reserves a layer slot for light ordering.</summary>
[<Struct>]
type NoopLightCommand(cmdLayer: int<RenderLayer>) =
  interface IRenderCommand2D with
    member _.Layer = cmdLayer
    member _.Render _ = ()

[<Struct>]
type LitSpriteCommand(lightCtx: LightContext2D, sprite: Command2D.SpriteState) =
  interface IRenderCommand2D with
    member _.Layer = sprite.Layer

    member _.Render ctx =
      if not lightCtx.ShaderActive then
        ctx.BeginShader(lightCtx.Shader)
        lightCtx.ShaderActive <- true

      if lightCtx.UniformsDirty then
        lightCtx.UploadUniforms()
        lightCtx.UniformsDirty <- false

      Raylib.DrawTexturePro(
        sprite.Texture,
        sprite.Source,
        sprite.Dest,
        sprite.Origin,
        sprite.Rotation,
        sprite.Color
      )

[<Struct>]
type EndLightingCommand(lightCtx: LightContext2D, cmdLayer: int<RenderLayer>) =
  interface IRenderCommand2D with
    member _.Layer = cmdLayer

    member _.Render ctx =
      if lightCtx.ShaderActive then
        ctx.EndShader()
        lightCtx.ShaderActive <- false
        lightCtx.UniformsDirty <- true

// ═══════════════════════════════════════════════════════════════════
// Command2D Factory Functions
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Factory functions for lighting-related render commands.
/// Each function mutates the <see cref="T:Mibo.Elmish.Graphics2D.Lighting.LightContext2D"/> immediately
/// (during view population) and returns an <see cref="T:Mibo.Elmish.Graphics2D.IRenderCommand2D"/>
/// for layer ordering.
/// </summary>
module LightCommands =

  /// <summary>
  /// Sets the ambient light color for the current frame.
  /// Mutates the light context immediately. The returned command is a layer placeholder.
  /// (lightCtx) can be bound first for partial application.
  /// </summary>
  let inline setAmbient
    (lightCtx: LightContext2D)
    (layer: int<RenderLayer>, ambient: AmbientLight2D)
    : IRenderCommand2D =
    lightCtx.Ambient <- ambient.Color
    NoopLightCommand(layer)

  /// <summary>
  /// Adds a point light for the current frame.
  /// Mutates the light context immediately. The returned command is a layer placeholder.
  /// </summary>
  let inline addPointLight
    (lightCtx: LightContext2D)
    (layer: int<RenderLayer>)
    (light: PointLight2D)
    : IRenderCommand2D =
    lightCtx.PointLights.Add(light)
    NoopLightCommand(layer)

  /// <summary>
  /// Adds a directional light for the current frame.
  /// Mutates the light context immediately. The returned command is a layer placeholder.
  /// </summary>
  let inline addDirectionalLight
    (lightCtx: LightContext2D)
    (layer: int<RenderLayer>)
    (light: DirectionalLight2D)
    : IRenderCommand2D =
    lightCtx.DirLights.Add(light)
    NoopLightCommand(layer)

  /// <summary>
  /// Adds an occluder segment for the current frame. Used by the shadow system.
  /// Mutates the light context immediately. The returned command is a layer placeholder.
  /// </summary>
  let inline addOccluder
    (lightCtx: LightContext2D)
    (layer: int<RenderLayer>)
    (occluder: Occluder2D)
    : IRenderCommand2D =
    lightCtx.Occluders.Add(occluder)
    NoopLightCommand(layer)

  /// <summary>
  /// Draws a sprite with the current lighting state from the given light context.
  /// Activates the lit shader and uploads light uniforms on first call each frame.
  /// </summary>
  let inline litSprite
    (lightCtx: LightContext2D)
    (sprite: Command2D.SpriteState)
    : IRenderCommand2D =
    LitSpriteCommand(lightCtx, sprite)

  /// <summary>
  /// Ends the current lighting pass. Deactivates the lit shader.
  /// Sprites after this point are unlit. Call again to re-enable lighting.
  /// </summary>
  let inline endLighting
    (lightCtx: LightContext2D)
    (layer: int<RenderLayer>)
    : IRenderCommand2D =
    EndLightingCommand(lightCtx, layer)

// ═══════════════════════════════════════════════════════════════════
// Draw Module Wrappers (buffer-returning, pipe-friendly)
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Pipe-friendly lighting draw functions. Each takes a
/// <see cref="T:Mibo.Elmish.Graphics2D.RenderBuffer2D"/> as the last argument,
/// adds the command, and returns the buffer for chaining.
/// </summary>
module LightDraw =

  let inline setAmbient
    (lightCtx: LightContext2D)
    (layer: int<RenderLayer>, ambient: AmbientLight2D)
    (buffer: RenderBuffer2D)
    =
    buffer.Add(LightCommands.setAmbient lightCtx (layer, ambient))
    buffer

  let inline addPointLight
    (lightCtx: LightContext2D)
    (layer: int<RenderLayer>)
    (light: PointLight2D)
    (buffer: RenderBuffer2D)
    =
    buffer.Add(LightCommands.addPointLight lightCtx layer light)
    buffer

  let inline addDirectionalLight
    (lightCtx: LightContext2D)
    (layer: int<RenderLayer>)
    (light: DirectionalLight2D)
    (buffer: RenderBuffer2D)
    =
    buffer.Add(LightCommands.addDirectionalLight lightCtx layer light)
    buffer

  let inline addOccluder
    (lightCtx: LightContext2D)
    (layer: int<RenderLayer>)
    (occluder: Occluder2D)
    (buffer: RenderBuffer2D)
    =
    buffer.Add(LightCommands.addOccluder lightCtx layer occluder)
    buffer

  let inline litSprite
    (lightCtx: LightContext2D)
    (sprite: Command2D.SpriteState)
    (buffer: RenderBuffer2D)
    =
    buffer.Add(LightCommands.litSprite lightCtx sprite)
    buffer

  let inline endLighting
    (lightCtx: LightContext2D)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer2D)
    =
    buffer.Add(LightCommands.endLighting lightCtx layer)
    buffer
