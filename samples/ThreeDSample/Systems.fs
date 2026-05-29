module ThreeDSample.Systems

open System
open System.Numerics
open Raylib_cs
open Mibo.Elmish
open Mibo.Input
open ThreeDSample.Constants
open ThreeDSample.Types
open ThreeDSample.Physics
open ThreeDSample.WorldGen

let inputSystem
  (dt: float32)
  (model: GameModel)
  : struct (GameModel * Cmd<Msg>) =
  let mutable yaw = model.CameraYaw
  let mutable pitch = model.CameraPitch

  if model.Actions.Held.Contains(GameAction.RotateCameraLeft) then
    yaw <- yaw - 2.0f * dt

  if model.Actions.Held.Contains(GameAction.RotateCameraRight) then
    yaw <- yaw + 2.0f * dt

  if model.Actions.Held.Contains(GameAction.RotateCameraUp) then
    pitch <- pitch + 1.5f * dt

  if model.Actions.Held.Contains(GameAction.RotateCameraDown) then
    pitch <- pitch - 1.5f * dt

  model.CameraYaw <- yaw
  model.CameraPitch <- Math.Clamp(pitch, -0.5f, 1.3f)
  struct (model, Cmd.none)

let physicsSystem
  (dt: float32)
  (model: GameModel)
  : struct (GameModel * Cmd<Msg>) =
  let moveDir = computeMoveDirection model.Actions model.CameraYaw

  let vel =
    if model.IsGrounded && model.Actions.Started.Contains(GameAction.Jump) then
      Vector3(model.PlayerVelocity.X, jumpSpeed, model.PlayerVelocity.Z)
    else
      model.PlayerVelocity

  let vel = Vector3(vel.X, vel.Y + gravity * dt, vel.Z)
  let vel = applyMovement dt moveDir vel

  let prevPos = model.PlayerPosition
  let newPos = prevPos + vel * dt

  let struct (finalPos, finalVel, grounded, scoreDelta) =
    resolveCollision prevPos newPos vel model.Chunks

  let mutable finalPos = finalPos
  let mutable finalVel = finalVel
  let mutable grounded = grounded

  model.Score <- model.Score + scoreDelta

  if finalPos.Y < fallLimit then
    finalPos <- spawnPosition
    finalVel <- Vector3.Zero
    grounded <- false

  if model.Actions.Started.Contains(GameAction.Respawn) then
    finalPos <- spawnPosition
    finalVel <- Vector3.Zero
    grounded <- false

  model.PlayerPosition <- finalPos
  model.PlayerVelocity <- finalVel
  model.IsGrounded <- grounded

  if moveDir.LengthSquared() > 0.1f then
    model.PlayerFacing <- MathF.Atan2(moveDir.X, moveDir.Z)

  let target = finalPos + Vector3(0.0f, playerHeight * 0.5f, 0.0f)

  let desiredCamPos =
    computeCameraPosition target model.CameraYaw model.CameraPitch

  let lerpFactor = 1.0f - MathF.Exp(-dt * cameraLerpSpeed)

  model.CameraPosition <-
    Vector3.Lerp(model.CameraPosition, desiredCamPos, lerpFactor)

  model.CameraTarget <- Vector3.Lerp(model.CameraTarget, target, lerpFactor)

  struct (model, Cmd.none)

let chunkSystem
  (dt: float32)
  (model: GameModel)
  : struct (GameModel * Cmd<Msg>) =
  loadChunks model.PlayerPosition model.Chunks model.Seed
  evictDistantChunks model.PlayerPosition model.Chunks model.KeysToRemove
  struct (model, Cmd.none)

let dayNightSystem
  (dt: float32)
  (model: GameModel)
  : struct (GameModel * Cmd<Msg>) =
  let newTime =
    (model.DayNightTimeOfDay + dt * (24.0f / model.DayNightDuration)) % 24.0f

  model.DayNightTimeOfDay <- newTime
  model.TotalTime <- model.TotalTime + dt
  struct (model, Cmd.none)

let update (msg: Msg) (model: GameModel) : struct (GameModel * Cmd<Msg>) =
  match msg with
  | InputMapped actions ->
    model.Actions <- actions
    struct (model, Cmd.none)
  | Tick gt ->
    let dt = float32 gt.ElapsedGameTime.TotalSeconds

    System.start model
    |> System.pipeMutable(inputSystem dt)
    |> System.pipeMutable(physicsSystem dt)
    |> System.pipeMutable(chunkSystem dt)
    |> System.pipeMutable(dayNightSystem dt)
    |> System.finish id
