module ThreeDSample.Program

open System
open System.Numerics
open FSharp.NativeInterop
open Raylib_cs
open Mibo.Elmish
open Mibo.Elmish.Graphics3D
open Mibo.Elmish.DefaultShaders

// -------------------------------------------------------------
// Game Actions
// -------------------------------------------------------------

type GameAction =
  | MoveLeft
  | MoveRight
  | MoveForward
  | MoveBackward
  | Jump

// -------------------------------------------------------------
// Physics Constants (match original 3DSample)
// -------------------------------------------------------------

let gravity = -20.0f
let jumpSpeed = 15.0f
let moveSpeed = 10.0f
let acceleration = 25.0f
let friction = 8.0f
let fallLimit = -20.0f
let rollSpeed = 2.0f

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
    DayDuration = 30.0f
  }

  let update dt state =
    let hoursPerSecond = 24.0f / state.DayDuration

    {
      state with
          TimeOfDay = (state.TimeOfDay + dt * hoursPerSecond) % 24.0f
    }

  let getAmbient time : Color * float32 =
    if time < 5.0f || time > 19.0f then
      Color(15uy, 20uy, 45uy), 0.05f
    elif time < 7.0f then
      let t = (time - 5.0f) / 2.0f
      let r = byte(int(15.0f + t * 80.0f))
      let g = byte(int(20.0f + t * 100.0f))
      let b = byte(int(45.0f + t * 110.0f))
      Color(r, g, b), 0.05f + t * 0.65f
    elif time < 17.0f then
      Color(95uy, 130uy, 155uy), 0.70f
    elif time < 19.0f then
      let t = (time - 17.0f) / 2.0f
      let r = byte(int(95.0f + t * 40.0f))
      let g = byte(int(130.0f + t * 50.0f))
      let b = byte(int(155.0f + t * 60.0f))
      Color(r, g, b), 0.70f - t * 0.35f
    else
      Color(15uy, 20uy, 45uy), 0.05f

  let getBackgroundColor time : Color =
    if time < 5.0f || time > 19.0f then
      Color(5uy, 8uy, 20uy)
    elif time < 7.0f then
      let t = (time - 5.0f) / 2.0f
      let r = byte(int(5.0f + t * 60.0f))
      let g = byte(int(8.0f + t * 90.0f))
      let b = byte(int(20.0f + t * 100.0f))
      Color(r, g, b)
    elif time < 17.0f then
      Color(65uy, 98uy, 120uy)
    elif time < 19.0f then
      let t = (time - 17.0f) / 2.0f
      let r = byte(int(65.0f + t * 50.0f))
      let g = byte(int(98.0f + t * 40.0f))
      let b = byte(int(120.0f + t * 30.0f))
      Color(r, g, b)
    else
      Color(5uy, 8uy, 20uy)

  let getSunIntensity time : float32 =
    if time < 6.0f || time > 18.0f then 0.0f
    elif time < 8.0f then (time - 6.0f) / 2.0f
    elif time < 16.0f then 1.0f
    else (18.0f - time) / 2.0f

  let getMoonIntensity time : float32 =
    if time > 6.0f && time < 18.0f then 0.0f
    elif time < 8.0f then 1.0f - (time - 6.0f) / 2.0f
    elif time < 16.0f then 0.0f
    else (time - 16.0f) / 2.0f

  // Sun arcs across the sky from east to west
  let getSunDirection time =
    let angle = (time - 6.0f) / 12.0f * MathF.PI
    Vector3(MathF.Cos(angle) * 0.8f, -0.6f, MathF.Sin(angle) * 0.4f)

  let getMoonDirection time =
    let angle = (time - 18.0f) / 12.0f * MathF.PI
    Vector3(MathF.Cos(angle) * 0.6f, -0.5f, MathF.Sin(angle) * 0.3f)

// -------------------------------------------------------------
// Level Types
// -------------------------------------------------------------

type Torch3D = {
  Position: Vector3
  Color: Color
  Radius: float32
}

type Platform3D = {
  Position: Vector3
  LocalBounds: BoundingBox
  WorldBounds: BoundingBox
  ModelPath: string
}

// -------------------------------------------------------------
// Level Generation
// -------------------------------------------------------------

let generateLevel
  (getLocalBounds: string -> BoundingBox)
  : Platform3D list * Torch3D list =

  let create (position: Vector3) (modelPath: string) =
    let localBounds = getLocalBounds modelPath
    let worldMin = localBounds.Min + position
    let worldMax = localBounds.Max + position

    {
      Position = position
      LocalBounds = localBounds
      WorldBounds = BoundingBox(worldMin, worldMax)
      ModelPath = modelPath
    }

  let platforms = ResizeArray<Platform3D>()
  let torches = ResizeArray<Torch3D>()
  let rng = Random()

  // Central plaza: 12x12 grid of 4x4 floor tiles
  for ix = 0 to 11 do
    for iz = 0 to 11 do
      let px = float32 ix * 4.0f + 2.0f
      let pz = float32 iz * 4.0f + 2.0f

      platforms.Add(
        create
          (Vector3(px, 0.0f, pz))
          "assets/Models/Platform/platform_4x4x1_blue.obj"
      )

      // Frequent torch on ground tiles for visible point-light warmth
      if rng.NextDouble() < 0.12 then
        torches.Add {
          Position = Vector3(px, 1.0f, pz)
          Color = Color(255uy, 140uy, 40uy)
          Radius = 12.0f
        }

  // Elevated 2x2 platforms
  let elevated = [
    Vector3(20.0f, 3.0f, 20.0f)
    Vector3(28.0f, 5.0f, 16.0f)
    Vector3(36.0f, 7.0f, 24.0f)
    Vector3(16.0f, 4.0f, 32.0f)
    Vector3(8.0f, 6.0f, 28.0f)
  ]

  for pos in elevated do
    platforms.Add(create pos "assets/Models/Platform/platform_2x2x1_blue.obj")

    // Always add a torch on elevated platforms
    torches.Add {
      Position = Vector3(pos.X, pos.Y + 1.0f, pos.Z)
      Color = Color(255uy, 140uy, 40uy)
      Radius = 12.0f
    }

  // Stairs
  for step = 0 to 4 do
    platforms.Add(
      create
        (Vector3(44.0f + float32 step, float32 step, 4.0f))
        "assets/Models/Platform/platform_1x1x1_blue.obj"
    )

  for step = 0 to 4 do
    platforms.Add(
      create
        (Vector3(4.0f + float32 step, float32 step, 44.0f))
        "assets/Models/Platform/platform_1x1x1_blue.obj"
    )

  // Floating challenge blocks
  platforms.Add(
    create
      (Vector3(24.0f, 10.0f, 24.0f))
      "assets/Models/Platform/platform_1x1x1_blue.obj"
  )

  platforms.Add(
    create
      (Vector3(26.0f, 12.0f, 24.0f))
      "assets/Models/Platform/platform_1x1x1_blue.obj"
  )

  platforms.Add(
    create
      (Vector3(28.0f, 14.0f, 24.0f))
      "assets/Models/Platform/platform_1x1x1_blue.obj"
  )

  platforms |> Seq.toList, torches |> Seq.toList

// -------------------------------------------------------------
// Camera-Relative Movement
// -------------------------------------------------------------

let computeCameraRelativeMove
  (actions: ActionState<GameAction>)
  (cameraPos: Vector3)
  (cameraTarget: Vector3)
  : Vector3 =

  let forward =
    Vector3(cameraTarget.X - cameraPos.X, 0.0f, cameraTarget.Z - cameraPos.Z)

  let forward =
    if forward.LengthSquared() > 0.001f then
      Vector3.Normalize(forward)
    else
      Vector3.UnitZ

  let right = Vector3.Cross(forward, Vector3.UnitY)

  let mutable dir = Vector3.Zero

  if actions.Held.Contains(MoveForward) then
    dir <- dir + forward

  if actions.Held.Contains(MoveBackward) then
    dir <- dir - forward

  if actions.Held.Contains(MoveRight) then
    dir <- dir + right

  if actions.Held.Contains(MoveLeft) then
    dir <- dir - right

  if dir.LengthSquared() > 0.0f then
    Vector3.Normalize(dir)
  else
    Vector3.Zero

// -------------------------------------------------------------
// Acceleration / Friction
// -------------------------------------------------------------

let applyMovement
  (dt: float32)
  (moveDir: Vector3)
  (velocity: Vector3)
  : Vector3 =

  let horizontalVel = Vector2(velocity.X, velocity.Z)
  let hasInput = moveDir.LengthSquared() > 0.0f

  let newHorizontalVel =
    if hasInput then
      let targetVel = Vector2(moveDir.X * moveSpeed, moveDir.Z * moveSpeed)

      let diff = targetVel - horizontalVel
      let accel = acceleration * dt

      if diff.Length() <= accel then
        targetVel
      else
        horizontalVel + Vector2.Normalize(diff) * accel
    else
      let frictionAmount = friction * dt
      let speed = horizontalVel.Length()

      if speed <= frictionAmount then
        Vector2.Zero
      else
        horizontalVel * ((speed - frictionAmount) / speed)

  Vector3(newHorizontalVel.X, velocity.Y, newHorizontalVel.Y)

// -------------------------------------------------------------
// Collision
// -------------------------------------------------------------

let resolveCollision
  (prevPos: Vector3)
  (newPos: Vector3)
  (velocity: Vector3)
  (playerRadius: float32)
  (platforms: Platform3D list)
  : struct (Vector3 * Vector3 * bool) =

  let mutable pos = newPos
  let mutable vel = velocity
  let mutable grounded = false

  for platform in platforms do
    let platMin = platform.WorldBounds.Min
    let platMax = platform.WorldBounds.Max

    let inX = pos.X >= platMin.X && pos.X <= platMax.X
    let inZ = pos.Z >= platMin.Z && pos.Z <= platMax.Z

    if inX && inZ then
      let cellTop = platMax.Y
      let feetY = pos.Y - playerRadius
      let prevFeetY = prevPos.Y - playerRadius

      if
        vel.Y <= 0.0f
        && prevFeetY >= cellTop - 0.1f
        && feetY < cellTop
        && not grounded
      then
        pos <- Vector3(pos.X, cellTop + playerRadius, pos.Z)
        vel <- Vector3(vel.X, 0.0f, vel.Z)
        grounded <- true

      let cellBottom = platMin.Y
      let headY = pos.Y + playerRadius
      let prevHeadY = prevPos.Y + playerRadius

      if
        vel.Y > 0.0f && prevHeadY <= cellBottom + 0.1f && headY > cellBottom
      then
        pos <- Vector3(pos.X, cellBottom - playerRadius, pos.Z)
        vel <- Vector3(vel.X, 0.0f, vel.Z)

  struct (pos, vel, grounded)

// -------------------------------------------------------------
// Grid Rendering (per-platform, distance-faded)
// -------------------------------------------------------------

let drawPlatformGrid
  (playerPos: Vector3)
  (platform: Platform3D)
  (buffer: RenderBuffer3D)
  =

  let dist = Vector3.Distance(playerPos, platform.Position)
  let maxDist = 30.0f

  if dist > maxDist then
    ()
  else
    let alphaFactor = MathF.Max(0.0f, 1.0f - dist / maxDist)
    let alpha = byte(int(alphaFactor * 160.0f))
    let color = Color(120uy, 120uy, 120uy, alpha)

    let padding = 1.0f
    let bmin = platform.WorldBounds.Min
    let bmax = platform.WorldBounds.Max
    let y = bmin.Y - 0.01f

    let startX = MathF.Floor(bmin.X - padding)
    let endX = MathF.Ceiling(bmax.X + padding)
    let startZ = MathF.Floor(bmin.Z - padding)
    let endZ = MathF.Ceiling(bmax.Z + padding)

    let mutable z = startZ

    while z <= endZ + 0.001f do
      buffer.Add(
        2<RenderLayer3D>,
        DrawLine3D(Vector3(startX, y, z), Vector3(endX, y, z), color)
      )

      z <- z + 1.0f

    let mutable x = startX

    while x <= endX + 0.001f do
      buffer.Add(
        2<RenderLayer3D>,
        DrawLine3D(Vector3(x, y, startZ), Vector3(x, y, endZ), color)
      )

      x <- x + 1.0f

// -------------------------------------------------------------
// Quaternion to Axis-Angle
// -------------------------------------------------------------

let quatToAxisAngle(q: Quaternion) : Vector3 * float32 =
  let q = Quaternion.Normalize(q)
  let qW = MathF.Max(-1.0f, MathF.Min(1.0f, q.W))
  let angle = 2.0f * MathF.Acos(qW)
  let s = MathF.Sqrt(MathF.Max(0.0f, 1.0f - qW * qW))

  if s < 0.001f then
    Vector3.UnitX, 0.0f
  else
    let invS = 1.0f / s
    Vector3(q.X * invS, q.Y * invS, q.Z * invS), angle

// -------------------------------------------------------------
// Game State
// -------------------------------------------------------------

type GameState = {
  PlayerPosition: Vector3
  PlayerVelocity: Vector3
  TotalRollX: float32
  TotalRollZ: float32
  IsGrounded: bool
  CameraPosition: Vector3
  CameraTarget: Vector3
  Actions: ActionState<GameAction>
  InputMap: InputMap<GameAction>
  Platforms: Platform3D list
  Torches: Torch3D list
  TotalTime: float32
  DayNight: DayNight.State
  PlayerModel: Model
  PlayerRadius: float32
  PlayerCenterOffset: Vector3
  PhongShader: Shader
}

type Msg =
  | Tick of GameTime
  | InputMapped of ActionState<GameAction>

// -------------------------------------------------------------
// Init
// -------------------------------------------------------------

let init(ctx: GameContext) =
  let inputMap =
    InputMap.empty
    |> InputMap.key MoveLeft KeyboardKey.A
    |> InputMap.key MoveLeft KeyboardKey.Left
    |> InputMap.key MoveRight KeyboardKey.D
    |> InputMap.key MoveRight KeyboardKey.Right
    |> InputMap.key MoveForward KeyboardKey.W
    |> InputMap.key MoveForward KeyboardKey.Up
    |> InputMap.key MoveBackward KeyboardKey.S
    |> InputMap.key MoveBackward KeyboardKey.Down
    |> InputMap.key Jump KeyboardKey.Space

  // Pre-load all models and cache their local bounds
  let localBoundsCache =
    System.Collections.Generic.Dictionary<string, BoundingBox>()

  let modelCache = System.Collections.Generic.Dictionary<string, Model>()

  let getModel path =
    if not(modelCache.ContainsKey(path)) then
      modelCache[path] <- ctx.Assets.Model(path)

    modelCache[path]

  let getLocalBounds path =
    if not(localBoundsCache.ContainsKey(path)) then
      let model = getModel path
      localBoundsCache[path] <- Raylib.GetModelBoundingBox(model)

    localBoundsCache[path]

  let playerModel = getModel "assets/Models/Platform/ball_blue.obj"
  let playerLocalBounds = getLocalBounds "assets/Models/Platform/ball_blue.obj"
  let playerRadius = (playerLocalBounds.Max.Y - playerLocalBounds.Min.Y) * 0.5f

  let playerCenterOffset =
    (playerLocalBounds.Min + playerLocalBounds.Max) * 0.5f

  let platforms, torches = generateLevel getLocalBounds
  let phongShader = loadPhong3DShader()

  // Patch all model materials to use the Phong lighting shader.
  // DrawModel overrides BeginShaderMode with the model's own material
  // shader, so we must replace it directly on each loaded model.
  ModelHelper.setMaterialShader playerModel phongShader

  let uniqueModelPaths =
    platforms |> List.map(fun p -> p.ModelPath) |> List.distinct

  for path in uniqueModelPaths do
    let m = getModel path
    ModelHelper.setMaterialShader m phongShader

  // Debug: verify patching worked
  let mutable testMat = NativePtr.read playerModel.Materials

  printfn
    "DEBUG: Phong shader ID = %d, Player material shader ID = %d"
    phongShader.Id
    testMat.Shader.Id

  let startPos = Vector3(24.0f, 5.0f, 24.0f)
  let cameraOffset = Vector3(12.0f, 12.0f, 12.0f)

  struct ({
            PlayerPosition = startPos
            PlayerVelocity = Vector3.Zero
            TotalRollX = 0.0f
            TotalRollZ = 0.0f
            IsGrounded = false
            CameraPosition = startPos + cameraOffset
            CameraTarget = startPos
            Actions = ActionState.empty
            InputMap = inputMap
            Platforms = platforms
            Torches = torches
            TotalTime = 0.0f
            DayNight = DayNight.initial
            PlayerModel = playerModel
            PlayerRadius = playerRadius
            PlayerCenterOffset = playerCenterOffset
            PhongShader = phongShader
          },
          Cmd.none)

// -------------------------------------------------------------
// Update
// -------------------------------------------------------------

let update (msg: Msg) (state: GameState) =
  match msg with
  | InputMapped actions -> struct ({ state with Actions = actions }, Cmd.none)
  | Tick gameTime ->
    let dt = float32 gameTime.ElapsedGameTime.TotalSeconds
    let actions = Keyboard.poll state.InputMap state.Actions

    // Jump
    let velocity =
      if state.IsGrounded && actions.Started.Contains(Jump) then
        Vector3(state.PlayerVelocity.X, jumpSpeed, state.PlayerVelocity.Z)
      else
        state.PlayerVelocity

    // Gravity
    let velocity = Vector3(velocity.X, velocity.Y + gravity * dt, velocity.Z)

    // Camera-relative movement
    let moveDir =
      computeCameraRelativeMove actions state.CameraPosition state.CameraTarget

    let velocity = applyMovement dt moveDir velocity

    // Integrate position
    let prevPos = state.PlayerPosition
    let newPos = prevPos + velocity * dt

    // Resolve collision
    let struct (finalPos, finalVel, grounded) =
      resolveCollision
        prevPos
        newPos
        velocity
        state.PlayerRadius
        state.Platforms

    // Fall limit / respawn
    let mutable finalPos = finalPos
    let mutable finalVel = finalVel
    let mutable grounded = grounded

    if finalPos.Y <= fallLimit then
      finalPos <- Vector3(24.0f, 5.0f, 24.0f)
      finalVel <- Vector3.Zero
      grounded <- false

    // Camera follow
    let targetCamPos = finalPos + Vector3(12.0f, 12.0f, 12.0f)
    let targetCamTarget = finalPos
    let lerpFactor = 1.0f - MathF.Exp(-dt * 5.0f)
    let newCamPos = Vector3.Lerp(state.CameraPosition, targetCamPos, lerpFactor)

    let newCamTarget =
      Vector3.Lerp(state.CameraTarget, targetCamTarget, lerpFactor)

    // Ball roll
    let totalRollX = state.TotalRollX + finalVel.Z * dt * rollSpeed
    let totalRollZ = state.TotalRollZ - finalVel.X * dt * rollSpeed

    // Day/night
    let dayNight = DayNight.update dt state.DayNight

    struct ({
              state with
                  PlayerPosition = finalPos
                  PlayerVelocity = finalVel
                  TotalRollX = totalRollX
                  TotalRollZ = totalRollZ
                  IsGrounded = grounded
                  CameraPosition = newCamPos
                  CameraTarget = newCamTarget
                  Actions = actions
                  TotalTime = state.TotalTime + dt
                  DayNight = dayNight
            },
            Cmd.none)

// -------------------------------------------------------------
// View
// -------------------------------------------------------------

let view (ctx: GameContext) (state: GameState) (buffer: RenderBuffer3D) =
  let time = state.DayNight.TimeOfDay

  // Sky background changes with time of day — very obvious visual cue
  buffer.Add(
    -2000<RenderLayer3D>,
    SetBackground3D(DayNight.getBackgroundColor time)
  )

  let camera =
    RaylibHelpers.createCamera3D
      state.CameraPosition
      state.CameraTarget
      Vector3.UnitY
      45.0f

  buffer.Add(0<RenderLayer3D>, SetCamera3D camera)

  // Per-platform grids (default shader)
  for platform in state.Platforms do
    drawPlatformGrid state.PlayerPosition platform buffer

  // Activate Phong lighting shader
  buffer.Add(5<RenderLayer3D>, SetShader3D state.PhongShader)

  // Ambient light
  let ambientColor, ambientIntensity = DayNight.getAmbient time

  buffer.Add(
    6<RenderLayer3D>,
    SetAmbient3D {
      Color = ambientColor
      Intensity = ambientIntensity
    }
  )

  // Directional light (sun) — arcs across sky, shadows move
  let sunIntensity = DayNight.getSunIntensity time

  if sunIntensity > 0.01f then
    buffer.Add(
      7<RenderLayer3D>,
      AddDirectionalLight3D {
        Direction = DayNight.getSunDirection time
        Color = Color(255uy, 245uy, 210uy)
        Intensity = 1.2f * sunIntensity
      }
    )

  // Directional light (moon) — cool blue, much weaker
  let moonIntensity = DayNight.getMoonIntensity time

  if moonIntensity > 0.01f then
    buffer.Add(
      7<RenderLayer3D>,
      AddDirectionalLight3D {
        Direction = DayNight.getMoonDirection time
        Color = Color(120uy, 150uy, 255uy)
        Intensity = 0.25f * moonIntensity
      }
    )

  // Torch point lights — warm orange, 12-unit radius
  for torch in state.Torches do
    buffer.Add(
      8<RenderLayer3D>,
      AddPointLight3D {
        Position = torch.Position
        Color = torch.Color
        Radius = torch.Radius
      }
    )

  // Platforms
  for platform in state.Platforms do
    let pModel = ctx.Assets.Model(platform.ModelPath)

    buffer.Add(
      10<RenderLayer3D>,
      DrawModel(pModel, platform.Position, 1.0f, Color.White)
    )

  // Player ball
  let drawPos = state.PlayerPosition - state.PlayerCenterOffset

  let rollQ =
    Quaternion.CreateFromYawPitchRoll(0.0f, state.TotalRollX, state.TotalRollZ)

  let axis, angle = quatToAxisAngle rollQ

  buffer.Add(
    20<RenderLayer3D>,
    DrawModelEx(
      state.PlayerModel,
      drawPos,
      axis,
      angle,
      Vector3.One,
      Color.White
    )
  )

  buffer.Add(999<RenderLayer3D>, ResetShader3D)
  buffer.Add(1000<RenderLayer3D>, ResetCamera3D)

// -------------------------------------------------------------
// Entry Point
// -------------------------------------------------------------

let subscribe _ctx _state = Sub.none

[<EntryPoint>]
let main _ =
  let program =
    Program.mkProgram init update
    |> Program.withConfig(fun cfg ->
      cfg.Width <- 1280
      cfg.Height <- 720
      cfg.Title <- "Mibo Raylib 3D Platformer"
      cfg.TargetFPS <- 60)
    |> Program.withTick Tick
    |> Program.withRenderer(fun () -> Batch3DRenderer.create view)

  let game = new RaylibGame<GameState, Msg>(program)
  game.Run()
  0
