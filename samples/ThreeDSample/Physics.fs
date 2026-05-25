module ThreeDSample.Physics

open System
open System.Collections.Generic
open System.Numerics
open Raylib_cs
open Mibo.Input
open Mibo.Layout3D
open ThreeDSample.Constants
open ThreeDSample.Types
open ThreeDSample.WorldGen

// ── Player bounds ──

let inline playerBottom (pos: Vector3) = pos.Y

let inline playerCenter (pos: Vector3) = pos + Vector3(0.0f, playerRadius, 0.0f)

// ── Camera-relative movement ──

let computeMoveDirection (actions: ActionState<GameAction>) (yaw: float32) =
  let forward = Vector3(-MathF.Sin(yaw), 0.0f, -MathF.Cos(yaw))
  let right = Vector3(MathF.Cos(yaw), 0.0f, -MathF.Sin(yaw))
  let mutable dir = Vector3.Zero

  if actions.Held.Contains(GameAction.MoveForward) then
    dir <- dir + forward

  if actions.Held.Contains(GameAction.MoveBackward) then
    dir <- dir - forward

  if actions.Held.Contains(GameAction.MoveRight) then
    dir <- dir + right

  if actions.Held.Contains(GameAction.MoveLeft) then
    dir <- dir - right

  if dir.LengthSquared() > 0.0f then
    Vector3.Normalize(dir)
  else
    Vector3.Zero

let computeCameraPosition (target: Vector3) (yaw: float32) (pitch: float32) =
  let dx = cameraDistance * MathF.Cos(pitch) * MathF.Sin(yaw)
  let dy = cameraDistance * MathF.Sin(pitch)
  let dz = cameraDistance * MathF.Cos(pitch) * MathF.Cos(yaw)
  target + Vector3(dx, dy, dz)

// ── Acceleration / Friction ──

let applyMovement (dt: float32) (moveDir: Vector3) (velocity: Vector3) =
  let horizontalVel = Vector3(velocity.X, 0.0f, velocity.Z)
  let hasInput = moveDir.LengthSquared() > 0.0f

  let newHorizontalVel =
    if hasInput then
      let targetVel = Vector3(moveDir.X * moveSpeed, 0.0f, moveDir.Z * moveSpeed)

      let diff = targetVel - horizontalVel
      let accel = acceleration * dt

      if diff.Length() <= accel then
        targetVel
      else
        horizontalVel + Vector3.Normalize(diff) * accel
    else
      let frictionAmount = friction * dt
      let speed = horizontalVel.Length()

      if speed <= frictionAmount then
        Vector3.Zero
      else
        horizontalVel * ((speed - frictionAmount) / speed)

  Vector3(newHorizontalVel.X, velocity.Y, newHorizontalVel.Z)

// ── AABB vs Sphere collision ──

let private aabbVsSphere
  (boxMin: Vector3)
  (boxMax: Vector3)
  (center: Vector3)
  (radius: float32)
  =
  let mutable dmin = 0.0f

  if center.X < boxMin.X then dmin <- dmin + (center.X - boxMin.X) * (center.X - boxMin.X)
  elif center.X > boxMax.X then dmin <- dmin + (center.X - boxMax.X) * (center.X - boxMax.X)

  if center.Y < boxMin.Y then dmin <- dmin + (center.Y - boxMin.Y) * (center.Y - boxMin.Y)
  elif center.Y > boxMax.Y then dmin <- dmin + (center.Y - boxMax.Y) * (center.Y - boxMax.Y)

  if center.Z < boxMin.Z then dmin <- dmin + (center.Z - boxMin.Z) * (center.Z - boxMin.Z)
  elif center.Z > boxMax.Z then dmin <- dmin + (center.Z - boxMax.Z) * (center.Z - boxMax.Z)

  dmin <= radius * radius

// ── Collision resolution ──

let resolveCollision
  (prevPos: Vector3)
  (newPos: Vector3)
  (velocity: Vector3)
  (chunks: Dictionary<struct (int * int), Chunk>)
  : struct (Vector3 * Vector3 * bool * int)
  =
  let mutable pos = newPos
  let mutable vel = velocity
  let mutable grounded = false
  let mutable scoreDelta = 0

  let playerBottomY = pos.Y
  let prevPlayerBottomY = prevPos.Y
  let sphereCenter = playerCenter pos

  let pcx = int(Math.Floor(float pos.X / float chunkWorldWidth))
  let pcz = int(Math.Floor(float pos.Z / float chunkWorldDepth))

  let bx = int(Math.Floor(float pos.X / float cellSize))

  let localX =
    bx - int(Math.Floor(float pos.X / float chunkWorldWidth)) * chunkWidth

  let by = int(Math.Floor(float playerBottomY / float cellSize))

  let bz = int(Math.Floor(float pos.Z / float cellSize))

  let localZ =
    bz - int(Math.Floor(float pos.Z / float chunkWorldDepth)) * chunkDepth

  for KeyValue(struct (cx, cz), chunk) in chunks do
    if abs (cx - pcx) <= 2 && abs (cz - pcz) <= 2 then
      let origin = chunk.Grid.Origin
      let blockOriginX = int origin.X
      let blockOriginZ = int origin.Z

      for dy in -1..2 do
        for dx in -1..1 do
          for dz in -1..1 do
            let gx = localX - (cx * chunkWidth - blockOriginX) + dx
            let gy = by + dy
            let gz = localZ - (cz * chunkDepth - blockOriginZ) + dz

            if
              gx >= 0 && gx < chunkWidth && gy >= 0 && gy < chunkHeight && gz >= 0
              && gz < chunkDepth
            then
              match CellGrid3D.get gx gy gz chunk.Grid with
              | ValueSome blockType when BlockType.isSolid blockType ->
                let worldX = origin.X + float32 gx * cellSize
                let worldY = origin.Y + float32 gy * cellSize
                let worldZ = origin.Z + float32 gz * cellSize

                let boxMin = Vector3(worldX, worldY, worldZ)
                let boxMax = Vector3(worldX + cellSize, worldY + cellSize, worldZ + cellSize)

                if aabbVsSphere boxMin boxMax sphereCenter playerRadius then
                  if not grounded && sphereCenter.Y >= boxMax.Y - playerRadius then
                    pos <- Vector3(pos.X, boxMax.Y, pos.Z)
                    vel <- Vector3(vel.X, 0.0f, vel.Z)
                    grounded <- true
                  elif vel.Y > 0.0f && sphereCenter.Y <= boxMin.Y + playerRadius then
                    let pushDown = sphereCenter.Y + playerRadius - boxMin.Y + 0.02f
                    pos <- Vector3(pos.X, pos.Y - pushDown, pos.Z)
                    vel <- Vector3(vel.X, 0.0f, vel.Z)
                  elif not grounded then
                    let mutable pushX = 0.0f

                    if sphereCenter.X < boxMin.X + boxMax.X * 0.5f then
                      pushX <- boxMin.X - (sphereCenter.X + playerRadius) - 0.01f
                    else
                      pushX <- boxMax.X - (sphereCenter.X - playerRadius) + 0.01f

                    let mutable pushZ = 0.0f

                    if sphereCenter.Z < boxMin.Z + boxMax.Z * 0.5f then
                      pushZ <- boxMin.Z - (sphereCenter.Z + playerRadius) - 0.01f
                    else
                      pushZ <- boxMax.Z - (sphereCenter.Z - playerRadius) + 0.01f

                    if abs pushX < abs pushZ then
                      pos <- Vector3(pos.X + pushX, pos.Y, pos.Z)
                      vel <- Vector3(float32 (sign (int vel.X)) * 2.0f, vel.Y, vel.Z)
                    else
                      pos <- Vector3(pos.X, pos.Y, pos.Z + pushZ)
                      vel <- Vector3(vel.X, vel.Y, float32 (sign (int vel.Z)) * 2.0f)

              | _ -> ()

  // Check collectibles
  for KeyValue(struct (cx, cz), chunk) in chunks do
    if abs (cx - pcx) <= 2 && abs (cz - pcz) <= 2 then
      let origin = chunk.Grid.Origin
      let blockOriginX = int origin.X
      let blockOriginZ = int origin.Z

      for dy in -1..2 do
        for dx in -1..1 do
          for dz in -1..1 do
            let gx = localX - (cx * chunkWidth - blockOriginX) + dx
            let gy = by + dy
            let gz = localZ - (cz * chunkDepth - blockOriginZ) + dz

            if
              gx >= 0 && gx < chunkWidth && gy >= 0 && gy < chunkHeight && gz >= 0
              && gz < chunkDepth
            then
              match CellGrid3D.get gx gy gz chunk.Grid with
              | ValueSome blockType when BlockType.isCollectible blockType ->
                let worldX = origin.X + float32 gx * cellSize + cellSize * 0.5f
                let worldY = origin.Y + float32 gy * cellSize + cellSize * 0.5f
                let worldZ = origin.Z + float32 gz * cellSize + cellSize * 0.5f

                let collectibleCenter = Vector3(worldX, worldY, worldZ)

                let distSq = (sphereCenter - collectibleCenter).LengthSquared()

                if distSq < (playerRadius + 0.5f) * (playerRadius + 0.5f) then
                  CellGrid3D.clear gx gy gz chunk.Grid |> ignore
                  scoreDelta <- scoreDelta + 1

              | _ -> ()

  struct (pos, vel, grounded, scoreDelta)
