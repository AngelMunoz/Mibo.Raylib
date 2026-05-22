namespace Mibo.Elmish.Graphics2D

open System
open System.Numerics
open Raylib_cs
open Mibo.Elmish

[<Measure>]
type RenderLayer

type LegacySpriteState = {
  Texture: Texture2D
  Dest: Raylib_cs.Rectangle
  Source: Raylib_cs.Rectangle
  Origin: Vector2
  Rotation: float32
  Color: Color
  Layer: int<RenderLayer>
}

type LegacyTextState = {
  Font: Font
  Text: string
  Position: Vector2
  FontSize: float32
  Spacing: float32
  Color: Color
  Layer: int<RenderLayer>
}

type Camera2DState = {
  Position: Vector2
  Zoom: float32
  Layer: int<RenderLayer>
}

type ShaderState = {
  Shader: Shader
  Layer: int<RenderLayer>
}

type PointLight2D = {
  Position: Vector2
  Color: Color
  Intensity: float32
  Radius: float32
  Falloff: float32
}

type DirectionalLight2D = {
  Direction: Vector2
  Color: Color
  Intensity: float32
}

type AmbientLight2D = { Color: Color }

type Occluder2D = { P1: Vector2; P2: Vector2 }

type PostProcessConfig = {
  Shader: Shader
  TintColor: Color
  TintAmount: float32
}

type RenderCmd2D =
  | DrawSprite of sprite: LegacySpriteState
  | DrawText of text: LegacyTextState
  | SetCamera2D of camera: Camera2DState
  | ResetCamera2D
  | SetShader of shader: ShaderState
  | ResetShader
  | DrawRect of
    rect: Raylib_cs.Rectangle *
    color: Color *
    layer: int<RenderLayer>
  | DrawLine of
    start: Vector2 *
    finish: Vector2 *
    color: Color *
    layer: int<RenderLayer>
  | SetLighting of ambient: AmbientLight2D
  | AddPointLight of light: PointLight2D
  | AddDirectionalLight of light: DirectionalLight2D
  | AddOccluder of occluder: Occluder2D
  | DrawSkyGradient of top: Color * bottom: Color * layer: int<RenderLayer>

type RenderBuffer<'Cmd> = Mibo.Elmish.RenderBuffer<int<RenderLayer>, 'Cmd>

module Lighting2D =
  let private colorToVec3(c: Color) =
    Vector3(float32 c.R / 255.0f, float32 c.G / 255.0f, float32 c.B / 255.0f)

  let private vec3ToColor(v: Vector3) =
    let clamp x =
      Math.Clamp(int(x * 255.0f), 0, 255) |> byte

    Color(clamp v.X, clamp v.Y, clamp v.Z, 255uy)

  let lerpColor (a: Color) (b: Color) (t: float32) : Color =
    let clamp01 x = Math.Clamp(x, 0.0f, 1.0f)
    let t = clamp01 t
    let r = byte(int(float32 a.R + t * (float32 b.R - float32 a.R)))
    let g = byte(int(float32 a.G + t * (float32 b.G - float32 a.G)))
    let bl = byte(int(float32 a.B + t * (float32 b.B - float32 a.B)))
    let al = byte(int(float32 a.A + t * (float32 b.A - float32 a.A)))
    Color(r, g, bl, al)

  let computeLightColor
    (ambient: AmbientLight2D option)
    (pointLights: PointLight2D list)
    (directionalLights: DirectionalLight2D list)
    (pos: Vector2)
    : Color =
    let mutable acc = Vector3.Zero

    match ambient with
    | Some a -> acc <- acc + colorToVec3 a.Color
    | None -> ()

    for light in pointLights do
      let dist = Vector2.Distance(pos, light.Position)

      if dist < light.Radius then
        let t = dist / light.Radius
        let atten = MathF.Pow(MathF.Max(0.0f, 1.0f - t), light.Falloff)
        acc <- acc + colorToVec3 light.Color * light.Intensity * atten

    for light in directionalLights do
      acc <- acc + colorToVec3 light.Color * light.Intensity

    vec3ToColor acc

  let mulColor (ca: Color) (cb: Color) : Color =
    let r = int ca.R * int cb.R / 255 |> byte
    let g = int ca.G * int cb.G / 255 |> byte
    let b = int ca.B * int cb.B / 255 |> byte
    let a = int ca.A * int cb.A / 255 |> byte
    Color(r, g, b, a)

type LegacyBatch2DRenderer<'Model>
  (
    view: GameContext -> 'Model -> RenderBuffer<RenderCmd2D> -> unit,
    postProcess: PostProcessConfig option
  ) =
  let buffer = RenderBuffer<RenderCmd2D>(capacity = 4096)
  let mutable renderTarget: RenderTexture2D option = None
  let mutable tintColorLoc = -1
  let mutable tintAmountLoc = -1

  let getOrCreateTarget (w: int) (h: int) =
    match renderTarget with
    | Some rt when rt.Texture.Width = w && rt.Texture.Height = h -> rt
    | _ ->
      match renderTarget with
      | Some old -> Raylib.UnloadRenderTexture(old)
      | _ -> ()

      let rt = Raylib.LoadRenderTexture(w, h)
      renderTarget <- Some rt
      rt

  let applyShaderUniforms(cfg: PostProcessConfig) =
    if tintColorLoc < 0 then
      tintColorLoc <- Raylib.GetShaderLocation(cfg.Shader, "tintColor")
      tintAmountLoc <- Raylib.GetShaderLocation(cfg.Shader, "tintAmount")

    let r = float32 cfg.TintColor.R / 255.0f
    let g = float32 cfg.TintColor.G / 255.0f
    let b = float32 cfg.TintColor.B / 255.0f
    let a = float32 cfg.TintColor.A / 255.0f

    let tintVec = Vector4(r, g, b, a)

    Raylib.SetShaderValue(
      cfg.Shader,
      tintColorLoc,
      tintVec,
      ShaderUniformDataType.Vec4
    )

    Raylib.SetShaderValue(
      cfg.Shader,
      tintAmountLoc,
      cfg.TintAmount,
      ShaderUniformDataType.Float
    )

  let executeCommands
    (ctx: GameContext)
    (ambient: AmbientLight2D option)
    (pointLights: PointLight2D list)
    (directionalLights: DirectionalLight2D list)
    (occluders: Occluder2D list)
    =
    let mutable inCamera = false
    let mutable inShader = false
    let mutable cameraState: Camera2DState option = None

    for i = 0 to buffer.Count - 1 do
      match buffer.Item(i) with
      | _, SetCamera2D cam ->
        let mutable c = Camera2D()
        c.Target <- cam.Position

        c.Offset <-
          Vector2(
            float32 ctx.WindowWidth / 2.0f,
            float32 ctx.WindowHeight / 2.0f
          )

        c.Rotation <- 0.0f
        c.Zoom <- cam.Zoom
        Raylib.BeginMode2D(c)
        inCamera <- true
        cameraState <- Some cam
      | _, ResetCamera2D ->
        if inCamera then
          Raylib.EndMode2D()
          inCamera <- false
      | _, SetShader s ->
        Raylib.BeginShaderMode(s.Shader)
        inShader <- true
      | _, ResetShader ->
        if inShader then
          Raylib.EndShaderMode()
          inShader <- false
      | _, DrawSprite sprite ->
        let spriteCenter =
          Vector2(
            sprite.Dest.X + sprite.Dest.Width / 2.0f,
            sprite.Dest.Y + sprite.Dest.Height / 2.0f
          )

        let lightColor =
          Lighting2D.computeLightColor
            ambient
            pointLights
            directionalLights
            spriteCenter

        let finalColor = Lighting2D.mulColor sprite.Color lightColor

        Raylib.DrawTexturePro(
          sprite.Texture,
          sprite.Source,
          sprite.Dest,
          sprite.Origin,
          sprite.Rotation,
          finalColor
        )
      | _, DrawText text ->
        Raylib.DrawTextEx(
          text.Font,
          text.Text,
          text.Position,
          text.FontSize,
          text.Spacing,
          text.Color
        )
      | _, DrawRect(rect, color, _) -> Raylib.DrawRectangleRec(rect, color)
      | _, DrawLine(start, finish, color, _) ->
        Raylib.DrawLineV(start, finish, color)
      | _, DrawSkyGradient(top, bottom, _) ->
        if inCamera then
          Raylib.EndMode2D()
          inCamera <- false

        Raylib.DrawRectangleGradientV(
          0,
          0,
          ctx.WindowWidth,
          ctx.WindowHeight,
          top,
          bottom
        )

        match cameraState with
        | Some cam ->
          let mutable c = Camera2D()
          c.Target <- cam.Position

          c.Offset <-
            Vector2(
              float32 ctx.WindowWidth / 2.0f,
              float32 ctx.WindowHeight / 2.0f
            )

          c.Rotation <- 0.0f
          c.Zoom <- cam.Zoom
          Raylib.BeginMode2D(c)
          inCamera <- true
        | None -> ()
      | _, SetLighting _
      | _, AddPointLight _
      | _, AddDirectionalLight _
      | _, AddOccluder _ -> ()

    // NOTE: raylib shape functions are intentionally not used here.
    // They fail to render inside BeginMode2D after DrawTexturePro calls
    // due to rlgl batch state conflicts. All primitives are rendered via
    // DrawSprite (DrawTexturePro) instead.

    if inShader then
      Raylib.EndShaderMode()

    if inCamera then
      Raylib.EndMode2D()

  interface IRenderer<'Model> with
    member _.Draw(ctx, model, gameTime) =
      buffer.Clear()
      view ctx model buffer
      buffer.Sort()

      let mutable ambient: AmbientLight2D option = None
      let pointLights = ResizeArray<PointLight2D>()
      let directionalLights = ResizeArray<DirectionalLight2D>()
      let occluders = ResizeArray<Occluder2D>()

      for i = 0 to buffer.Count - 1 do
        match buffer.Item(i) with
        | _, SetLighting a -> ambient <- Some a
        | _, AddPointLight l -> pointLights.Add(l)
        | _, AddDirectionalLight l -> directionalLights.Add(l)
        | _, AddOccluder o -> occluders.Add(o)
        | _ -> ()

      match postProcess with
      | None ->
        executeCommands
          ctx
          ambient
          (List.ofSeq pointLights)
          (List.ofSeq directionalLights)
          (List.ofSeq occluders)
      | Some cfg ->
        let target = getOrCreateTarget ctx.WindowWidth ctx.WindowHeight
        Raylib.BeginTextureMode(target)
        Raylib.ClearBackground(Color.Black)

        executeCommands
          ctx
          ambient
          (List.ofSeq pointLights)
          (List.ofSeq directionalLights)
          (List.ofSeq occluders)

        Raylib.EndTextureMode()

        applyShaderUniforms cfg
        Raylib.BeginShaderMode(cfg.Shader)

        let tw = float32 target.Texture.Width
        let th = float32 target.Texture.Height

        let sourceRect = Raylib_cs.Rectangle(0.0f, 0.0f, tw, -th)

        let destRect =
          Raylib_cs.Rectangle(
            0.0f,
            0.0f,
            float32 ctx.WindowWidth,
            float32 ctx.WindowHeight
          )

        Raylib.DrawTexturePro(
          target.Texture,
          sourceRect,
          destRect,
          Vector2.Zero,
          0.0f,
          Color.White
        )

        Raylib.EndShaderMode()

  interface IDisposable with
    member _.Dispose() =
      match renderTarget with
      | Some rt -> Raylib.UnloadRenderTexture(rt)
      | _ -> ()

module LegacyBatch2DRenderer =
  let create view =
    new LegacyBatch2DRenderer<'Model>(view, None) :> IRenderer<'Model>

  let createWithPostProcess postProcess view =
    new LegacyBatch2DRenderer<'Model>(view, Some postProcess) :> IRenderer<'Model>
