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

let loadOrGetModel
  (cache: Dictionary<string, Model>)
  (path: string)
  (ctx: GameContext)
  =
  if path = "" then
    Unchecked.defaultof<Model>
  else
    match cache.TryGetValue path with
    | true, m -> m
    | false, _ ->
      let assets = GameContext.getService<IAssets> ctx
      let m = assets.Model(path)
      cache[path] <- m
      m

// Persistent mesh/material cache keyed by model path.
let private meshMaterialCache =
  Dictionary<string, struct (Raylib_cs.Mesh * Material3D)[]>()

// Per-frame mutable context set once before rendering.
let mutable private currentModelCache =
  Unchecked.defaultof<Dictionary<string, Model>>

let mutable private currentGameContext = Unchecked.defaultof<GameContext>

let private resolveMeshesAndMaterial(blockType: BlockType) =
  let path = BlockType.modelPath blockType

  match meshMaterialCache.TryGetValue path with
  | true, cached -> cached
  | false, _ ->
    let m = loadOrGetModel currentModelCache path currentGameContext

    let result =
      if m.MeshCount > 0 then
        [|
          for mi = 0 to m.MeshCount - 1 do
            #nowarn 9
            let mesh = NativePtr.get m.Meshes mi
            let matIdx = NativePtr.get m.MeshMaterial mi
            let raylibMat = NativePtr.get m.Materials matIdx
            #warnon 9
            struct (mesh, Material3D.fromRaylibMaterial raylibMat)
        |]
      else
        Array.empty

    meshMaterialCache[path] <- result
    result

// Persistent context — allocated once, reused every frame.
let private instancedCtx =
  InstancedRenderContext<BlockType, string>(
    getKey = BlockType.modelPath,
    getMeshesAndMaterial = resolveMeshesAndMaterial,
    getTransform =
      fun worldPos blockType ->
        let rotAngle = BlockType.modelRotation blockType * MathF.PI / 180.0f
        let yOff = BlockType.modelVerticalOffset blockType

        if rotAngle = 0.0f && yOff = 0.0f then
          Raymath.MatrixTranslate(worldPos.X, worldPos.Y, worldPos.Z)
        elif rotAngle = 0.0f then
          Raymath.MatrixTranslate(worldPos.X, worldPos.Y + yOff, worldPos.Z)
        else
          let rot = Raymath.MatrixRotateY(rotAngle)

          let trans =
            Raymath.MatrixTranslate(worldPos.X, worldPos.Y + yOff, worldPos.Z)

          Raymath.MatrixMultiply(rot, trans)
  )

let view (ctx: GameContext) (model: GameModel) (buffer: RenderBuffer3D) =
  let time = 12.0f // DEBUG: fixed noon
  let skyColor = DayNight.getSkyColor time

  buffer.Add(
    Command3D.drawImmediate(fun () -> Raylib.ClearBackground(skyColor))
  )

  let camera =
    Camera3D(
      model.CameraPosition,
      model.CameraTarget,
      Vector3.UnitY,
      55.0f,
      CameraProjection.Perspective
    )

  let ambient = {
    Color = DayNight.getAmbientColor time
    Intensity = 0.6f
  }

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

  currentModelCache <- model.ModelCache
  currentGameContext <- ctx
  instancedCtx.ResetFrameBuffers()

  let camPos = model.CameraPosition
  let maxChunkDistSq = 2500.0f
  let mutable mushroomLightCount = 0
  let maxMushroomLights = 8

  for KeyValue(struct (cx, cz), chunk) in model.Chunks do
    let chunkCenter =
      Vector3(
        (chunk.Bounds.Min.X + chunk.Bounds.Max.X) * 0.5f,
        (chunk.Bounds.Min.Y + chunk.Bounds.Max.Y) * 0.5f,
        (chunk.Bounds.Min.Z + chunk.Bounds.Max.Z) * 0.5f
      )

    if (chunkCenter - camPos).LengthSquared() <= maxChunkDistSq then
      let layoutBounds = {
        Mibo.Layout3D.BoundingBox.Min = chunk.Bounds.Min
        Mibo.Layout3D.BoundingBox.Max = chunk.Bounds.Max
      }

      // Instanced block rendering via library
      CellGridRenderer3D.renderVolumeInstanced
        instancedCtx
        layoutBounds
        chunk.Grid
        buffer

      // Mushroom lights — collected in the same chunk loop
      CellGridRenderer3D.renderVolume
        layoutBounds
        chunk.Grid
        (fun worldPos blockType ->
          if
            blockType = BlockType.MushroomLight
            && mushroomLightCount < maxMushroomLights
            && (worldPos - camPos).LengthSquared() <= 1600.0f
          then
            mushroomLightCount <- mushroomLightCount + 1

            buffer.Add(
              Command3D.addPointLight {
                Position = worldPos + Vector3(0.0f, 0.5f, 0.0f)
                Color = Color(255uy, 200uy, 120uy)
                Radius = 6.0f
                CastsShadows = false
                ShadowBias = ValueNone
              }
            )
            |> ignore)

  // Player model
  let playerModel =
    loadOrGetModel model.ModelCache KenneyModels.characterOobi ctx

  let playerTransform =
    let rot = Raymath.MatrixRotateY(model.PlayerFacing)

    let trans =
      Raymath.MatrixTranslate(
        model.PlayerPosition.X,
        model.PlayerPosition.Y,
        model.PlayerPosition.Z
      )

    Raymath.MatrixMultiply(rot, trans)

  buffer.Add(Command3D.drawModel playerModel playerTransform) |> ignore

  buffer |> Draw3D.endCamera |> Draw3D.drop

  buffer.Add(
    Command3D.drawImmediate(fun () ->
      Raylib.DrawText(
        $"FPS: {Raylib.GetFPS()}  Chunks: {model.Chunks.Count}  Score: {model.Score}",
        10,
        10,
        20,
        Color.Yellow
      )

      Raylib.DrawText(
        $"Time: {model.DayNightTimeOfDay:F1}h  Pos: ({model.PlayerPosition.X:F0},{model.PlayerPosition.Y:F0},{model.PlayerPosition.Z:F0})  Grounded: {model.IsGrounded}",
        10,
        35,
        20,
        Color.Yellow
      ))
  )
  |> ignore

  Draw3D.drop buffer
