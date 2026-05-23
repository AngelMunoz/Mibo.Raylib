module PlatformerSample.Types

open System
open System.Collections.Generic
open System.Numerics
open Raylib_cs
open System.Numerics
open Raylib_cs
open Mibo.Elmish
open Mibo.Elmish.Graphics2D.Lighting
open Mibo.Input
open Mibo.Animation

// -------------------------------------------------------------
// Domain Types
// -------------------------------------------------------------

type GameAction =
  | MoveLeft
  | MoveRight
  | Jump
  | Respawn

type AnimationState =
  | Idle
  | Walk
  | Jump
  | Fall

type TileType =
  | Empty
  | Ground
  | Platform

type TorchLight = {
  Position: Vector2
  Color: Color
  Radius: float32
}

type Chunk = {
  Grid: Mibo.Layout.CellGrid2D<TileType>
  Platforms: Rectangle[]
  Occluders: Occluder2D[]
  Torches: TorchLight[]
  Bounds: Rectangle
}

type SpriteAssets = {
  PlayerSheet: SpriteSheet
  TileTexture: Texture2D
  TorchSheet: SpriteSheet
  ParticleTexture: Texture2D
  Font: Font
  JumpSound: Sound
}

// -------------------------------------------------------------
// Mutable Model (Level 2.5 — reduces GC pressure)
// -------------------------------------------------------------

type Model() as self =
  member val PlayerPosition = Vector2(200.0f, 0.0f) with get, set
  member val PlayerVelocity = Vector2.Zero with get, set
  member val PlayerFacing = 1.0f with get, set
  member val IsGrounded = true with get, set
  member val CameraPos = Vector2(200.0f, 0.0f) with get, set
  member val Actions: ActionState<GameAction> = ActionState.empty with get, set
  member val InputMap: InputMap<GameAction> = InputMap.empty with get, set
  member val Assets: SpriteAssets = Unchecked.defaultof<_> with get, set
  member val TotalTime = 0.0f with get, set
  member val AnimationState = Idle with get, set
  member val PlayerSprite: AnimatedSprite = Unchecked.defaultof<_> with get, set
  member val TorchSprite: AnimatedSprite = Unchecked.defaultof<_> with get, set
  member val PlayerChunk = struct(0, 0) with get, set
  member val Chunks = Dictionary<struct(int*int), Chunk>() with get, set
  member val Seed = 0 with get, set
  member val DayNightTimeOfDay = 12.0f with get, set
  member val DayNightDuration = 60.0f with get, set
  member val Lighting: LightContext2D = Unchecked.defaultof<_> with get, set
  member val Particles: Particle2D[] = Array.zeroCreate 512 with get, set
  member val ParticleVelocities: Vector2[] = Array.zeroCreate 512 with get, set
  member val ParticleCount = 0 with get, set

  interface IDisposable with
    member _.Dispose() =
      if self.Lighting <> Unchecked.defaultof<_> then
        (self.Lighting :> IDisposable).Dispose()

// -------------------------------------------------------------
// Struct Messages (Level 2.5 — zero-allocation dispatch)
// -------------------------------------------------------------

type Msg =
  | Tick of GameTime
  | InputMapped of ActionState<GameAction>
