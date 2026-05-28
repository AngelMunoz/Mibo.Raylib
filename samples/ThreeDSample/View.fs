module ThreeDSample.View

open System
open System.Collections.Generic
open System.Numerics
open Raylib_cs
open Mibo.Elmish
open Mibo.Elmish.Graphics3D
open Mibo.Layout3D
open ThreeDSample.Constants
open ThreeDSample.Types
open ThreeDSample.DayNight

let loadOrGetModel (cache: Dictionary<string, Model>) (path: string) (ctx: GameContext) =
  if path = "" then Unchecked.defaultof<Model>
  else
    match cache.TryGetValue path with
    | true, m -> m
    | false, _ ->
      let assets = GameContext.getService<IAssets> ctx
      let m = assets.Model(path)
      cache[path] <- m
      m

let view (ctx: GameContext) (model: GameModel) (buffer: RenderBuffer3D) =
  let time = 12.0f // DEBUG: fixed noon
  let skyColor = DayNight.getSkyColor time

  buffer.Add(Command3D.drawImmediate(fun () -> Raylib.ClearBackground(skyColor)))

  let camera =
    let mutable c = Camera3D()
    c.Position <- model.CameraPosition
    c.Target <- model.CameraTarget
    c.Up <- Vector3.UnitY
    c.FovY <- 55.0f
    c.Projection <- CameraProjection.Perspective
    c

  let ambient = { Color = DayNight.getAmbientColor time; Intensity = 0.6f }

  // DEBUG: fixed light pointing straight down
  let lightDir = Vector3(0.0f, -1.0f, 0.0f)

  buffer
  |> Draw3D.beginCamera camera
  |> Draw3D.setAmbientLight ambient
  |> Draw3D.addDirectionalLight {
    Direction = lightDir
    Color = Color.White
    Intensity = 1.5f
    CastsShadows = true
  }
  |> ignore

  let mutable drawCount = 0
  let maxRenderDistSq = 900.0f  // 30^2
  let camPos = model.CameraPosition

  for KeyValue(struct (cx, cz), chunk) in model.Chunks do
    CellGridRenderer3D.render
      chunk.Grid
      (fun worldPos blockType ->
        if blockType <> BlockType.Empty then
          let distSq = (worldPos - camPos).LengthSquared()

          if distSq <= maxRenderDistSq then
            let path = BlockType.modelPath blockType

            if path <> "" then
              let blockModel = loadOrGetModel model.ModelCache path ctx
              let rotAngle = BlockType.modelRotation blockType * MathF.PI / 180.0f
              let yOff = BlockType.modelVerticalOffset blockType

              let transform =
                if rotAngle = 0.0f && yOff = 0.0f then
                  Raymath.MatrixTranslate(worldPos.X, worldPos.Y, worldPos.Z)
                elif rotAngle = 0.0f then
                  Raymath.MatrixTranslate(worldPos.X, worldPos.Y + yOff, worldPos.Z)
                else
                  let rot = Raymath.MatrixRotateY(rotAngle)
                  let trans = Raymath.MatrixTranslate(worldPos.X, worldPos.Y + yOff, worldPos.Z)
                  Raymath.MatrixMultiply(rot, trans)

              buffer.Add(Command3D.drawModel blockModel transform) |> ignore

              if blockType = BlockType.MushroomLight then
                buffer.Add(
                  Command3D.addPointLight {
                    Position = worldPos + Vector3(0.0f, 0.5f, 0.0f)
                    Color = Color(255uy, 200uy, 120uy)
                    Radius = 6.0f
                    CastsShadows = true
                    ShadowBias = ValueNone
                  }
                ) |> ignore

              drawCount <- drawCount + 1)

  // Player model
  let playerModel = loadOrGetModel model.ModelCache KenneyModels.characterOobi ctx

  let playerTransform =
    let rot = Raymath.MatrixRotateY(model.PlayerFacing)
    let trans = Raymath.MatrixTranslate(model.PlayerPosition.X, model.PlayerPosition.Y, model.PlayerPosition.Z)
    Raymath.MatrixMultiply(rot, trans)

  buffer.Add(Command3D.drawModel playerModel playerTransform) |> ignore

  buffer.Add(Command3D.drawGrid 20 1.0f) |> ignore
  buffer |> Draw3D.endCamera |> ignore

  buffer.Add(
    Command3D.drawImmediate(fun () ->
      Raylib.DrawText(
        $"FPS: {Raylib.GetFPS()}  Models: {drawCount}  Chunks: {model.Chunks.Count}  Score: {model.Score}",
        10, 10, 20, Color.Yellow
      )

      Raylib.DrawText(
        $"Time: {model.DayNightTimeOfDay:F1}h  Pos: ({model.PlayerPosition.X:F0},{model.PlayerPosition.Y:F0},{model.PlayerPosition.Z:F0})  Grounded: {model.IsGrounded}",
        10, 35, 20, Color.Yellow
      )
    )
  ) |> ignore

  Draw3D.drop buffer
