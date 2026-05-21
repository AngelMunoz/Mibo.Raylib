namespace Mibo.Elmish.Graphics3D

open System
open System.Numerics
open Raylib_cs
open Mibo.Elmish
open Mibo.Elmish.Graphics2D

[<Measure>]
type RenderLayer3D

type AmbientLight3D = { Color: Color; Intensity: float32 }

type DirectionalLight3D = {
  Direction: Vector3
  Color: Color
  Intensity: float32
}

type PointLight3D = {
  Position: Vector3
  Color: Color
  Radius: float32
}

type RenderCmd3D =
  | SetCamera3D of camera: Camera3D
  | ResetCamera3D
  | DrawCube of
    position: Vector3 *
    width: float32 *
    height: float32 *
    length: float32 *
    color: Color
  | DrawSphere of position: Vector3 * radius: float32 * color: Color
  | DrawGrid of slices: int * spacing: float32
  | DrawModel of model: Model * position: Vector3 * scale: float32 * tint: Color
  | DrawModelEx of
    model: Model *
    position: Vector3 *
    rotationAxis: Vector3 *
    rotationAngle: float32 *
    scale: Vector3 *
    tint: Color
  | DrawLine3D of start: Vector3 * finish: Vector3 * color: Color
  | SetShader3D of shader: Shader
  | ResetShader3D
  | SetBackground3D of color: Color
  | SetAmbient3D of ambient: AmbientLight3D
  | AddDirectionalLight3D of light: DirectionalLight3D
  | AddPointLight3D of light: PointLight3D

type RenderBuffer3D = Mibo.Elmish.RenderBuffer<int<RenderLayer3D>, RenderCmd3D>

module ModelHelper =
  open FSharp.NativeInterop

  /// Replace the first material's shader on a loaded Model.
  /// Required because DrawModel overrides BeginShaderMode with the
  /// model's own material shader.
  let setMaterialShader (model: Model) (shader: Shader) =
    if model.MaterialCount > 0 then
      let matPtr = model.Materials
      let mutable mat = NativePtr.read matPtr
      mat.Shader <- shader
      NativePtr.write matPtr mat

type Batch3DRenderer<'Model>
  (
    view: GameContext -> 'Model -> RenderBuffer3D -> unit,
    postProcess: PostProcessConfig option
  ) =
  let buffer = RenderBuffer3D(capacity = 4096)
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

  let colorToVec3 (c: Color) =
    Vector3(
      float32 c.R / 255.0f,
      float32 c.G / 255.0f,
      float32 c.B / 255.0f
    )

  let uploadLights
    (shader: Shader)
    (ambient: AmbientLight3D option)
    (directionalLights: DirectionalLight3D list)
    (pointLights: PointLight3D list)
    =
    match ambient with
    | Some a ->
      let loc = Raylib.GetShaderLocation(shader, "ambientColor")

      if loc >= 0 then
        Raylib.SetShaderValue(
          shader,
          loc,
          colorToVec3 a.Color,
          ShaderUniformDataType.Vec3
        )

      let loc2 = Raylib.GetShaderLocation(shader, "ambientIntensity")

      if loc2 >= 0 then
        Raylib.SetShaderValue(
          shader,
          loc2,
          a.Intensity,
          ShaderUniformDataType.Float
        )
    | None -> ()

    match directionalLights with
    | [ d ] ->
      let locDir = Raylib.GetShaderLocation(shader, "dirLightDir")

      if locDir >= 0 then
        Raylib.SetShaderValue(
          shader,
          locDir,
          d.Direction,
          ShaderUniformDataType.Vec3
        )

      let locCol = Raylib.GetShaderLocation(shader, "dirLightColor")

      if locCol >= 0 then
        Raylib.SetShaderValue(
          shader,
          locCol,
          colorToVec3 d.Color,
          ShaderUniformDataType.Vec3
        )

      let locInt = Raylib.GetShaderLocation(shader, "dirLightIntensity")

      if locInt >= 0 then
        Raylib.SetShaderValue(
          shader,
          locInt,
          d.Intensity,
          ShaderUniformDataType.Float
        )
    | _ -> ()

    let count = Math.Min(pointLights.Length, 4)

    let locCount = Raylib.GetShaderLocation(shader, "pointLightCount")

    if locCount >= 0 then
      Raylib.SetShaderValue(
        shader,
        locCount,
        count,
        ShaderUniformDataType.Int
      )

    for i = 0 to count - 1 do
      let pl = pointLights[i]
      let posLoc = Raylib.GetShaderLocation(shader, $"pointLightPos{i}")
      let colLoc = Raylib.GetShaderLocation(shader, $"pointLightColor{i}")
      let radLoc = Raylib.GetShaderLocation(shader, $"pointLightRadius{i}")

      if posLoc >= 0 then
        Raylib.SetShaderValue(
          shader,
          posLoc,
          pl.Position,
          ShaderUniformDataType.Vec3
        )

      if colLoc >= 0 then
        Raylib.SetShaderValue(
          shader,
          colLoc,
          colorToVec3 pl.Color,
          ShaderUniformDataType.Vec3
        )

      if radLoc >= 0 then
        Raylib.SetShaderValue(
          shader,
          radLoc,
          pl.Radius,
          ShaderUniformDataType.Float
        )

  let executeCommands
    (ctx: GameContext)
    (ambient: AmbientLight3D option)
    (directionalLights: DirectionalLight3D list)
    (pointLights: PointLight3D list)
    =
    let mutable inCamera = false
    let mutable inShader = false

    for i = 0 to buffer.Count - 1 do
      match buffer.Item(i) with
      | _, SetCamera3D cam ->
        Raylib.BeginMode3D(cam)
        inCamera <- true
      | _, ResetCamera3D ->
        if inCamera then
          Raylib.EndMode3D()
          inCamera <- false
      | _, SetShader3D s ->
        Raylib.BeginShaderMode(s)
        inShader <- true
        uploadLights s ambient directionalLights pointLights
      | _, ResetShader3D ->
        if inShader then
          Raylib.EndShaderMode()
          inShader <- false
      | _, DrawCube(pos, w, h, l, color) ->
        Raylib.DrawCube(pos, w, h, l, color)
      | _, DrawSphere(pos, r, color) ->
        Raylib.DrawSphere(pos, r, color)
      | _, DrawGrid(slices, spacing) ->
        Raylib.DrawGrid(slices, spacing)
      | _, DrawModel(model, pos, scale, tint) ->
        Raylib.DrawModel(model, pos, scale, tint)
      | _, DrawModelEx(model, pos, axis, angle, scale, tint) ->
        Raylib.DrawModelEx(model, pos, axis, angle, scale, tint)
      | _, DrawLine3D(start, finish, color) ->
        Raylib.DrawLine3D(start, finish, color)
      | _, SetBackground3D color ->
        Raylib.ClearBackground(color)
      | _, SetAmbient3D _
      | _, AddDirectionalLight3D _
      | _, AddPointLight3D _ ->
        ()

    if inShader then
      Raylib.EndShaderMode()

    if inCamera then
      Raylib.EndMode3D()

  interface IRenderer<'Model> with
    member _.Draw(ctx, model, gameTime) =
      buffer.Clear()
      view ctx model buffer
      buffer.Sort()

      let mutable ambient: AmbientLight3D option = None
      let mutable backgroundColor = Color.Black
      let directionalLights = ResizeArray<DirectionalLight3D>()
      let pointLights = ResizeArray<PointLight3D>()

      for i = 0 to buffer.Count - 1 do
        match buffer.Item(i) with
        | _, SetAmbient3D a -> ambient <- Some a
        | _, SetBackground3D c -> backgroundColor <- c
        | _, AddDirectionalLight3D l -> directionalLights.Add(l)
        | _, AddPointLight3D l -> pointLights.Add(l)
        | _ -> ()

      match postProcess with
      | None ->
        Raylib.ClearBackground(backgroundColor)

        executeCommands
          ctx
          ambient
          (List.ofSeq directionalLights)
          (List.ofSeq pointLights)
      | Some cfg ->
        let target = getOrCreateTarget ctx.WindowWidth ctx.WindowHeight
        Raylib.BeginTextureMode(target)
        Raylib.ClearBackground(backgroundColor)

        executeCommands
          ctx
          ambient
          (List.ofSeq directionalLights)
          (List.ofSeq pointLights)

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

module Batch3DRenderer =
  let create view =
    new Batch3DRenderer<'Model>(view, None) :> IRenderer<'Model>

  let createWithPostProcess postProcess view =
    new Batch3DRenderer<'Model>(view, Some postProcess) :> IRenderer<'Model>
