namespace Mibo.Elmish.Graphics2D

open System
open System.Numerics
open Raylib_cs
open Mibo.Elmish

[<Measure>]
type RenderLayer

type SpriteState =
    { Texture: Texture2D
      Dest: Raylib_cs.Rectangle
      Source: Raylib_cs.Rectangle
      Origin: Vector2
      Rotation: float32
      Color: Color
      Layer: int<RenderLayer> }

type TextState =
    { Font: Font
      Text: string
      Position: Vector2
      FontSize: float32
      Spacing: float32
      Color: Color
      Layer: int<RenderLayer> }

type Camera2DState =
    { Position: Vector2
      Zoom: float32
      Layer: int<RenderLayer> }

type ShaderState =
    { Shader: Shader
      Layer: int<RenderLayer> }

type PostProcessConfig =
    { Shader: Shader
      TintColor: Color
      TintAmount: float32 }

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

type Batch2DRenderer<'Model>
    (view: GameContext -> 'Model -> RenderBuffer<RenderCmd2D> -> unit, postProcess: PostProcessConfig option) =
    let buffer = RenderBuffer<RenderCmd2D>(capacity = 4096)
    let mutable renderTarget: RenderTexture2D option = None
    let mutable tintColorLoc = -1
    let mutable tintAmountLoc = -1

    let getOrCreateTarget (w: int) (h: int) =
        match renderTarget with
        | Some rt when rt.Texture.Width = w && rt.Texture.Height = h -> rt
        | _ ->
            match renderTarget with
            | Some old -> Raylib.UnloadRenderTexture(old)
            | _ -> ()

            let rt = Raylib.LoadRenderTexture(w, h)
            renderTarget <- Some rt
            rt

    let applyShaderUniforms (cfg: PostProcessConfig) =
        if tintColorLoc < 0 then
            tintColorLoc <- Raylib.GetShaderLocation(cfg.Shader, "tintColor")
            tintAmountLoc <- Raylib.GetShaderLocation(cfg.Shader, "tintAmount")

        let r = float32 cfg.TintColor.R / 255.0f
        let g = float32 cfg.TintColor.G / 255.0f
        let b = float32 cfg.TintColor.B / 255.0f
        let a = float32 cfg.TintColor.A / 255.0f

        let tintVec = Vector4(r, g, b, a)

        Raylib.SetShaderValue(cfg.Shader, tintColorLoc, tintVec, ShaderUniformDataType.Vec4)

        Raylib.SetShaderValue(cfg.Shader, tintAmountLoc, cfg.TintAmount, ShaderUniformDataType.Float)

    let executeCommands (ctx: GameContext) =
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
                Raylib.DrawTexturePro(
                    sprite.Texture,
                    sprite.Source,
                    sprite.Dest,
                    sprite.Origin,
                    sprite.Rotation,
                    sprite.Color
                )
            | _, DrawText text ->
                Raylib.DrawTextEx(text.Font, text.Text, text.Position, text.FontSize, text.Spacing, text.Color)
            | _, DrawRect(rect, color, _) -> Raylib.DrawRectangleRec(rect, color)
            | _, DrawLine(start, finish, color, _) -> Raylib.DrawLineV(start, finish, color)

        if inShader then
            Raylib.EndShaderMode()

        if inCamera then
            Raylib.EndMode2D()

    interface IRenderer<'Model> with
        member _.Draw(ctx, model, gameTime) =
            buffer.Clear()
            view ctx model buffer
            buffer.Sort()

            match postProcess with
            | None -> executeCommands ctx
            | Some cfg ->
                let target = getOrCreateTarget ctx.WindowWidth ctx.WindowHeight
                Raylib.BeginTextureMode(target)
                Raylib.ClearBackground(Color.Black)
                executeCommands ctx
                Raylib.EndTextureMode()

                applyShaderUniforms cfg
                Raylib.BeginShaderMode(cfg.Shader)

                let tw = float32 target.Texture.Width
                let th = float32 target.Texture.Height

                let sourceRect = Raylib_cs.Rectangle(0.0f, 0.0f, tw, -th)

                let destRect =
                    Raylib_cs.Rectangle(0.0f, 0.0f, float32 ctx.WindowWidth, float32 ctx.WindowHeight)

                Raylib.DrawTexturePro(target.Texture, sourceRect, destRect, Vector2.Zero, 0.0f, Color.White)
                Raylib.EndShaderMode()

    interface IDisposable with
        member _.Dispose() =
            match renderTarget with
            | Some rt -> Raylib.UnloadRenderTexture(rt)
            | _ -> ()

module Batch2DRenderer =
    let create view =
        new Batch2DRenderer<'Model>(view, None) :> IRenderer<'Model>

    let createWithPostProcess postProcess view =
        new Batch2DRenderer<'Model>(view, Some postProcess) :> IRenderer<'Model>
