module PlatformerSample.Program

open System
open System.Numerics
open Raylib_cs
open Mibo.Elmish
open Mibo.Elmish.Graphics2D

[<Measure>]
type EntityId

// -------------------------------------------------------------
// Game Actions
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

// -------------------------------------------------------------
// Physics Constants
// -------------------------------------------------------------

let tileSize = 64.0f
let worldHeight = 12.0f
let groundLevel = worldHeight * tileSize
let groundSurface = groundLevel - tileSize
let playerWidth = 40.0f
let playerHeight = 54.0f
let gravity = 1200.0f
let moveSpeed = 300.0f
let jumpSpeed = -700.0f

// -------------------------------------------------------------
// Day / Night Cycle
// -------------------------------------------------------------

module DayNight =
  type State = {
    TimeOfDay: float32
    DayDuration: float32
  }

  let initial = {
    TimeOfDay = 12.0f
    DayDuration = 60.0f
  }

  let update dt state =
    let hoursPerSecond = 24.0f / state.DayDuration

    {
      state with
          TimeOfDay = (state.TimeOfDay + dt * hoursPerSecond) % 24.0f
    }

  let lerpColor (a: Color) (b: Color) (t: float32) : Color =
    let clamp01 x = Math.Clamp(x, 0.0f, 1.0f)
    let t = clamp01 t
    let r = byte(int(float32 a.R + t * (float32 b.R - float32 a.R)))
    let g = byte(int(float32 a.G + t * (float32 b.G - float32 a.G)))
    let bl = byte(int(float32 a.B + t * (float32 b.B - float32 a.B)))
    let al = byte(int(float32 a.A + t * (float32 b.A - float32 a.A)))
    Color(r, g, bl, al)

  let getSkyColors time : Color * Color =
    let midnightTop = Color(10uy, 10uy, 30uy)
    let midnightBot = Color(20uy, 20uy, 40uy)
    let dayTop = Color(100uy, 149uy, 237uy)
    let dayBot = Color(173uy, 216uy, 230uy)
    let sunsetTop = Color(50uy, 50uy, 100uy)
    let sunsetBot = Color(255uy, 80uy, 50uy)

    if time < 6.0f then
      midnightTop, midnightBot
    elif time < 8.0f then
      let t = (time - 6.0f) / 2.0f
      lerpColor midnightTop dayTop t, lerpColor midnightBot dayBot t
    elif time < 16.0f then
      dayTop, dayBot
    elif time < 18.0f then
      let t = (time - 16.0f) / 2.0f
      lerpColor dayTop sunsetTop t, lerpColor dayBot sunsetBot t
    elif time < 20.0f then
      let t = (time - 18.0f) / 2.0f
      lerpColor sunsetTop midnightTop t, lerpColor sunsetBot midnightBot t
    else
      midnightTop, midnightBot

  let getAmbientColor time : Color =
    let top, bot = getSkyColors time

    let avg =
      (int top.R + int top.G + int top.B + int bot.R + int bot.G + int bot.B)
      / 6
      |> float32

    let intensity = MathF.Max(avg / 255.0f, 0.12f)
    let r = byte(int(intensity * 255.0f))
    let g = byte(int(intensity * 245.0f))
    let b = byte(int(intensity * 230.0f))
    Color(r, g, b, 255uy)

  let getSunIntensity time : float32 =
    if time < 6.0f || time > 18.0f then 0.0f
    elif time < 8.0f then (time - 6.0f) / 2.0f
    elif time < 16.0f then 1.0f
    else (18.0f - time) / 2.0f

  let getMoonIntensity time : float32 =
    if time > 6.0f && time < 18.0f then 0.0f
    elif time < 8.0f then 1.0f - (time - 6.0f) / 2.0f
    elif time < 16.0f then 0.0f
    elif time < 18.0f then (time - 16.0f) / 2.0f
    else 1.0f

// -------------------------------------------------------------
// Terrain
// -------------------------------------------------------------

[<Struct>]
type Platform = { Bounds: Raylib_cs.Rectangle }

type TorchLight = {
  Position: Vector2
  Color: Color
  Radius: float32
}

let generateTerrain
  (seed: int)
  : Platform list * TorchLight list * Occluder2D list =
  let rng = Random(seed)
  let mutable platforms = []
  let mutable torches = []
  let mutable occluders = []
  let mutable x = 0.0f

  while x < 5000.0f do
    let segmentLength = rng.Next(3, 8) |> float32 |> (*) tileSize
    let py = groundSurface
    let rect = Raylib_cs.Rectangle(x, py, segmentLength, tileSize)
    platforms <- { Bounds = rect } :: platforms

    // Top edge occluder
    occluders <-
      { P1 = Vector2(x, py); P2 = Vector2(x + segmentLength, py) }
      :: occluders

    // Left and right side occluders (for sideways shadows)
    occluders <-
      { P1 = Vector2(x, py); P2 = Vector2(x, py + tileSize) }
      :: occluders

    occluders <-
      { P1 = Vector2(x + segmentLength, py); P2 = Vector2(x + segmentLength, py + tileSize) }
      :: occluders

    // Add torches every few tiles
    let torchCount = segmentLength / tileSize / 3.0f |> int

    for i = 1 to torchCount do
      let tx = x + float32 i * 3.0f * tileSize

      if tx < x + segmentLength - tileSize then
        torches <-
          {
            Position = Vector2(tx + tileSize / 2.0f, py - 10.0f)
            Color = Color(255uy, 160uy, 60uy)
            Radius = 120.0f
          }
          :: torches

    x <- x + segmentLength + (rng.Next(2, 5) |> float32 |> (*) tileSize)

  for _ = 1 to 10 do
    let px = rng.Next(200, 4800) |> float32
    let pw = rng.Next(2, 5) |> float32 |> (*) tileSize
    let py = groundSurface - (rng.Next(2, 5) |> float32 |> (*) tileSize)

    platforms <-
      {
        Bounds = Raylib_cs.Rectangle(px, py, pw, tileSize)
      }
      :: platforms

    // Top, bottom, left, right edges for elevated platforms
    occluders <-
      {
        P1 = Vector2(px, py)
        P2 = Vector2(px + pw, py)
      }
      :: occluders

    occluders <-
      {
        P1 = Vector2(px, py + tileSize)
        P2 = Vector2(px + pw, py + tileSize)
      }
      :: occluders

    occluders <-
      {
        P1 = Vector2(px, py)
        P2 = Vector2(px, py + tileSize)
      }
      :: occluders

    occluders <-
      {
        P1 = Vector2(px + pw, py)
        P2 = Vector2(px + pw, py + tileSize)
      }
      :: occluders

    // Elevated platform torch
    if rng.NextDouble() > 0.5 then
      torches <-
        {
          Position = Vector2(px + pw / 2.0f, py - 10.0f)
          Color = Color(255uy, 160uy, 60uy)
          Radius = 100.0f
        }
        :: torches

  platforms |> List.rev, torches |> List.rev, occluders |> List.rev

// -------------------------------------------------------------
// Model
// -------------------------------------------------------------

type SpriteAssets = {
  PlayerTexture: Texture2D
  TileTexture: Texture2D
  ShadowTexture: Texture2D
  Font: Font
  JumpSound: Sound
}

type Model = {
  PlayerPosition: Vector2
  PlayerVelocity: Vector2
  PlayerFacing: float32
  IsGrounded: bool
  CameraX: float32
  Actions: ActionState<GameAction>
  InputMap: InputMap<GameAction>
  Assets: SpriteAssets
  TotalTime: float32
  AnimationState: AnimationState
  Platforms: Platform list
  Torches: TorchLight list
  Occluders: Occluder2D list
  Seed: int
  DayNight: DayNight.State
}

// -------------------------------------------------------------
// Messages
// -------------------------------------------------------------

type Msg =
  | Tick of GameTime
  | InputMapped of ActionState<GameAction>

// -------------------------------------------------------------
// Helpers
// -------------------------------------------------------------

let r (x: int) (y: int) (w: int) (h: int) : Raylib_cs.Rectangle =
  Raylib_cs.Rectangle(float32 x, float32 y, float32 w, float32 h)

let getAnimationState (velocity: Vector2) (isGrounded: bool) : AnimationState =
  if not isGrounded then
    if velocity.Y > 0.0f then Fall else Jump
  elif abs velocity.X > 1.0f then
    Walk
  else
    Idle

let getPlayerSrcRect
  (totalTime: float32)
  (state: AnimationState)
  : Raylib_cs.Rectangle =
  match state with
  | Idle -> r 645 0 128 128
  | Walk ->
    let frame = int(totalTime * 10.0f) % 2
    if frame = 0 then r 0 129 128 128 else r 129 129 128 128
  | Jump -> r 774 0 128 128
  | Fall -> r 774 0 128 128

// -------------------------------------------------------------
// Collision
// -------------------------------------------------------------

let playerBounds(pos: Vector2) : Raylib_cs.Rectangle =
  Raylib_cs.Rectangle(pos.X, pos.Y, playerWidth, playerHeight)

let resolvePlatformCollision
  (prevPos: Vector2)
  (newPos: Vector2)
  (velocity: Vector2)
  (platforms: Platform list)
  : struct (Vector2 * Vector2 * bool) =

  let mutable pos = newPos
  let mutable vel = velocity
  let mutable grounded = false

  let prevBounds = playerBounds prevPos
  let newBounds = playerBounds pos

  for platform in platforms do
    let pb = platform.Bounds

    if Raylib.CheckCollisionRecs(newBounds, pb).AsBool() then
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

// -------------------------------------------------------------
// Init
// -------------------------------------------------------------

let init(ctx: GameContext) =
  let playerTex =
    ctx.Assets.Texture(
      "assets/kenney_platformer/Spritesheets/spritesheet-characters-default.png"
    )

  let tileTex =
    ctx.Assets.Texture(
      "assets/kenney_platformer/Spritesheets/spritesheet-tiles-default.png"
    )

  let font = ctx.Assets.Font("assets/Fonts/monogram.ttf")
  let jumpSound = ctx.Assets.Sound("assets/sfx_jump.ogg")

  let shadowImg =
    Raylib.GenImageColor(1, 1, Color(0uy, 0uy, 0uy, 200uy))

  let shadowTex = Raylib.LoadTextureFromImage(shadowImg)
  Raylib.UnloadImage(shadowImg)

  let inputMap =
    InputMap.empty
    |> InputMap.key MoveLeft KeyboardKey.A
    |> InputMap.key MoveLeft KeyboardKey.Left
    |> InputMap.key MoveRight KeyboardKey.D
    |> InputMap.key MoveRight KeyboardKey.Right
    |> InputMap.key GameAction.Jump KeyboardKey.Space
    |> InputMap.key Respawn KeyboardKey.R

  let assets = {
    PlayerTexture = playerTex
    TileTexture = tileTex
    ShadowTexture = shadowTex
    Font = font
    JumpSound = jumpSound
  }

  let seed = Random().Next()
  let platforms, torches, occluders = generateTerrain seed
  let spawnY = groundSurface - playerHeight

  struct ({
            PlayerPosition = Vector2(200.0f, spawnY)
            PlayerVelocity = Vector2.Zero
            PlayerFacing = 1.0f
            IsGrounded = true
            CameraX = 0.0f
            Actions = ActionState.empty
            InputMap = inputMap
            Assets = assets
            TotalTime = 0.0f
            AnimationState = Idle
            Platforms = platforms
            Torches = torches
            Occluders = occluders
            Seed = seed
            DayNight = DayNight.initial
          },
          Cmd.none)

// -------------------------------------------------------------
// Update
// -------------------------------------------------------------

let update (msg: Msg) (model: Model) =
  match msg with
  | InputMapped actions -> struct ({ model with Actions = actions }, Cmd.none)
  | Tick gameTime ->
    let dt = float32 gameTime.ElapsedGameTime.TotalSeconds

    // -- Input polling --
    let actions = Keyboard.poll model.InputMap model.Actions

    // -- Horizontal movement --
    let moveDir =
      if actions.Held.Contains(MoveLeft) then -1.0f
      elif actions.Held.Contains(MoveRight) then 1.0f
      else 0.0f

    // -- Jump --
    let canJump = model.IsGrounded
    let jumpPressed = actions.Started.Contains(GameAction.Jump)

    let mutable playedJumpSound = false

    let velocityY =
      if jumpPressed && canJump then
        playedJumpSound <- true
        jumpSpeed
      else
        model.PlayerVelocity.Y + gravity * dt

    let velocityX = moveDir * moveSpeed
    let velocity = Vector2(velocityX, velocityY)

    // -- Integrate position --
    let prevPos = model.PlayerPosition
    let newPos = prevPos + velocity * dt

    // -- Platform collision --
    let struct (finalPos, finalVel, isGrounded) =
      resolvePlatformCollision prevPos newPos velocity model.Platforms

    // -- Fall off world --
    let mutable finalPos = finalPos
    let mutable finalVel = finalVel
    let mutable isGrounded = isGrounded

    if finalPos.Y > groundLevel + 200.0f then
      finalPos <- Vector2(200.0f, groundSurface - playerHeight)
      finalVel <- Vector2.Zero
      isGrounded <- true

    // -- Respawn --
    if actions.Started.Contains(Respawn) then
      finalPos <- Vector2(200.0f, groundSurface - playerHeight)
      finalVel <- Vector2.Zero
      isGrounded <- true

    // -- Constrain to left edge --
    if finalPos.X < 0.0f then
      finalPos <- Vector2(0.0f, finalPos.Y)

    // -- Facing --
    let newFacing =
      if moveDir < 0.0f then -1.0f
      elif moveDir > 0.0f then 1.0f
      else model.PlayerFacing

    // -- Camera follow --
    let viewportWidth = 1280.0f
    let targetCameraX = finalPos.X - viewportWidth * 0.3f
    let cameraX = Math.Max(0.0f, targetCameraX)

    // -- Animation state --
    let animState = getAnimationState finalVel isGrounded

    // -- Day/Night --
    let dayNight = DayNight.update dt model.DayNight

    // -- Play sound --
    if playedJumpSound then
      Raylib.PlaySound(model.Assets.JumpSound)

    struct ({
              model with
                  PlayerPosition = finalPos
                  PlayerVelocity = finalVel
                  PlayerFacing = newFacing
                  IsGrounded = isGrounded
                  CameraX = cameraX
                  Actions = actions
                  TotalTime = model.TotalTime + dt
                  AnimationState = animState
                  DayNight = dayNight
            },
            Cmd.none)

// -------------------------------------------------------------
// View
// -------------------------------------------------------------

let view (ctx: GameContext) (model: Model) (buffer: RenderBuffer<RenderCmd2D>) =
  let time = model.DayNight.TimeOfDay
  let topColor, botColor = DayNight.getSkyColors time
  let ambient = DayNight.getAmbientColor time
  let sunIntensity = DayNight.getSunIntensity time
  let moonIntensity = DayNight.getMoonIntensity time

  // Sky gradient (drawn before camera, layer -1000)
  buffer.Add(
    -1000<RenderLayer>,
    DrawSkyGradient(topColor, botColor, -1000<RenderLayer>)
  )

  // World camera
  let cameraCenterX = model.CameraX + float32 ctx.WindowWidth / 2.0f
  let cameraCenterY = groundLevel - float32 ctx.WindowHeight / 2.0f

  buffer.Add(
    0<RenderLayer>,
    SetCamera2D {
      Position = Vector2(cameraCenterX, cameraCenterY)
      Zoom = 1.0f
      Layer = 0<RenderLayer>
    }
  )

  // Ambient light
  buffer.Add(5<RenderLayer>, SetLighting { Color = ambient })

  // Sun directional light
  if sunIntensity > 0.01f then
    buffer.Add(
      6<RenderLayer>,
      AddDirectionalLight {
        Direction = Vector2(0.3f, -0.7f)
        Color = Color(255uy, 240uy, 200uy)
        Intensity = 0.8f * sunIntensity
      }
    )

  // Moon directional light
  if moonIntensity > 0.01f then
    buffer.Add(
      6<RenderLayer>,
      AddDirectionalLight {
        Direction = Vector2(-0.3f, -0.7f)
        Color = Color(160uy, 180uy, 255uy)
        Intensity = 0.4f * moonIntensity
      }
    )

  // Torch point lights
  for torch in model.Torches do
    buffer.Add(
      7<RenderLayer>,
      AddPointLight {
        Position = torch.Position
        Color = torch.Color
        Intensity = 1.2f
        Radius = torch.Radius
        Falloff = 1.5f
      }
    )

  // Occluders for shadow volumes
  for occluder in model.Occluders do
    buffer.Add(8<RenderLayer>, AddOccluder occluder)

  // Contact shadows (sprite-based, drawn before platforms)
  let groundY = int groundLevel

  for platform in model.Platforms do
    let pb = platform.Bounds
    let platformBottom = int(pb.Y + pb.Height)

    let shadowBottom =
      if pb.Y + pb.Height >= groundLevel - 1.0f then
        platformBottom + 4
      else
        groundY

    let shadowH = shadowBottom - platformBottom

    if shadowH > 0 then
      buffer.Add(
        9<RenderLayer>,
        DrawSprite {
          Texture = model.Assets.ShadowTexture
          Dest =
            r
              (int pb.X + 4)
              platformBottom
              (int pb.Width - 8)
              shadowH
          Source = Raylib_cs.Rectangle(0.0f, 0.0f, 1.0f, 1.0f)
          Origin = Vector2.Zero
          Rotation = 0.0f
          Color = Color(255uy, 255uy, 255uy, 140uy)
          Layer = 9<RenderLayer>
        }
      )

  // Platforms
  let tileSrc = r 260 585 64 64

  for platform in model.Platforms do
    let pb = platform.Bounds
    let tileCount = int(pb.Width / tileSize)

    for i = 0 to tileCount - 1 do
      let dest =
        r
          (int(pb.X) + i * int tileSize)
          (int pb.Y)
          (int tileSize)
          (int tileSize)

      buffer.Add(
        10<RenderLayer>,
        DrawSprite {
          Texture = model.Assets.TileTexture
          Dest = dest
          Source = tileSrc
          Origin = Vector2.Zero
          Rotation = 0.0f
          Color = Color.White
          Layer = 10<RenderLayer>
        }
      )

  // Player sprite
  let playerSrc = getPlayerSrcRect model.TotalTime model.AnimationState
  let mutable playerSrcMut = playerSrc

  if model.PlayerFacing < 0.0f then
    playerSrcMut <-
      Raylib_cs.Rectangle(
        playerSrcMut.X,
        playerSrcMut.Y,
        -playerSrcMut.Width,
        playerSrcMut.Height
      )

  let playerDrawY = int(model.PlayerPosition.Y + playerHeight - 64.0f)
  let playerDest = r (int model.PlayerPosition.X) playerDrawY 64 64

  buffer.Add(
    20<RenderLayer>,
    DrawSprite {
      Texture = model.Assets.PlayerTexture
      Dest = playerDest
      Source = playerSrcMut
      Origin = Vector2.Zero
      Rotation = 0.0f
      Color = Color.White
      Layer = 20<RenderLayer>
    }
  )

  // UI camera
  buffer.Add(1000<RenderLayer>, ResetCamera2D)

  let timeStr =
    sprintf "Time: %.1fh | WASD/Arrows: Move | Space: Jump | R: Respawn" time

  buffer.Add(
    1001<RenderLayer>,
    DrawText {
      Font = model.Assets.Font
      Text = timeStr
      Position = Vector2(10.0f, 10.0f)
      FontSize = 20.0f
      Spacing = 1.0f
      Color = Color.White
      Layer = 1001<RenderLayer>
    }
  )

// -------------------------------------------------------------
// Entry Point
// -------------------------------------------------------------

let subscribe _ctx _model = Sub.none

[<EntryPoint>]
let main _ =
  let program =
    Program.mkProgram init update
    |> Program.withConfig(fun cfg ->
      cfg.Width <- 1280
      cfg.Height <- 720
      cfg.Title <- "Mibo Raylib MVP"
      cfg.TargetFPS <- 60)
    |> Program.withTick Tick
    |> Program.withRenderer(fun () -> Batch2DRenderer.create view)

  let game = new RaylibGame<Model, Msg>(program)
  game.Run()
  0
