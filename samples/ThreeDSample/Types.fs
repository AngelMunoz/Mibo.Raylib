module ThreeDSample.Types

open System
open System.Collections.Generic
open System.Numerics
open Raylib_cs
open Mibo.Elmish
open Mibo.Input

[<Struct>]
type GameAction =
  | MoveLeft
  | MoveRight
  | MoveForward
  | MoveBackward
  | Jump
  | Respawn
  | RotateCameraLeft
  | RotateCameraRight
  | RotateCameraUp
  | RotateCameraDown

[<Struct>]
type BlockType =
  | Empty
  | Ground
  | GroundSlopeXPos
  | GroundSlopeXNeg
  | GroundSlopeZPos
  | GroundSlopeZNeg
  | Platform
  | PlatformRamp
  | SnowGround
  | SnowSlopeXPos
  | SnowSlopeXNeg
  | SnowSlopeZPos
  | SnowSlopeZNeg
  | Spikes
  | TreePine
  | TreeSnow
  | Rock
  | GrassTuft
  | Coin
  | Jewel
  | Heart
  | Star
  | Mushrooms
  | Crate
  | Barrel
  | Flag

module BlockType =
  let modelPath = function
    | Ground -> Constants.KenneyModels.blockGrass
    | GroundSlopeXPos -> Constants.KenneyModels.blockGrassSlope
    | GroundSlopeXNeg -> Constants.KenneyModels.blockGrassSlope
    | GroundSlopeZPos -> Constants.KenneyModels.blockGrassSlope
    | GroundSlopeZNeg -> Constants.KenneyModels.blockGrassSlope
    | Platform -> Constants.KenneyModels.platform
    | PlatformRamp -> Constants.KenneyModels.platformRamp
    | SnowGround -> Constants.KenneyModels.blockSnow
    | SnowSlopeXPos -> Constants.KenneyModels.blockSnowSlope
    | SnowSlopeXNeg -> Constants.KenneyModels.blockSnowSlope
    | SnowSlopeZPos -> Constants.KenneyModels.blockSnowSlope
    | SnowSlopeZNeg -> Constants.KenneyModels.blockSnowSlope
    | Spikes -> Constants.KenneyModels.spikeBlock
    | TreePine -> Constants.KenneyModels.treePine
    | TreeSnow -> Constants.KenneyModels.treeSnow
    | Rock -> Constants.KenneyModels.rocks
    | GrassTuft -> Constants.KenneyModels.grass
    | Coin -> Constants.KenneyModels.coinGold
    | Jewel -> Constants.KenneyModels.jewel
    | Heart -> Constants.KenneyModels.heart
    | Star -> Constants.KenneyModels.star
    | Mushrooms -> Constants.KenneyModels.mushrooms
    | Crate -> Constants.KenneyModels.crate
    | Barrel -> Constants.KenneyModels.barrel
    | Flag -> Constants.KenneyModels.flag
    | Empty -> ""

  let modelVerticalOffset = function
    | Platform | PlatformRamp -> Constants.cellSize * 0.5f
    | Coin | Jewel | Heart | Star | Flag -> Constants.cellSize * 0.5f
    | _ -> 0.0f

  let modelRotation = function
    | GroundSlopeXNeg -> 180.0f
    | GroundSlopeZPos -> 90.0f
    | GroundSlopeZNeg -> -90.0f
    | SnowSlopeXNeg -> 180.0f
    | SnowSlopeZPos -> 90.0f
    | SnowSlopeZNeg -> -90.0f
    | _ -> 0.0f

  let isSolid = function
    | Empty | Coin | Jewel | Heart | Star | GrassTuft | Mushrooms | Flag -> false
    | _ -> true

  let isCollectible = function
    | Coin | Jewel | Heart | Star -> true
    | _ -> false

  let isDecoration = function
    | TreePine | TreeSnow | Rock | GrassTuft | Mushrooms | Flag | Barrel | Crate -> true
    | _ -> false

[<Struct>]
type Chunk = {
  Grid: Mibo.Layout3D.CellGrid3D<BlockType>
  Bounds: BoundingBox
  OriginX: int
  OriginZ: int
}

type GameModel() =
  member val PlayerPosition = Constants.spawnPosition with get, set
  member val PlayerVelocity = Vector3.Zero with get, set
  member val IsGrounded = false with get, set
  member val CameraYaw = Constants.cameraDefaultYaw with get, set
  member val CameraPitch = Constants.cameraDefaultPitch with get, set
  member val CameraPosition = Constants.spawnPosition + Vector3(0.0f, 4.0f, 8.0f) with get, set
  member val CameraTarget = Constants.spawnPosition with get, set
  member val Actions: ActionState<GameAction> = ActionState.empty with get, set
  member val InputMap: InputMap<GameAction> = InputMap.empty with get, set
  member val PlayerModel = Unchecked.defaultof<Model> with get, set
  member val ModelCache = Dictionary<string, Model>() with get, set
  member val Chunks = Dictionary<struct (int * int), Chunk>() with get, set
  member val TotalTime = 0.0f with get, set
  member val DayNightTimeOfDay = 12.0f with get, set
  member val DayNightDuration = 60.0f with get, set
  member val Score = 0 with get, set
  member val Seed = 0 with get, set
  member val KeysToRemove = ResizeArray<struct (int * int)>() with get, set
  member val PlayerFacing = 0.0f with get, set

[<Struct>]
type Msg =
  | Tick of tick: GameTime
  | InputMapped of inputs: ActionState<GameAction>
