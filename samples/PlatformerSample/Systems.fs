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
let keysToRemove = ResizeArray<struct (int * int)>(32)

let confettiColors =
  [| Color(255uy, 50uy, 50uy, 255uy)
     Color(50uy, 255uy, 50uy, 255uy)
     Color(50uy, 50uy, 255uy, 255uy)
     Color(255uy, 255uy, 50uy, 255uy)
     Color(255uy, 50uy, 255uy, 255uy)
     Color(50uy, 255uy, 255uy, 255uy)
     Color(255uy, 150uy, 50uy, 255uy)
     Color(255uy, 50uy, 150uy, 255uy) |]

// -------------------------------------------------------------
// System: Input -> Movement Intent
// -------------------------------------------------------------

let inputSystem (dt: float32) (model: Model) : struct (Model * Cmd<Msg>) =
  let moveDir =
    if model.Actions.Held.Contains(GameAction.MoveLeft) then
      -1.0f
    elif model.Actions.Held.Contains(GameAction.MoveRight) then
      1.0f
    else
      0.0f

  model.PlayerVelocity <- Vector2(moveDir * moveSpeed, model.PlayerVelocity.Y)
  model, Cmd.none

// -------------------------------------------------------------
// System: Physics (gravity + collision + camera follow)
// -------------------------------------------------------------

let physicsSystem (dt: float32) (model: Model) : struct (Model * Cmd<Msg>) =
  let canJump = model.IsGrounded
  let jumpPressed = model.Actions.Started.Contains(GameAction.Jump)

  let velocityY =
    if jumpPressed && canJump then
      // Spawn confetti burst
      let rng = System.Random.Shared
      let mutable pc = model.ParticleCount
      let particles = model.Particles
      let particleVelocities = model.ParticleVelocities
      for i = 0 to 19 do
        if pc < particles.Length then
          let spawnPos =
            model.PlayerPosition
            + Vector2(
                playerWidth / 2.0f + float32(rng.NextDouble() * 20.0 - 10.0),
                playerHeight * 0.3f
              )
          particles[pc] <- {
            Position = spawnPos
            Size = Vector2(4.0f, 4.0f)
            Rotation = float32(rng.NextDouble() * Math.PI * 2.0)
            SourceRect = Rectangle(0.0f, 0.0f, 1.0f, 1.0f)
            Color = confettiColors[rng.Next(confettiColors.Length)]
          }
          particleVelocities[pc] <-
            Vector2(
              float32(rng.NextDouble() * 300.0 - 150.0),
              float32(rng.NextDouble() * -250.0 - 50.0)
            )
          pc <- pc + 1
      model.ParticleCount <- pc
      Raylib.PlaySound(model.Assets.JumpSound)

      jumpSpeed
    else
      model.PlayerVelocity.Y + gravity * dt

  let velocity = Vector2(model.PlayerVelocity.X, velocityY)
  let prevPos = model.PlayerPosition
  let newPos = prevPos + velocity * dt

  // Collect platforms from nearby chunks only (reuse pre-allocated buffer)
  nearbyPlatforms.Clear()
  let pcx = int(Math.Floor(float newPos.X / float chunkWorldSize))
  let pcy = int(Math.Floor(float newPos.Y / float chunkWorldSize))

  for KeyValue(key, chunk) in model.Chunks do
    let struct (cx, cy) = key

    if abs(cx - pcx) <= chunkLoadRadius && abs(cy - pcy) <= chunkLoadRadius then
      nearbyPlatforms.AddRange(chunk.Platforms)

  let struct (finalPos, finalVel, isGrounded) =
    resolvePlatformCollision prevPos newPos velocity nearbyPlatforms

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
    if model.Actions.Held.Contains(GameAction.MoveLeft) then
      -1.0f
    elif model.Actions.Held.Contains(GameAction.MoveRight) then
      1.0f
    else
      0.0f

  let newFacing =
    if moveDir < 0.0f then -1.0f
    elif moveDir > 0.0f then 1.0f
    else model.PlayerFacing

  model.PlayerFacing <- newFacing

  // Smooth camera follow (mutates raylib Camera2D in place)
  let mutable cam = model.Camera
  Camera2D.smoothFollow &cam finalPos 0.1f
  Camera2D.clampTarget &cam 0.0f -500.0f 999999.0f 2000.0f
  model.Camera <- cam

  // Track chunk coordinate
  model.PlayerChunk <- struct (pcx, pcy)

  model, Cmd.none

// -------------------------------------------------------------
// System: Chunk Management (only runs when player changes chunk)
// -------------------------------------------------------------

let chunkSystem (dt: float32) (model: Model) : struct (Model * Cmd<Msg>) =
  let pos = model.PlayerPosition
  let pcx = int(Math.Floor(float pos.X / float chunkWorldSize))
  let pcy = int(Math.Floor(float pos.Y / float chunkWorldSize))
  let currentChunk = struct (pcx, pcy)

  if currentChunk <> model.PlayerChunk then
    loadChunks pos model.Chunks model.Seed
    evictDistantChunks pos model.Chunks keysToRemove

  model, Cmd.none

// -------------------------------------------------------------
// System: Animation
// -------------------------------------------------------------

let animationSystem (dt: float32) (model: Model) : struct (Model * Cmd<Msg>) =
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
  model.TorchSprite <- AnimatedSprite.update dt model.TorchSprite

  model, Cmd.none

// -------------------------------------------------------------
// System: Particles
// -------------------------------------------------------------

let particleSystem (dt: float32) (model: Model) : struct (Model * Cmd<Msg>) =
  let particles = model.Particles
  let particleVelocities = model.ParticleVelocities
  let mutable particleCount = model.ParticleCount

  for i = 0 to particleCount - 1 do
    let vel = particleVelocities[i]
    let newVel = Vector2(vel.X, vel.Y + gravity * dt * 0.05f)
    particleVelocities[i] <- newVel
    particles[i] <- {
      particles[i] with
          Position = particles[i].Position + newVel * dt
    }

  ParticleSimulation.fadeAndCompact particles &particleCount 60.0f dt
  model.ParticleCount <- particleCount
  model, Cmd.none

// -------------------------------------------------------------
// System: Day / Night
// -------------------------------------------------------------

let dayNightSystem (dt: float32) (model: Model) : struct (Model * Cmd<Msg>) =
  let newTime =
    (model.DayNightTimeOfDay + dt * (24.0f / model.DayNightDuration)) % 24.0f

  model.DayNightTimeOfDay <- newTime
  model.TotalTime <- model.TotalTime + dt
  model, Cmd.none

// -------------------------------------------------------------
// Combined Update Pipeline (Level 3 — explicit phase ordering)
// -------------------------------------------------------------

let update (msg: Msg) (model: Model) : struct (Model * Cmd<Msg>) =
  match msg with
  | InputMapped actions ->
    model.Actions <- actions
    model, Cmd.none

  | Tick gt ->
    let dt = float32 gt.ElapsedGameTime.TotalSeconds

    // Phase ordering: input → physics → chunk → animation → particles → day/night
    System.start model
    |> System.pipeMutable(inputSystem dt)
    |> System.pipeMutable(physicsSystem dt)
    |> System.pipeMutable(chunkSystem dt)
    |> System.pipeMutable(animationSystem dt)
    |> System.pipeMutable(particleSystem dt)
    |> System.pipeMutable(dayNightSystem dt)
    |> System.finish id
