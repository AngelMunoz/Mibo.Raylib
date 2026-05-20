namespace Mibo.Elmish.Graphics2D

open System
open System.Numerics
open Raylib_cs
open Mibo.Elmish

[<Measure>]
type RenderLayer

type SpriteState = {
    Texture: Texture2D
    Dest: Raylib_cs.Rectangle
    Source: Raylib_cs.Rectangle
    Origin: Vector2
    Rotation: float32
    Color: Color
    Layer: int<RenderLayer>
}

type TextState = {
    Font: Font
    Text: string
    Position: Vector2
    FontSize: float32
    Spacing: float32
    Color: Color
    Layer: int<RenderLayer>
}

type Camera2DState = {
    Position: Vector2
    Zoom: float32
    Layer: int<RenderLayer>
}

type ShaderState = {
    Shader: Shader
    Layer: int<RenderLayer>
}

[<Struct>]
type RenderCmd2D =
    | DrawSprite of sprite: SpriteState
    | DrawText of text: TextState
    | SetCamera2D of camera: Camera2DState
    | ResetCamera2D
    | SetShader of shader: ShaderState
    | ResetShader
    | DrawRect of rect: Raylib_cs.Rectangle * color: Color * layer: int<RenderLayer>
    | DrawLine of start: Vector2 * finish: Vector2 * color: Color * layer: int<RenderLayer>

type RenderBuffer<'Cmd> = Mibo.Elmish.RenderBuffer<int<RenderLayer>, 'Cmd>

type Batch2DRenderer<'Model>(view: GameContext -> 'Model -> RenderBuffer<RenderCmd2D> -> unit) =
    let buffer = RenderBuffer<RenderCmd2D>(capacity=4096)
    interface IRenderer<'Model> with
        member _.Draw(ctx, model, gameTime) =
            buffer.Clear()
            view ctx model buffer
            buffer.Sort()

            let mutable inCamera = false
            let mutable inShader = false

            for i = 0 to buffer.Count - 1 do
                match buffer.Item(i) with
                | _, SetCamera2D cam ->
                    let mutable c = Camera2D()
                    c.Target <- cam.Position
                    c.Offset <- Vector2(float32 ctx.WindowWidth / 2.0f, float32 ctx.WindowHeight / 2.0f)
                    c.Rotation <- 0.0f
                    c.Zoom <- cam.Zoom
                    Raylib.BeginMode2D(c)
                    inCamera <- true
                | _, ResetCamera2D ->
                    if inCamera then
                        Raylib.EndMode2D()
                        inCamera <- false
                | _, SetShader s ->
                    Raylib.BeginShaderMode(s.Shader)
                    inShader <- true
                | _, ResetShader ->
                    if inShader then
                        Raylib.EndShaderMode()
                        inShader <- false
                | _, DrawSprite sprite ->
                    Raylib.DrawTexturePro(sprite.Texture, sprite.Source, sprite.Dest, sprite.Origin, sprite.Rotation, sprite.Color)
                | _, DrawText text ->
                    Raylib.DrawTextEx(text.Font, text.Text, text.Position, text.FontSize, text.Spacing, text.Color)
                | _, DrawRect(rect, color, _) ->
                    Raylib.DrawRectangleRec(rect, color)
                | _, DrawLine(start, finish, color, _) ->
                    Raylib.DrawLineV(start, finish, color)

            if inShader then Raylib.EndShaderMode()
            if inCamera then Raylib.EndMode2D()

module Batch2DRenderer =
    let create view = new Batch2DRenderer<'Model>(view) :> IRenderer<'Model>
