module PlatformerSample.Program

open System
open System.Numerics
open Raylib_cs
open Mibo.Elmish
open Mibo.Elmish.Graphics2D

[<Measure>]
type EntityId

// ─────────────────────────────────────────────────────────────
// Game Actions
// ─────────────────────────────────────────────────────────────

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

// ─────────────────────────────────────────────────────────────
// Physics Constants
// ─────────────────────────────────────────────────────────────

let tileSize = 64.0f
let worldHeight = 12.0f
let groundLevel = worldHeight * tileSize
let groundSurface = groundLevel - tileSize
let playerWidth = 40.0f
let playerHeight = 54.0f
let gravity = 1200.0f
let moveSpeed = 300.0f
let jumpSpeed = -700.0f

// ─────────────────────────────────────────────────────────────
// Terrain
// ─────────────────────────────────────────────────────────────

[<Struct>]
type Platform = { Bounds: Raylib_cs.Rectangle }

let generateTerrain (seed: int) : Platform list =
    let rng = Random(seed)
    let mutable platforms = []
    let mutable x = 0.0f

    // Ground segments with gaps
    while x < 5000.0f do
        let segmentLength = rng.Next(3, 8) |> float32 |> (*) tileSize

        platforms <-
            { Bounds = Raylib_cs.Rectangle(x, groundSurface, segmentLength, tileSize) }
            :: platforms

        x <- x + segmentLength + (rng.Next(2, 5) |> float32 |> (*) tileSize)

    // Elevated platforms
    for _ = 1 to 10 do
        let px = rng.Next(200, 4800) |> float32
        let pw = rng.Next(2, 5) |> float32 |> (*) tileSize
        let py = groundSurface - (rng.Next(2, 5) |> float32 |> (*) tileSize)
        platforms <- { Bounds = Raylib_cs.Rectangle(px, py, pw, tileSize) } :: platforms

    platforms |> List.rev

// ─────────────────────────────────────────────────────────────
// Model
// ─────────────────────────────────────────────────────────────

type SpriteAssets =
    { PlayerTexture: Texture2D
      TileTexture: Texture2D
      Font: Font
      JumpSound: Sound }

type Model =
    { PlayerPosition: Vector2
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
      Seed: int }

// ─────────────────────────────────────────────────────────────
// Messages
// ─────────────────────────────────────────────────────────────

type Msg =
    | Tick of GameTime
    | InputMapped of ActionState<GameAction>

// ─────────────────────────────────────────────────────────────
// Helpers
// ─────────────────────────────────────────────────────────────

let r (x: int) (y: int) (w: int) (h: int) : Raylib_cs.Rectangle =
    Raylib_cs.Rectangle(float32 x, float32 y, float32 w, float32 h)

let getAnimationState (velocity: Vector2) (isGrounded: bool) : AnimationState =
    if not isGrounded then
        if velocity.Y > 0.0f then Fall else Jump
    elif abs velocity.X > 1.0f then
        Walk
    else
        Idle

let getPlayerSrcRect (totalTime: float32) (state: AnimationState) : Raylib_cs.Rectangle =
    match state with
    | Idle -> r 645 0 128 128
    | Walk ->
        let frame = int (totalTime * 10.0f) % 2
        if frame = 0 then r 0 129 128 128 else r 129 129 128 128
    | Jump -> r 774 0 128 128
    | Fall -> r 774 0 128 128

// ─────────────────────────────────────────────────────────────
// Collision
// ─────────────────────────────────────────────────────────────

let playerBounds (pos: Vector2) : Raylib_cs.Rectangle =
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
            // Vertical resolution: landing on top
            let prevFeetY = prevPos.Y + playerHeight
            let currFeetY = pos.Y + playerHeight
            let platformTop = pb.Y

            let crossedSurface = prevFeetY <= platformTop + 5.0f && currFeetY >= platformTop
            let movingDown = vel.Y >= 0.0f

            if crossedSurface && movingDown then
                pos <- Vector2(pos.X, platformTop - playerHeight)
                vel <- Vector2(vel.X, 0.0f)
                grounded <- true
            elif vel.Y < 0.0f then
                // Hit head
                pos <- Vector2(pos.X, pb.Y + pb.Height)
                vel <- Vector2(vel.X, 0.0f)
            elif vel.X > 0.0f && prevPos.X + playerWidth <= pb.X then
                // Hit left wall
                pos <- Vector2(pb.X - playerWidth, pos.Y)
                vel <- Vector2(0.0f, vel.Y)
            elif vel.X < 0.0f && prevPos.X >= pb.X + pb.Width then
                // Hit right wall
                pos <- Vector2(pb.X + pb.Width, pos.Y)
                vel <- Vector2(0.0f, vel.Y)

    struct (pos, vel, grounded)

// ─────────────────────────────────────────────────────────────
// Init
// ─────────────────────────────────────────────────────────────

let init (ctx: GameContext) =
    let playerTex =
        ctx.Assets.Texture("assets/kenney_platformer/Spritesheets/spritesheet-characters-default.png")

    let tileTex =
        ctx.Assets.Texture("assets/kenney_platformer/Spritesheets/spritesheet-tiles-default.png")

    let font = ctx.Assets.Font("assets/Fonts/monogram.ttf")
    let jumpSound = ctx.Assets.Sound("assets/sfx_jump.ogg")

    let inputMap =
        InputMap.empty
        |> InputMap.key MoveLeft KeyboardKey.A
        |> InputMap.key MoveLeft KeyboardKey.Left
        |> InputMap.key MoveRight KeyboardKey.D
        |> InputMap.key MoveRight KeyboardKey.Right
        |> InputMap.key GameAction.Jump KeyboardKey.Space
        |> InputMap.key Respawn KeyboardKey.R

    let assets =
        { PlayerTexture = playerTex
          TileTexture = tileTex
          Font = font
          JumpSound = jumpSound }

    let seed = Random().Next()
    let platforms = generateTerrain seed
    let spawnY = groundSurface - playerHeight

    struct ({ PlayerPosition = Vector2(200.0f, spawnY)
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
              Seed = seed },
            Cmd.none)

// ─────────────────────────────────────────────────────────────
// Update
// ─────────────────────────────────────────────────────────────

let update (msg: Msg) (model: Model) =
    match msg with
    | InputMapped actions -> struct ({ model with Actions = actions }, Cmd.none)
    | Tick gameTime ->
        let dt = float32 gameTime.ElapsedGameTime.TotalSeconds

        // ── Input polling ──
        let actions = Keyboard.poll model.InputMap model.Actions

        // ── Horizontal movement ──
        let moveDir =
            if actions.Held.Contains(MoveLeft) then -1.0f
            elif actions.Held.Contains(MoveRight) then 1.0f
            else 0.0f

        // ── Jump ──
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

        // ── Integrate position ──
        let prevPos = model.PlayerPosition
        let newPos = prevPos + velocity * dt

        // ── Platform collision ──
        let struct (finalPos, finalVel, isGrounded) =
            resolvePlatformCollision prevPos newPos velocity model.Platforms

        // ── Fall off world ──
        let mutable finalPos = finalPos
        let mutable finalVel = finalVel
        let mutable isGrounded = isGrounded

        if finalPos.Y > groundLevel + 200.0f then
            finalPos <- Vector2(200.0f, groundSurface - playerHeight)
            finalVel <- Vector2.Zero
            isGrounded <- true

        // ── Respawn ──
        if actions.Started.Contains(Respawn) then
            finalPos <- Vector2(200.0f, groundSurface - playerHeight)
            finalVel <- Vector2.Zero
            isGrounded <- true

        // ── Constrain to left edge ──
        if finalPos.X < 0.0f then
            finalPos <- Vector2(0.0f, finalPos.Y)

        // ── Facing ──
        let newFacing =
            if moveDir < 0.0f then -1.0f
            elif moveDir > 0.0f then 1.0f
            else model.PlayerFacing

        // ── Camera follow ──
        let viewportWidth = 1280.0f
        let targetCameraX = finalPos.X - viewportWidth * 0.3f
        let cameraX = Math.Max(0.0f, targetCameraX)

        // ── Animation state ──
        let animState = getAnimationState finalVel isGrounded

        // ── Play sound ──
        if playedJumpSound then
            Raylib.PlaySound(model.Assets.JumpSound)

        struct ({ model with
                    PlayerPosition = finalPos
                    PlayerVelocity = finalVel
                    PlayerFacing = newFacing
                    IsGrounded = isGrounded
                    CameraX = cameraX
                    Actions = actions
                    TotalTime = model.TotalTime + dt
                    AnimationState = animState },
                Cmd.none)

// ─────────────────────────────────────────────────────────────
// View
// ─────────────────────────────────────────────────────────────

let view (ctx: GameContext) (model: Model) (buffer: RenderBuffer<RenderCmd2D>) =
    // World camera
    let cameraCenterX = model.CameraX + float32 ctx.WindowWidth / 2.0f
    let cameraCenterY = groundLevel - float32 ctx.WindowHeight / 2.0f

    buffer.Add(
        0<RenderLayer>,
        SetCamera2D
            { Position = Vector2(cameraCenterX, cameraCenterY)
              Zoom = 1.0f
              Layer = 0<RenderLayer> }
    )

    // Platforms
    let tileSrc = r 260 585 64 64

    for platform in model.Platforms do
        let pb = platform.Bounds
        let tileCount = int (pb.Width / tileSize)

        for i = 0 to tileCount - 1 do
            let dest =
                r (int (pb.X) + i * int tileSize) (int pb.Y) (int tileSize) (int tileSize)

            buffer.Add(
                1<RenderLayer>,
                DrawSprite
                    { Texture = model.Assets.TileTexture
                      Dest = dest
                      Source = tileSrc
                      Origin = Vector2.Zero
                      Rotation = 0.0f
                      Color = Color.White
                      Layer = 1<RenderLayer> }
            )

    // Player sprite
    let playerSrc = getPlayerSrcRect model.TotalTime model.AnimationState
    let mutable playerSrcMut = playerSrc

    if model.PlayerFacing < 0.0f then
        playerSrcMut <- Raylib_cs.Rectangle(playerSrcMut.X, playerSrcMut.Y, -playerSrcMut.Width, playerSrcMut.Height)

    let playerDrawY = int (model.PlayerPosition.Y + playerHeight - 64.0f)
    let playerDest = r (int model.PlayerPosition.X) playerDrawY 64 64

    buffer.Add(
        2<RenderLayer>,
        DrawSprite
            { Texture = model.Assets.PlayerTexture
              Dest = playerDest
              Source = playerSrcMut
              Origin = Vector2.Zero
              Rotation = 0.0f
              Color = Color.White
              Layer = 2<RenderLayer> }
    )

    // UI camera
    buffer.Add(1000<RenderLayer>, ResetCamera2D)

    buffer.Add(
        1001<RenderLayer>,
        DrawText
            { Font = model.Assets.Font
              Text = "Mibo Raylib MVP - WASD/Arrows: Move, Space: Jump, R: Respawn"
              Position = Vector2(10.0f, 10.0f)
              FontSize = 20.0f
              Spacing = 1.0f
              Color = Color.White
              Layer = 1001<RenderLayer> }
    )

// ─────────────────────────────────────────────────────────────
// Entry Point
// ─────────────────────────────────────────────────────────────

let subscribe _ctx _model = Sub.none

[<EntryPoint>]
let main _ =
    let program =
        Program.mkProgram init update
        |> Program.withConfig (fun cfg ->
            cfg.Width <- 1280
            cfg.Height <- 720
            cfg.Title <- "Mibo Raylib MVP"
            cfg.TargetFPS <- 60)
        |> Program.withTick Tick
        |> Program.withRenderer (fun () ->
            let shader = DefaultShaders.loadSepiaTintShader ()

            let postProcessConfig =
                { Shader = shader
                  TintColor = Color(112uy, 66uy, 20uy, 128uy)
                  TintAmount = 0.15f }

            Batch2DRenderer.createWithPostProcess postProcessConfig view)

    let game = new RaylibGame<Model, Msg>(program)
    game.Run()
    0
