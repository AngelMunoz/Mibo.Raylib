module ThreeDSample.View

open System
open System.Collections.Generic
open System.Numerics
open FSharp.NativeInterop
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

  let camPos = model.CameraPosition
  let maxChunkDistSq = 2500.0f // 50^2 — chunk-level distance cull
  let mutable mushroomLightCount = 0
  let maxMushroomLights = 8 // cap shadow-casting mushroom lights

  // Build frustum from camera VP for chunk-level culling
  let viewMatrix = Raymath.MatrixLookAt(camPos, model.CameraTarget, Vector3.UnitY)
  let projMatrix = Raymath.MatrixPerspective(
    float(55.0f * MathF.PI / 180.0f),
    float(1280.0f / 720.0f),
    0.1, 200.0
  )
  let frustum = Frustum(Raymath.MatrixMultiply(viewMatrix, projMatrix))

  for KeyValue(struct (cx, cz), chunk) in model.Chunks do
    // Chunk-level distance cull
    let chunkCenter = Vector3(
      (chunk.Bounds.Min.X + chunk.Bounds.Max.X) * 0.5f,
      (chunk.Bounds.Min.Y + chunk.Bounds.Max.Y) * 0.5f,
      (chunk.Bounds.Min.Z + chunk.Bounds.Max.Z) * 0.5f
    )

    let chunkDistSq = (chunkCenter - camPos).LengthSquared()

    if chunkDistSq > maxChunkDistSq then
      () // skip distant chunks entirely
    else
      // Use instanced rendering: group blocks by model path
      let groups = Dictionary<string, ResizeArray<struct (Matrix4x4 * BlockType)>>()
      let layoutBounds = {
        Mibo.Layout3D.BoundingBox.Min = chunk.Bounds.Min
        Mibo.Layout3D.BoundingBox.Max = chunk.Bounds.Max
      }

      CellGridRenderer3D.renderVolume layoutBounds chunk.Grid (fun worldPos blockType ->
        if blockType <> BlockType.Empty then
          let path = BlockType.modelPath blockType

          if path <> "" then
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

            match groups.TryGetValue path with
            | true, list -> list.Add(struct (transform, blockType))
            | false, _ ->
              let list = ResizeArray<struct (Matrix4x4 * BlockType)>()
              list.Add(struct (transform, blockType))
              groups[path] <- list

          // Collect mushroom lights separately (can't instance lights)
          if blockType = BlockType.MushroomLight && mushroomLightCount < maxMushroomLights then
            let lightDistSq = (worldPos - camPos).LengthSquared()

            if lightDistSq <= 1600.0f then // 40^2 for lights
              mushroomLightCount <- mushroomLightCount + 1

              buffer.Add(
                Command3D.addPointLight {
                  Position = worldPos + Vector3(0.0f, 0.5f, 0.0f)
                  Color = Color(255uy, 200uy, 120uy)
                  Radius = 6.0f
                  CastsShadows = false
                  ShadowBias = ValueNone
                }
              ) |> ignore)

      // Emit one instanced draw per block type per chunk
      for KeyValue(path, entries) in groups do
        if entries.Count > 0 then
          let blockModel = loadOrGetModel model.ModelCache path ctx

          if blockModel.MeshCount > 0 then
            let transforms = Array.zeroCreate<Matrix4x4> entries.Count

            for i = 0 to entries.Count - 1 do
              let struct (t, _) = entries[i]
              transforms[i] <- t

            // Use the first sub-mesh for instanced draw
            let mesh = NativePtr.get blockModel.Meshes 0
            let matIdx = NativePtr.get blockModel.MeshMaterial 0
            let raylibMat = NativePtr.get blockModel.Materials matIdx
            let mat3d = Material3D.fromRaylibMaterial raylibMat

            buffer.Add(Command3D.drawMeshInstanced mesh transforms mat3d transforms.Length) |> ignore

      groups.Clear()

  // Player model
  let playerModel = loadOrGetModel model.ModelCache KenneyModels.characterOobi ctx

  let playerTransform =
    let rot = Raymath.MatrixRotateY(model.PlayerFacing)
    let trans = Raymath.MatrixTranslate(model.PlayerPosition.X, model.PlayerPosition.Y, model.PlayerPosition.Z)
    Raymath.MatrixMultiply(rot, trans)

  buffer.Add(Command3D.drawModel playerModel playerTransform) |> ignore

  buffer |> Draw3D.endCamera |> ignore

  buffer.Add(
    Command3D.drawImmediate(fun () ->
      Raylib.DrawText(
        $"FPS: {Raylib.GetFPS()}  Chunks: {model.Chunks.Count}  Score: {model.Score}",
        10, 10, 20, Color.Yellow
      )

      Raylib.DrawText(
        $"Time: {model.DayNightTimeOfDay:F1}h  Pos: ({model.PlayerPosition.X:F0},{model.PlayerPosition.Y:F0},{model.PlayerPosition.Z:F0})  Grounded: {model.IsGrounded}",
        10, 35, 20, Color.Yellow
      )
    )
  ) |> ignore

  Draw3D.drop buffer
