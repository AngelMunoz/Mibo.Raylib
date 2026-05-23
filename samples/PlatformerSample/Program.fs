module PlatformerSample.Program

open System
open System.Collections.Generic
open System.Numerics
open Raylib_cs
open Mibo.Elmish
open Mibo.Elmish.Graphics2D
open Mibo.Elmish.Graphics2D.Lighting
open Mibo.Input
open Mibo.Layout
open Mibo.Animation

// -------------------------------------------------------------
// Constants
// -------------------------------------------------------------

let tileSize = 64.0f
let chunkCells = 32
let chunkWorldSize = float32 chunkCells * tileSize  // 2048
let playerWidth = 40.0f
let playerHeight = 54.0f
let gravity = 1200.0f
let moveSpeed = 300.0f
let jumpSpeed = -700.0f
let worldHeight = 12.0f
let groundLevel = worldHeight * tileSize
let groundSurface = groundLevel - tileSize

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
// Tile & Chunk Types
// -------------------------------------------------------------

type TileType =
  | Empty
  | Solid

type TorchLight = {
  Position: Vector2
  Color: Color
  Radius: float32
}

type Chunk = {
  Grid: CellGrid2D<TileType>
  Platforms: Rectangle[]
  Occluders: Occluder2D[]
  Torches: TorchLight[]
  Bounds: Rectangle
}

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
// Chunk Generation
// -------------------------------------------------------------

let chunkSeed (cx: int) (cy: int) (worldSeed: int) =
  cx * 73856093 + cy * 19349663 + worldSeed

let extractPlatforms (grid: CellGrid2D<TileType>) : Rectangle[] =
  let platforms = ResizeArray<Rectangle>()
  let cellW = grid.CellSize.X
  let cellH = grid.CellSize.Y

  for y in 0 .. grid.Height - 1 do
    let mutable x = 0
    while x < grid.Width do
      match CellGrid2D.get x y grid with
      | ValueSome Solid ->
        let startX = x
        let mutable runLength = 1
        let mutable more = true
        while more && x + runLength < grid.Width do
          match CellGrid2D.get (x + runLength) y grid with
          | ValueSome Solid -> runLength <- runLength + 1
          | _ -> more <- false

        let wx = grid.Origin.X + float32 startX * cellW
        let wy = grid.Origin.Y + float32 y * cellH
        platforms.Add(Rectangle(wx, wy, float32 runLength * cellW, cellH))
        x <- x + runLength
      | _ ->
        x <- x + 1

  platforms.ToArray()

let extractTorches (grid: CellGrid2D<TileType>) (rng: Random) : TorchLight[] =
  let torches = ResizeArray<TorchLight>()
  let cellW = grid.CellSize.X

  for y in 0 .. grid.Height - 1 do
    let mutable x = 0
    while x < grid.Width do
      match CellGrid2D.get x y grid with
      | ValueSome Solid ->
        match CellGrid2D.get x (y - 1) grid with
        | ValueNone ->
          if rng.NextDouble() > 0.92 then
            let wx = grid.Origin.X + float32 x * cellW + cellW * 0.5f
            let wy = grid.Origin.Y + float32 y * grid.CellSize.Y - 10.0f
            torches.Add({
              Position = Vector2(wx, wy)
              Color = Color(255uy, 160uy, 60uy)
              Radius = 100.0f + float32(rng.Next(-20, 20))
            })
        | _ -> ()
        x <- x + 1
      | _ ->
        x <- x + 1

  torches.ToArray()

let generateChunk (cx: int) (cy: int) (worldSeed: int) : Chunk =
  let rng = Random(chunkSeed cx cy worldSeed)
  let origin = Vector2(float32 cx * chunkWorldSize, float32 cy * chunkWorldSize)
  let grid = CellGrid2D.create chunkCells chunkCells (Vector2(tileSize, tileSize)) origin

  let groundY = int(worldHeight)  // 12

  if cy = 0 then
    // Ground chunk: floor with pits and floating platforms
    Layout.run (fun section ->
      section
      |> Layout.section 0 groundY (fun groundSection ->
        groundSection
        |> Platformer.platform chunkCells Solid
        |> ignore

        let pitCount = rng.Next(1, 4)
        for _ in 1 .. pitCount do
          let px = rng.Next(0, chunkCells - 5)
          let pw = rng.Next(2, 5)
          groundSection
          |> Layout.section px 0 (Platformer.pit pw 1)
          |> ignore

        groundSection
      )
      |> ignore

      // Floating platforms above ground (reachable: need <= 204px jump)
      // groundY=12, so rows 8-9 (y=512-576) need 128-192px jump, all reachable
      let platCount = rng.Next(1, 4)
      for _ in 1 .. platCount do
        let px = rng.Next(0, chunkCells - 8)
        let py = rng.Next(groundY - 4, groundY - 2)
        let pw = rng.Next(3, 8)
        section
        |> Layout.section px py (Platformer.platform pw Solid)
        |> ignore

      section
    ) grid
    |> ignore

  // Air chunks (cy < 0) are kept empty for now — reachable vertical
  // traversal via stairs/ledges can be added later

  let platforms = extractPlatforms grid
  let torches = extractTorches grid rng
  let occluders = GridOccluders.fromCellGrid (fun t -> t = Solid) grid

  {
    Grid = grid
    Platforms = platforms
    Occluders = occluders
    Torches = torches
    Bounds = Rectangle(origin.X, origin.Y, chunkWorldSize, chunkWorldSize)
  }

let loadChunks (playerPos: Vector2) (chunks: Dictionary<struct(int*int), Chunk>) (seed: int) =
  let pcx = int(Math.Floor(float playerPos.X / float chunkWorldSize))
  let pcy = int(Math.Floor(float playerPos.Y / float chunkWorldSize))
  let radius = 2

  for x in pcx - radius .. pcx + radius do
    for y in pcy - radius .. pcy + radius do
      let key = struct(x, y)
      if not (chunks.ContainsKey(key)) then
        chunks[key] <- generateChunk x y seed

// -------------------------------------------------------------
// Model
// -------------------------------------------------------------

type SpriteAssets = {
  PlayerSheet: SpriteSheet
  TileTexture: Texture2D
  TorchSheet: SpriteSheet
  ShadowTexture: Texture2D
  Font: Font
  JumpSound: Sound
}

type Model = {
  PlayerPosition: Vector2
  PlayerVelocity: Vector2
  PlayerFacing: float32
  IsGrounded: bool
  CameraPos: Vector2
  Actions: ActionState<GameAction>
  InputMap: InputMap<GameAction>
  Assets: SpriteAssets
  TotalTime: float32
  AnimationState: AnimationState
  PlayerSprite: AnimatedSprite
  TorchSprite: AnimatedSprite
  PlayerChunk: struct(int*int)
  Chunks: Dictionary<struct(int*int), Chunk>
  Seed: int
  DayNight: DayNight.State
  Lighting: LightContext2D
  Particles: Particle2D[]
  ParticleVelocities: Vector2[]
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

let playerBounds(pos: Vector2) =
  Raylib_cs.Rectangle(pos.X, pos.Y, playerWidth, playerHeight)

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

  let torchTex =
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

  let playerSheet =
    SpriteSheet.fromFrames
      playerTex
      (Vector2(64.0f, 64.0f))
      [|
        struct ("idle", { Frames = [| r 645 0 128 128 |]; FrameDuration = 1.0f; Loop = false })
        struct ("walk", { Frames = [| r 0 129 128 128; r 129 129 128 128 |]; FrameDuration = 0.1f; Loop = true })
        struct ("jump", { Frames = [| r 774 0 128 128 |]; FrameDuration = 1.0f; Loop = false })
        struct ("fall", { Frames = [| r 774 0 128 128 |]; FrameDuration = 1.0f; Loop = false })
      |]

  let playerSprite = AnimatedSprite.create playerSheet "idle"

  // torch_on_a (65,1105) and torch_on_b (130,1105) — 64x64 each
  let torchSheet =
    SpriteSheet.fromFrames
      torchTex
      (Vector2(32.0f, 32.0f))
      [|
        struct ("lit", { Frames = [| r 65 1105 64 64; r 130 1105 64 64 |]; FrameDuration = 0.15f; Loop = true })
      |]

  let torchSprite = AnimatedSprite.create torchSheet "lit"

  let seed = Random().Next()
  let spawnY = groundSurface - playerHeight

  let chunks = Dictionary<struct(int*int), Chunk>()
  let spawnChunkX = 0
  let spawnChunkY = 0
  let loadRadius = 2

  for x in spawnChunkX - loadRadius .. spawnChunkX + loadRadius do
    for y in spawnChunkY - loadRadius .. spawnChunkY + loadRadius do
      chunks[struct(x, y)] <- generateChunk x y seed

  let lighting =
    new LightContext2D(softness = 0.05f, maxShadowDistance = 2000.0f)

  let assetsRec = {
    PlayerSheet = playerSheet
    TileTexture = tileTex
    TorchSheet = torchSheet
    ShadowTexture = shadowTex
    Font = font
    JumpSound = jumpSound
  }

  struct ({
            PlayerPosition = Vector2(200.0f, spawnY)
            PlayerVelocity = Vector2.Zero
            PlayerFacing = 1.0f
            IsGrounded = true
            CameraPos = Vector2(200.0f, spawnY)
            Actions = ActionState.empty
            InputMap = inputMap
            Assets = assetsRec
            TotalTime = 0.0f
            AnimationState = Idle
            PlayerSprite = playerSprite
            TorchSprite = torchSprite
            PlayerChunk = struct(0, 0)
            Chunks = chunks
            Seed = seed
            DayNight = DayNight.initial
            Lighting = lighting
            Particles = Array.zeroCreate 512
            ParticleVelocities = Array.zeroCreate 512
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

    // Only load/evict chunks when player enters a new chunk
    let pcx = int(Math.Floor(float newPos.X / float chunkWorldSize))
    let pcy = int(Math.Floor(float newPos.Y / float chunkWorldSize))
    let currentChunk = struct(pcx, pcy)

    if currentChunk <> model.PlayerChunk then
      loadChunks newPos model.Chunks model.Seed

      // Evict distant chunks
      let evictRadius = 4
      let keysToRemove = ResizeArray<struct(int*int)>()
      for KeyValue(key, _) in model.Chunks do
        let struct(cx, cy) = key
        if abs (cx - pcx) > evictRadius || abs (cy - pcy) > evictRadius then
          keysToRemove.Add(key)
      for key in keysToRemove do
        model.Chunks.Remove(key) |> ignore

    // Collect platforms from nearby chunks only
    let nearbyPlatforms = ResizeArray<Rectangle>()
    let collisionRadius = 2
    for KeyValue(key, chunk) in model.Chunks do
      let struct(cx, cy) = key
      if abs (cx - pcx) <= collisionRadius && abs (cy - pcy) <= collisionRadius then
        nearbyPlatforms.AddRange(chunk.Platforms)
    let platforms = nearbyPlatforms.ToArray()

    let struct (finalPos, finalVel, isGrounded) =
      resolvePlatformCollision prevPos newPos velocity platforms

    let mutable finalPos = finalPos
    let mutable finalVel = finalVel
    let mutable isGrounded = isGrounded

    // Respawn if fallen too far
    if finalPos.Y > groundLevel + 500.0f then
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

    // Smooth camera follow
    let viewportWidth = 1280.0f
    let viewportHeight = 720.0f
    let targetCameraX = finalPos.X
    let targetCameraY = finalPos.Y
    let smoothX = model.CameraPos.X + (targetCameraX - model.CameraPos.X) * 0.1f
    let smoothY = model.CameraPos.Y + (targetCameraY - model.CameraPos.Y) * 0.1f
    let cameraX = Math.Max(0.0f, smoothX)
    let cameraY = Math.Clamp(smoothY, -500.0f, 2000.0f)

    let animState = getAnimationState finalVel isGrounded
    let dayNight = DayNight.update dt model.DayNight

    // Animation update
    let playerSprite =
      match animState with
      | Idle -> AnimatedSprite.playIfNot "idle" model.PlayerSprite
      | Walk -> AnimatedSprite.playIfNot "walk" model.PlayerSprite
      | Jump -> AnimatedSprite.playIfNot "jump" model.PlayerSprite
      | Fall -> AnimatedSprite.playIfNot "fall" model.PlayerSprite
    let updatedSprite = AnimatedSprite.update dt playerSprite
    let flippedSprite =
      if newFacing < 0.0f then
        AnimatedSprite.facingLeft updatedSprite
      else
        AnimatedSprite.facingRight updatedSprite

    // Particle physics
    let particles = model.Particles
    let particleVelocities = model.ParticleVelocities
    let mutable particleCount = model.ParticleCount

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
          particleVelocities[particleCount] <- Vector2(
            float32(rng.NextDouble() * 200.0 - 100.0),
            float32(rng.NextDouble() * -150.0 - 50.0)
          )
          particleCount <- particleCount + 1

    for i = 0 to particleCount - 1 do
      let vel = particleVelocities[i]
      let newVel = Vector2(vel.X, vel.Y + gravity * dt * 0.3f)
      particleVelocities[i] <- newVel
      particles[i] <- {
        particles[i] with
            Position = particles[i].Position + newVel * dt
      }

    ParticleSimulation.fadeAndCompact particles &particleCount 255.0f dt

    if playedJumpSound then
      Raylib.PlaySound(model.Assets.JumpSound)

    // Update torch animation
    let torchSprite = AnimatedSprite.update dt model.TorchSprite

    struct ({
              model with
                  PlayerPosition = finalPos
                  PlayerVelocity = finalVel
                  PlayerFacing = newFacing
                  IsGrounded = isGrounded
                  CameraPos = Vector2(cameraX, cameraY)
                  Actions = actions
                  TotalTime = model.TotalTime + dt
                  AnimationState = animState
                  DayNight = dayNight
                  PlayerSprite = flippedSprite
                  TorchSprite = torchSprite
                  PlayerChunk = currentChunk
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

  let camera =
    Camera2D(
      Vector2(float32 ctx.WindowWidth / 2.0f, float32 ctx.WindowHeight / 2.0f),
      Vector2(model.CameraPos.X, model.CameraPos.Y),
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

  let viewBounds =
    Camera2D.viewportBoundsFromRaylib
      camera
      (float32 ctx.WindowWidth)
      (float32 ctx.WindowHeight)

  buffer
  |> Draw.rectGradientV
    (-1000<RenderLayer>)
    (0, 0, ctx.WindowWidth, ctx.WindowHeight, skyTop, skyBot)
  |> Draw.beginCamera 0<RenderLayer> camera
  |> LightDraw.setAmbient model.Lighting (5<RenderLayer>, { Color = ambient })
  |> Draw.drop

      // Sun directional light
  if sunIntensity > 0.0f then
    let sunDir =
      Vector2.Normalize(Vector2(playerCenterX, groundLevel - 200.0f) - sunPos)

    buffer
    |> LightDraw.addDirectionalLight model.Lighting (6<RenderLayer>) {
      Direction = sunDir
      Color = Color(255uy, 245uy, 220uy)
      Intensity = sunIntensity * 1.5f
      CastsShadows = true
    }
    |> Draw.drop

  // Moon directional light
  if moonIntensity > 0.0f then
    let moonDir =
      Vector2.Normalize(Vector2(playerCenterX, groundLevel - 200.0f) - moonPos)

    buffer
    |> LightDraw.addDirectionalLight model.Lighting (6<RenderLayer>) {
      Direction = moonDir
      Color = Color(180uy, 200uy, 255uy)
      Intensity = moonIntensity * 0.8f
      CastsShadows = true
    }
    |> Draw.drop

  // Collect occluders and torches from chunks near player, sorted by distance
  let pcx = int(Math.Floor(float model.PlayerPosition.X / float chunkWorldSize))
  let pcy = int(Math.Floor(float model.PlayerPosition.Y / float chunkWorldSize))
  let radius = 2

  let mutable nearbyOccluders = []
  let mutable nearbyTorches = []

  for KeyValue(key, chunk) in model.Chunks do
    let struct(cx, cy) = key
    if abs (cx - pcx) <= radius && abs (cy - pcy) <= radius then
      nearbyOccluders <- chunk.Occluders |> Array.toList |> List.append nearbyOccluders
      nearbyTorches <- chunk.Torches |> Array.toList |> List.append nearbyTorches

  // Sort occluders by distance to player and take nearest 128
  let playerPos = model.PlayerPosition
  let occludersSorted =
    nearbyOccluders
    |> List.sortBy (fun o ->
      let mx = (o.P1.X + o.P2.X) * 0.5f
      let my = (o.P1.Y + o.P2.Y) * 0.5f
      (mx - playerPos.X) * (mx - playerPos.X) + (my - playerPos.Y) * (my - playerPos.Y)
    )
    |> List.truncate 128

  // Sort torches by distance and take nearest 16
  let torchesSorted =
    nearbyTorches
    |> List.sortBy (fun t ->
      let dx = t.Position.X - playerPos.X
      let dy = t.Position.Y - playerPos.Y
      dx * dx + dy * dy
    )
    |> List.truncate 16

  // Add torches as point lights and draw animated sprites
  let torchSrc = AnimatedSprite.currentSource model.TorchSprite

  for torch in torchesSorted do
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

    // Draw torch sprite
    let torchDest = r (int torch.Position.X - 16) (int torch.Position.Y - 32) 32 32
    buffer
    |> LightDraw.litSprite model.Lighting {
      Texture = model.Assets.TorchSheet.Texture
      Dest = torchDest
      Source = torchSrc
      Origin = Vector2.Zero
      Rotation = 0.0f
      Color = Color.White
      Layer = 7<RenderLayer>
    }
    |> Draw.drop

  // Add occluders
  for occluder in occludersSorted do
    buffer
    |> LightDraw.addOccluder model.Lighting (8<RenderLayer>) occluder
    |> Draw.drop

  // Render visible tiles only from nearby chunks
  let tileSrc = r 260 585 64 64

  for KeyValue(key, chunk) in model.Chunks do
    let struct(cx, cy) = key
    if abs (cx - pcx) <= radius && abs (cy - pcy) <= radius then
      if Culling.isVisible2D viewBounds chunk.Bounds then
        CellGrid2D.iterVisible
          (int viewBounds.X)
          (int viewBounds.Y)
          (int (viewBounds.X + viewBounds.Width))
          (int (viewBounds.Y + viewBounds.Height))
          (fun x y tile ->
            if tile = Solid then
              let wx = chunk.Grid.Origin.X + float32 x * tileSize
              let wy = chunk.Grid.Origin.Y + float32 y * tileSize
              let dest = Rectangle(wx, wy, tileSize, tileSize)

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
          )
          chunk.Grid

  // Lit player sprite
  let playerSrc = AnimatedSprite.currentSource model.PlayerSprite
  let mutable playerSrcMut = playerSrc

  if model.PlayerSprite.FlipX then
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
    Texture = model.Assets.PlayerSheet.Texture
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
      $"Day/Night Cycle | Time: {model.DayNight.TimeOfDay:F1}h | Chunks: {model.Chunks.Count} | Pos: %.1f{model.PlayerPosition.X},%.1f{model.PlayerPosition.Y} | WASD/Arrows: Move | Space: Jump | R: Respawn"
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
