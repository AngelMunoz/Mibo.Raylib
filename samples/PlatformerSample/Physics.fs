module PlatformerSample.Physics

open System.Numerics
open Raylib_cs
open PlatformerSample.Constants
open PlatformerSample.Types

// -------------------------------------------------------------
// Player Bounds & Collision
// -------------------------------------------------------------

let playerBounds(pos: Vector2) =
  Rectangle(pos.X, pos.Y, playerWidth, playerHeight)

let inline checkCollision (a: Rectangle) (b: Rectangle) =
  a.X < b.X + b.Width && a.X + a.Width > b.X && a.Y < b.Y + b.Height && a.Y + a.Height > b.Y

let resolvePlatformCollision
  (prevPos: Vector2)
  (newPos: Vector2)
  (velocity: Vector2)
  (platforms: Rectangle[])
  : struct (Vector2 * Vector2 * bool) =
  let mutable pos = newPos
  let mutable vel = velocity
  let mutable grounded = false

  for pb in platforms do
    if checkCollision (playerBounds pos) pb then
      let prevFeetY = prevPos.Y + playerHeight
      let currFeetY = pos.Y + playerHeight
      let platformTop = pb.Y

      let crossedSurface =
        prevFeetY <= platformTop + 5.0f && currFeetY >= platformTop

      let movingDown = vel.Y >= 0.0f

      if crossedSurface && movingDown then
        pos <- Vector2(pos.X, platformTop - playerHeight)
        vel <- Vector2(vel.X, 0.0f)
        grounded <- true
      elif vel.Y < 0.0f then
        pos <- Vector2(pos.X, pb.Y + pb.Height)
        vel <- Vector2(vel.X, 0.0f)
      elif vel.X > 0.0f && prevPos.X + playerWidth <= pb.X then
        pos <- Vector2(pb.X - playerWidth, pos.Y)
        vel <- Vector2(0.0f, vel.Y)
      elif vel.X < 0.0f && prevPos.X >= pb.X + pb.Width then
        pos <- Vector2(pb.X + pb.Width, pos.Y)
        vel <- Vector2(0.0f, vel.Y)

  struct (pos, vel, grounded)

let getAnimationState (velocity: Vector2) (isGrounded: bool) =
  if not isGrounded then
    if velocity.Y > 0.0f then AnimationState.Fall else AnimationState.Jump
  elif abs velocity.X > 1.0f then
    AnimationState.Walk
  else
    AnimationState.Idle
