module PlatformerSample.Program

open System
open System.Numerics
open Raylib_cs
open Mibo.Elmish
open Mibo.Elmish.Graphics2D
open Mibo.Elmish.Graphics2D.Lighting
open Mibo.Input

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

  let inline update dt state =
    let hoursPerSecond = 24.0f / state.DayDuration

    {
      state with
          TimeOfDay = (state.TimeOfDay + dt * hoursPerSecond) % 24.0f
    }

  let inline lerpColor (a: Color) (b: Color) (t: float32) =
    let t = Math.Clamp(t, 0.0f, 1.0f)

    Color(
      byte(float32 a.R + t * (float32 b.R - float32 a.R)),
      byte(float32 a.G + t * (float32 b.G - float32 a.G)),
      byte(float32 a.B + t * (float32 b.B - float32 a.B)),
      255uy
    )

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

    Color(
      byte(intensity * 255.0f),
      byte(intensity * 245.0f),
      byte(intensity * 230.0f),
      255uy
    )

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

  let worldCenterX = (groundLevel / tileSize) * tileSize * 0.5f * 3.0f // ~2500, midpoint of generated terrain

  let orbitalPositions (centerX: float32) (state: State) =
    let centerY = groundLevel - 200.0f
    let radiusX = 500.0f
    let radiusY = 200.0f
    let sunAngle = (state.TimeOfDay - 18.0f) / 24.0f * MathF.PI * 2.0f
    let moonAngle = sunAngle + MathF.PI
    let sunX = centerX + radiusX * MathF.Cos(sunAngle)
    let sunY = centerY + radiusY * MathF.Sin(sunAngle)
    let moonX = centerX + radiusX * MathF.Cos(moonAngle)
    let moonY = centerY + radiusY * MathF.Sin(moonAngle)
    Vector2(sunX, sunY), Vector2(moonX, moonY)

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

    occluders <-
      {
        P1 = Vector2(x, py)
        P2 = Vector2(x + segmentLength, py)

      }
      :: occluders

    occluders <-
      {
        P1 = Vector2(x, py)
        P2 = Vector2(x, py + tileSize)

      }
      :: occluders

    occluders <-
      {
        P1 = Vector2(x + segmentLength, py)
        P2 = Vector2(x + segmentLength, py + tileSize)

      }
      :: occluders

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
  Lighting: LightContext2D
  Particles: Particle2D[]
  ParticleCount: int
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

let inline r (x: int) (y: int) (w: int) (h: int) =
  Raylib_cs.Rectangle(float32 x, float32 y, float32 w, float32 h)

let getAnimationState (velocity: Vector2) (isGrounded: bool) =
  if not isGrounded then
    if velocity.Y > 0.0f then Fall else Jump
  elif abs velocity.X > 1.0f then
    Walk
  else
    Idle

let getPlayerSrcRect (totalTime: float32) (state: AnimationState) =
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

let playerBounds(pos: Vector2) =
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

  for platform in platforms do
    let pb = platform.Bounds

    if Raylib.CheckCollisionRecs(playerBounds pos, pb).AsBool() then
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
  let assets = GameContext.getService<IAssets> ctx

  let playerTex =
    assets.Texture(
      "assets/kenney_platformer/Spritesheets/spritesheet-characters-default.png"
    )

  let tileTex =
    assets.Texture(
      "assets/kenney_platformer/Spritesheets/spritesheet-tiles-default.png"
    )

  let font = assets.Font("assets/Fonts/monogram.ttf")
  let jumpSound = assets.Sound("assets/sfx_jump.ogg")

  let shadowImg = Raylib.GenImageColor(1, 1, Color(0uy, 0uy, 0uy, 200uy))
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

  let assetsRec = {
    PlayerTexture = playerTex
    TileTexture = tileTex
    ShadowTexture = shadowTex
    Font = font
    JumpSound = jumpSound
  }

  let seed = Random().Next()
  let platforms, torches, occluders = generateTerrain seed
  let spawnY = groundSurface - playerHeight

  let lighting =
    new LightContext2D(softness = 0.05f, maxShadowDistance = 2000.0f)

  struct ({
            PlayerPosition = Vector2(200.0f, spawnY)
            PlayerVelocity = Vector2.Zero
            PlayerFacing = 1.0f
            IsGrounded = true
            CameraX = 0.0f
            Actions = ActionState.empty
            InputMap = inputMap
            Assets = assetsRec
            TotalTime = 0.0f
            AnimationState = Idle
            Platforms = platforms
            Torches = torches
            Occluders = occluders
            Seed = seed
            DayNight = DayNight.initial
            Lighting = lighting
            Particles = Array.zeroCreate 512
            ParticleCount = 0
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
    let actions = model.Actions

    let moveDir =
      if actions.Held.Contains(MoveLeft) then -1.0f
      elif actions.Held.Contains(MoveRight) then 1.0f
      else 0.0f

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

    let prevPos = model.PlayerPosition
    let newPos = prevPos + velocity * dt

    let struct (finalPos, finalVel, isGrounded) =
      resolvePlatformCollision prevPos newPos velocity model.Platforms

    let mutable finalPos = finalPos
    let mutable finalVel = finalVel
    let mutable isGrounded = isGrounded

    if finalPos.Y > groundLevel + 200.0f then
      finalPos <- Vector2(200.0f, groundSurface - playerHeight)
      finalVel <- Vector2.Zero
      isGrounded <- true

    if actions.Started.Contains(Respawn) then
      finalPos <- Vector2(200.0f, groundSurface - playerHeight)
      finalVel <- Vector2.Zero
      isGrounded <- true

    if finalPos.X < 0.0f then
      finalPos <- Vector2(0.0f, finalPos.Y)

    let newFacing =
      if moveDir < 0.0f then -1.0f
      elif moveDir > 0.0f then 1.0f
      else model.PlayerFacing

    let viewportWidth = 1280.0f
    let targetCameraX = finalPos.X - viewportWidth * 0.3f
    let cameraX = Math.Max(0.0f, targetCameraX)

    let animState = getAnimationState finalVel isGrounded
    let dayNight = DayNight.update dt model.DayNight

    // Particle burst on jump
    let mutable particleCount = model.ParticleCount
    let particles = model.Particles

    if playedJumpSound then
      let rng = Random()

      for i = 0 to 11 do
        if particleCount < particles.Length then
          particles[particleCount] <- {
            Position = finalPos + Vector2(playerWidth / 2.0f, playerHeight)
            Size = Vector2(8.0f, 8.0f)
            Rotation = float32(rng.NextDouble() * Math.PI * 2.0)
            SourceRect = Raylib_cs.Rectangle(0.0f, 0.0f, 1.0f, 1.0f)
            Color = Color(255uy, 255uy, 0uy, 255uy)
          }

          particleCount <- particleCount + 1

    ParticleSimulation.fadeAndCompact particles &particleCount 255.0f dt

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
                  ParticleCount = particleCount
            },
            Cmd.none)

// -------------------------------------------------------------
// View
// -------------------------------------------------------------

let view (ctx: GameContext) (model: Model) (buffer: RenderBuffer2D) =
  model.Lighting.Reset()

  let playerCenterX = model.PlayerPosition.X + playerWidth / 2.0f
  let playerCenterY = model.PlayerPosition.Y + playerHeight / 2.0f

  let cameraCenterX = playerCenterX
  let cameraCenterY = playerCenterY

  let camera =
    Camera2D(
      Vector2(float32 ctx.WindowWidth / 2.0f, float32 ctx.WindowHeight / 2.0f),
      Vector2(cameraCenterX, cameraCenterY),
      0.0f,
      1.0f
    )

  // Sky background and day/night ambient
  let time = model.DayNight.TimeOfDay
  let skyTop, skyBot = DayNight.getSkyColors time
  let ambient = DayNight.getAmbientColor time
  let sunIntensity = DayNight.getSunIntensity time
  let moonIntensity = DayNight.getMoonIntensity time
  let sunPos, moonPos = DayNight.orbitalPositions playerCenterX model.DayNight

  buffer
  |> Draw.rectGradientV
    (-1000<RenderLayer>)
    (0, 0, ctx.WindowWidth, ctx.WindowHeight, skyTop, skyBot)
  |> Draw.beginCamera 0<RenderLayer> camera
  |> LightDraw.setAmbient model.Lighting (5<RenderLayer>, { Color = ambient })
  |> Draw.drop

  // Sun directional light
  if sunIntensity > 0.0f then
    let sunDir = Vector2.Normalize(Vector2(playerCenterX, groundLevel - 200.0f) - sunPos)
    buffer
    |> LightDraw.addDirectionalLight model.Lighting (6<RenderLayer>) {
      Direction = sunDir
      Color = Color(255uy, 245uy, 220uy)
      Intensity = sunIntensity * 3.0f
      CastsShadows = true
    }
    |> Draw.drop

  // Moon directional light
  if moonIntensity > 0.0f then
    let moonDir = Vector2.Normalize(Vector2(playerCenterX, groundLevel - 200.0f) - moonPos)
    buffer
    |> LightDraw.addDirectionalLight model.Lighting (6<RenderLayer>) {
      Direction = moonDir
      Color = Color(180uy, 200uy, 255uy)
      Intensity = moonIntensity * 1.5f
      CastsShadows = true
    }
    |> Draw.drop

  // Torch point lights
  for torch in model.Torches do
    buffer
    |> LightDraw.addPointLight model.Lighting (7<RenderLayer>) {
      Position = torch.Position
      Color = torch.Color
      Intensity = 1.2f
      Radius = torch.Radius
      Falloff = 1.5f
      CastsShadows = false
    }
    |> Draw.drop

  // Occluders
  for occluder in model.Occluders do
    buffer
    |> LightDraw.addOccluder model.Lighting (8<RenderLayer>) occluder
    |> Draw.drop

  // Lit terrain tiles
  let tileSrc = r 260 585 64 64

  for platform in model.Platforms do
    let pb = platform.Bounds
    let tileCount = int(pb.Width / tileSize)

    for i = 0 to tileCount - 1 do
      let dest =
        r
          (int pb.X + i * int tileSize)
          (int pb.Y)
          (int tileSize)
          (int tileSize)

      buffer
      |> LightDraw.litSprite model.Lighting {
        Texture = model.Assets.TileTexture
        Dest = dest
        Source = tileSrc
        Origin = Vector2.Zero
        Rotation = 0.0f
        Color = Color.White
        Layer = 10<RenderLayer>
      }
      |> Draw.drop

  // Lit player sprite
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

  buffer
  |> LightDraw.litSprite model.Lighting {
    Texture = model.Assets.PlayerTexture
    Dest = playerDest
    Source = playerSrcMut
    Origin = Vector2.Zero
    Rotation = 0.0f
    Color = Color.White
    Layer = 20<RenderLayer>
  }
  |> Draw.drop

  // Particles
  buffer
  |> ParticleDraw.particles
    model.Assets.ShadowTexture
    model.Particles
    model.ParticleCount
    3<RenderLayer>

  // End lighting
  |> LightDraw.endLighting model.Lighting 999<RenderLayer>

  // End camera
  |> Draw.endCamera 1000<RenderLayer>

  // UI + diagnostics
  |> Draw.text {
    Font = model.Assets.Font
    Text =
      $"Day/Night Cycle | Time: {model.DayNight.TimeOfDay:F1}h | Pos: %.1f{model.PlayerPosition.X},%.1f{model.PlayerPosition.Y} | Occluders: {model.Occluders.Length} | WASD/Arrows: Move | Space: Jump | R: Respawn"
    Position = Vector2(10.0f, 10.0f)
    FontSize = 20.0f
    Spacing = 1.0f
    Color = Color.White
    Layer = 1001<RenderLayer>
  }
  |> Draw.drop

// -------------------------------------------------------------
// Entry Point
// -------------------------------------------------------------

let subscribe (ctx: GameContext) (model: Model) =
  Mibo.Input.InputMapper.subscribeStatic model.InputMap InputMapped ctx

[<EntryPoint>]
let main _ =
  let program =
    Program.mkProgram init update
    |> Program.withAssetsBasePath AppContext.BaseDirectory
    |> Program.withConfig(fun cfg -> {
      cfg with
          Width = 1280
          Height = 720
          Title = "Mibo Raylib Platformer"
          TargetFPS = 60
    })
    |> Program.withInput
    |> Program.withSubscription subscribe
    |> Program.withTick Tick
    |> Program.withRenderer(fun () -> Renderer2D.create view)

  let game = new RaylibGame<Model, Msg>(program)
  game.Run()
  0
