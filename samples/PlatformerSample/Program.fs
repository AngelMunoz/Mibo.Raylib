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
// Model
// ─────────────────────────────────────────────────────────────

type SpriteAssets = {
    PlayerTexture: Texture2D
    TileTexture: Texture2D
    Font: Font
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
}

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
        let frame = int(totalTime * 10.0f) % 2
        if frame = 0 then r 0 129 128 128 else r 129 129 128 128
    | Jump -> r 774 0 128 128
    | Fall -> r 774 0 128 128

// ─────────────────────────────────────────────────────────────
// Init
// ─────────────────────────────────────────────────────────────

let init (ctx: GameContext) =
    let playerTex = ctx.Assets.Texture("assets/kenney_platformer/Spritesheets/spritesheet-characters-default.png")
    let tileTex = ctx.Assets.Texture("assets/kenney_platformer/Spritesheets/spritesheet-tiles-default.png")
    let font = ctx.Assets.Font("assets/Fonts/monogram.ttf")

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
        Font = font
    }

    let spawnY = groundSurface - playerHeight

    struct (
        {
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
        },
        Cmd.none
    )

// ─────────────────────────────────────────────────────────────
// Update
// ─────────────────────────────────────────────────────────────

let update (msg: Msg) (model: Model) =
    match msg with
    | InputMapped actions ->
        struct ({ model with Actions = actions }, Cmd.none)
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

        let velocityY =
            if jumpPressed && canJump then
                jumpSpeed
            else
                model.PlayerVelocity.Y + gravity * dt

        let velocityX = moveDir * moveSpeed
        let velocity = Vector2(velocityX, velocityY)

        // ── Integrate position ──
        let newPos = model.PlayerPosition + velocity * dt

        // ── Ground collision ──
        let mutable finalPos = newPos
        let mutable finalVel = velocity
        let mutable isGrounded = false

        if finalPos.Y >= groundSurface - playerHeight then
            finalPos <- Vector2(finalPos.X, groundSurface - playerHeight)
            finalVel <- Vector2(finalVel.X, 0.0f)
            isGrounded <- true

        // ── Respawn ──
        let mutable finalPos = finalPos
        if actions.Started.Contains(Respawn) then
            finalPos <- Vector2(200.0f, groundSurface - playerHeight)
            finalVel <- Vector2.Zero
            isGrounded <- true

        // ── Constrain to left edge ──
        if finalPos.X < model.CameraX then
            finalPos <- Vector2(model.CameraX, finalPos.Y)

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

        struct (
            { model with
                PlayerPosition = finalPos
                PlayerVelocity = finalVel
                PlayerFacing = newFacing
                IsGrounded = isGrounded
                CameraX = cameraX
                Actions = actions
                TotalTime = model.TotalTime + dt
                AnimationState = animState
            },
            Cmd.none
        )

// ─────────────────────────────────────────────────────────────
// View
// ─────────────────────────────────────────────────────────────

let view (ctx: GameContext) (model: Model) (buffer: RenderBuffer<RenderCmd2D>) =
    // World camera: center horizontally on player, align ground with bottom of screen
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

    // Ground tiles
    let firstTile = int(model.CameraX / tileSize) - 1
    let lastTile = firstTile + int(float32 ctx.WindowWidth / tileSize) + 2

    for i = firstTile to lastTile do
        let dest = r (i * int tileSize) (int groundLevel - int tileSize) (int tileSize) (int tileSize)
        let src = r 260 585 64 64
        buffer.Add(
            1<RenderLayer>,
            DrawSprite {
                Texture = model.Assets.TileTexture
                Dest = dest
                Source = src
                Origin = Vector2.Zero
                Rotation = 0.0f
                Color = Color.White
                Layer = 1<RenderLayer>
            }
        )

    // Player sprite
    let playerSrc = getPlayerSrcRect model.TotalTime model.AnimationState
    let mutable playerSrcMut = playerSrc
    if model.PlayerFacing < 0.0f then
        playerSrcMut <- Raylib_cs.Rectangle(playerSrcMut.X, playerSrcMut.Y, -playerSrcMut.Width, playerSrcMut.Height)

    // Align sprite bottom with collision box bottom (playerHeight = 54, sprite = 64)
    let playerDrawY = int(model.PlayerPosition.Y + playerHeight - 64.0f)
    let playerDest = r (int model.PlayerPosition.X) playerDrawY 64 64

    buffer.Add(
        2<RenderLayer>,
        DrawSprite {
            Texture = model.Assets.PlayerTexture
            Dest = playerDest
            Source = playerSrcMut
            Origin = Vector2.Zero
            Rotation = 0.0f
            Color = Color.White
            Layer = 2<RenderLayer>
        }
    )

    // UI camera
    buffer.Add(
        1000<RenderLayer>,
        ResetCamera2D
    )

    buffer.Add(
        1001<RenderLayer>,
        DrawText {
            Font = model.Assets.Font
            Text = "Mibo Raylib MVP - WASD/Arrows: Move, Space: Jump, R: Respawn"
            Position = Vector2(10.0f, 10.0f)
            FontSize = 20.0f
            Spacing = 1.0f
            Color = Color.White
            Layer = 1001<RenderLayer>
        }
    )

// ─────────────────────────────────────────────────────────────
// Entry Point
// ─────────────────────────────────────────────────────────────

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
