#nowarn "3391"

module PlatformerSample.Program

open System.Numerics
open Raylib_cs
open Mibo.Elmish
open Mibo.Elmish.Graphics2D

[<Measure>]
type EntityId

type Model = {
    PlayerPosition: Vector2
    CameraX: float32
    PlayerAssets: Texture2D
    TerrainAssets: Texture2D
    Font: Font
}

type Msg =
    | Tick of GameTime

let init (ctx: GameContext) =
    let playerTex = ctx.Assets.Texture("assets/kenney_platformer/Spritesheets/spritesheet-characters-default.png")
    let tileTex = ctx.Assets.Texture("assets/kenney_platformer/Spritesheets/spritesheet-tiles-default.png")
    let font = ctx.Assets.Font("assets/Fonts/monogram.ttf")

    struct (
        {
            PlayerPosition = Vector2(200.0f, 576.0f)
            CameraX = 0.0f
            PlayerAssets = playerTex
            TerrainAssets = tileTex
            Font = font
        },
        Cmd.none
    )

let update (msg: Msg) (model: Model) =
    match msg with
    | Tick _ -> struct (model, Cmd.none)

let r x y w h =
    Raylib_cs.Rectangle(float32 x, float32 y, float32 w, float32 h)

let view (ctx: GameContext) (model: Model) (buffer: RenderBuffer<RenderCmd2D>) =
    buffer.Add(
        0<RenderLayer>,
        SetCamera2D {
            Position = Vector2(model.CameraX + float32 ctx.WindowWidth / 2.0f, float32 ctx.WindowHeight / 2.0f)
            Zoom = 1.0f
            Layer = 0<RenderLayer>
        }
    )

    for i = 0 to 20 do
        let dest = r (i * 64) (12 * 64 - 64) 64 64
        let src = r 260 585 64 64
        buffer.Add(
            1<RenderLayer>,
            DrawSprite {
                Texture = model.TerrainAssets
                Dest = dest
                Source = src
                Origin = Vector2.Zero
                Rotation = 0.0f
                Color = Color.White
                Layer = 1<RenderLayer>
            }
        )

    let playerSrc = r 645 0 128 128
    let playerDest = r (int model.PlayerPosition.X) (int model.PlayerPosition.Y) 64 64
    buffer.Add(
        2<RenderLayer>,
        DrawSprite {
            Texture = model.PlayerAssets
            Dest = playerDest
            Source = playerSrc
            Origin = Vector2.Zero
            Rotation = 0.0f
            Color = Color.White
            Layer = 2<RenderLayer>
        }
    )

    buffer.Add(
        1000<RenderLayer>,
        ResetCamera2D
    )

    buffer.Add(
        1001<RenderLayer>,
        DrawText {
            Font = model.Font
            Text = "Mibo Raylib MVP"
            Position = Vector2(10.0f, 10.0f)
            FontSize = 32.0f
            Spacing = 1.0f
            Color = Color.White
            Layer = 1001<RenderLayer>
        }
    )

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
