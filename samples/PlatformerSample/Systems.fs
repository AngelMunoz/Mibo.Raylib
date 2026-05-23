module PlatformerSample.Systems

open System
open System.Collections.Generic
open System.Numerics
open Raylib_cs
open Mibo.Elmish
open Mibo.Elmish.Graphics2D
open Mibo.Elmish.Graphics2D.Lighting
open Mibo.Animation
open PlatformerSample.Constants
open PlatformerSample.Types
open PlatformerSample.Physics
open PlatformerSample.WorldGen

// -------------------------------------------------------------
// Pre-allocated buffers (Level 4 — avoid per-frame allocation)
// -------------------------------------------------------------

let nearbyPlatforms = ResizeArray<Rectangle>(256)
let keysToRemove = ResizeArray<struct(int*int)>(32)

// -------------------------------------------------------------
// System: Chunk Management
// -------------------------------------------------------------

let chunkSystem (dt: float32) (model: Model) : struct(Model * Cmd<Msg>) =
  let pos = model.PlayerPosition
  loadChunks pos model.Chunks model.Seed
  evictDistantChunks pos model.Chunks
  model, Cmd.none

// -------------------------------------------------------------
// System: Input -> Movement Intent
// -------------------------------------------------------------

let inputSystem (dt: float32) (model: Model) : struct(Model * Cmd<Msg>) =
  let moveDir =
    if model.Actions.Held.Contains(GameAction.MoveLeft) then -1.0f
    elif model.Actions.Held.Contains(GameAction.MoveRight) then 1.0f
    else 0.0f

  model.PlayerVelocity <- Vector2(moveDir * moveSpeed, model.PlayerVelocity.Y)
  model, Cmd.none

// -------------------------------------------------------------
// System: Physics (gravity + collision)
// -------------------------------------------------------------

let physicsSystem (dt: float32) (model: Model) : struct(Model * Cmd<Msg>) =
  let canJump = model.IsGrounded
  let jumpPressed = model.Actions.Started.Contains(GameAction.Jump)

  let velocityY =
    if jumpPressed && canJump then
      jumpSpeed
    else
      model.PlayerVelocity.Y + gravity * dt

  let velocity = Vector2(model.PlayerVelocity.X, velocityY)
  let prevPos = model.PlayerPosition
  let newPos = prevPos + velocity * dt

  // Collect platforms from nearby chunks only
  nearbyPlatforms.Clear()
  let pcx = int(Math.Floor(float newPos.X / float chunkWorldSize))
  let pcy = int(Math.Floor(float newPos.Y / float chunkWorldSize))
  for KeyValue(key, chunk) in model.Chunks do
    let struct(cx, cy) = key
    if abs (cx - pcx) <= chunkLoadRadius && abs (cy - pcy) <= chunkLoadRadius then
      nearbyPlatforms.AddRange(chunk.Platforms)
  let platforms = nearbyPlatforms.ToArray()

  let struct (finalPos, finalVel, isGrounded) =
    resolvePlatformCollision prevPos newPos velocity platforms

  let mutable finalPos = finalPos
  let mutable finalVel = finalVel
  let mutable isGrounded = isGrounded

  // Respawn if fallen too far
  if finalPos.Y > groundLevel + 500.0f then
    finalPos <- Vector2(spawnX, groundSurface - playerHeight)
    finalVel <- Vector2.Zero
    isGrounded <- true

  // Respawn key
  if model.Actions.Started.Contains(GameAction.Respawn) then
    finalPos <- Vector2(spawnX, groundSurface - playerHeight)
    finalVel <- Vector2.Zero
    isGrounded <- true

  // Clamp to world left edge
  if finalPos.X < 0.0f then
    finalPos <- Vector2(0.0f, finalPos.Y)

  model.PlayerPosition <- finalPos
  model.PlayerVelocity <- finalVel
  model.IsGrounded <- isGrounded

  // Update facing
  let moveDir =
    if model.Actions.Held.Contains(GameAction.MoveLeft) then -1.0f
    elif model.Actions.Held.Contains(GameAction.MoveRight) then 1.0f
    else 0.0f
  let newFacing =
    if moveDir < 0.0f then -1.0f
    elif moveDir > 0.0f then 1.0f
    else model.PlayerFacing
  model.PlayerFacing <- newFacing

  // Smooth camera follow
  let targetX = finalPos.X
  let targetY = finalPos.Y
  let smoothX = model.CameraPos.X + (targetX - model.CameraPos.X) * 0.1f
  let smoothY = model.CameraPos.Y + (targetY - model.CameraPos.Y) * 0.1f
  model.CameraPos <- Vector2(
    Math.Max(0.0f, smoothX),
    Math.Clamp(smoothY, -500.0f, 2000.0f)
  )

  // Track chunk coordinate
  let currentChunk = struct(pcx, pcy)
  model.PlayerChunk <- currentChunk

  model, Cmd.none

// -------------------------------------------------------------
// System: Animation
// -------------------------------------------------------------

let animationSystem (dt: float32) (model: Model) : struct(Model * Cmd<Msg>) =
  let animState = getAnimationState model.PlayerVelocity model.IsGrounded

  let playerSprite =
    match animState with
    | Idle -> AnimatedSprite.playIfNot "idle" model.PlayerSprite
    | Walk -> AnimatedSprite.playIfNot "walk" model.PlayerSprite
    | Jump -> AnimatedSprite.playIfNot "jump" model.PlayerSprite
    | Fall -> AnimatedSprite.playIfNot "fall" model.PlayerSprite

  let updatedSprite = AnimatedSprite.update dt playerSprite
  let flippedSprite =
    if model.PlayerFacing < 0.0f then
      AnimatedSprite.facingLeft updatedSprite
    else
      AnimatedSprite.facingRight updatedSprite

  model.PlayerSprite <- flippedSprite
  model.AnimationState <- animState

  // Torch animation (single shared sprite for all torches)
  model.TorchSprite <- AnimatedSprite.update dt model.TorchSprite

  model, Cmd.none

// -------------------------------------------------------------
// System: Particles
// -------------------------------------------------------------

let particleSystem (dt: float32) (model: Model) : struct(Model * Cmd<Msg>) =
  let mutable playedJumpSound =
    model.Actions.Started.Contains(GameAction.Jump) && model.IsGrounded

  let particles = model.Particles
  let particleVelocities = model.ParticleVelocities
  let mutable particleCount = model.ParticleCount

  // Spawn burst on jump
  if playedJumpSound then
    let rng = Random()
    for i = 0 to 11 do
      if particleCount < particles.Length then
        particles[particleCount] <- {
          Position = model.PlayerPosition + Vector2(playerWidth / 2.0f, playerHeight)
          Size = Vector2(8.0f, 8.0f)
          Rotation = float32(rng.NextDouble() * Math.PI * 2.0)
          SourceRect = Rectangle(0.0f, 0.0f, 1.0f, 1.0f)
          Color = Color(255uy, 255uy, 0uy, 255uy)
        }
        particleVelocities[particleCount] <- Vector2(
          float32(rng.NextDouble() * 200.0 - 100.0),
          float32(rng.NextDouble() * -150.0 - 50.0)
        )
        particleCount <- particleCount + 1

  // Update existing particles
  for i = 0 to particleCount - 1 do
    let vel = particleVelocities[i]
    let newVel = Vector2(vel.X, vel.Y + gravity * dt * 0.3f)
    particleVelocities[i] <- newVel
    particles[i] <- {
      particles[i] with
          Position = particles[i].Position + newVel * dt
    }

  ParticleSimulation.fadeAndCompact particles &particleCount 255.0f dt
  model.ParticleCount <- particleCount

  if playedJumpSound then
    Raylib.PlaySound(model.Assets.JumpSound)

  model, Cmd.none

// -------------------------------------------------------------
// System: Day / Night
// -------------------------------------------------------------

let dayNightSystem (dt: float32) (model: Model) : struct(Model * Cmd<Msg>) =
  let newTime = (model.DayNightTimeOfDay + dt * (24.0f / model.DayNightDuration)) % 24.0f
  model.DayNightTimeOfDay <- newTime
  model.TotalTime <- model.TotalTime + dt
  model, Cmd.none

// -------------------------------------------------------------
// Combined Update Pipeline (Level 3 — explicit phase ordering)
// -------------------------------------------------------------

let update (msg: Msg) (model: Model) : struct(Model * Cmd<Msg>) =
  match msg with
  | InputMapped actions ->
    model.Actions <- actions
    model, Cmd.none

  | Tick gt ->
    let dt = float32 gt.ElapsedGameTime.TotalSeconds

    // Phase 1: Mutable systems (physics, particles, chunks)
    System.start model
    |> System.pipeMutable (chunkSystem dt)
    |> System.pipeMutable (inputSystem dt)
    |> System.pipeMutable (physicsSystem dt)
    |> System.pipeMutable (animationSystem dt)
    |> System.pipeMutable (particleSystem dt)
    |> System.pipeMutable (dayNightSystem dt)
    |> System.finish id
