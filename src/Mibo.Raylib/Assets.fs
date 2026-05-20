namespace Mibo.Elmish

open System
open System.Collections.Generic
open Raylib_cs

type IAssets =
    abstract Texture: path: string -> Texture2D
    abstract Font: path: string -> Font
    abstract Dispose: unit -> unit

type AssetsService() =
    let textures = Dictionary<string, Texture2D>()
    let fonts = Dictionary<string, Font>()

    interface IAssets with
        member _.Texture(path) =
            match textures.TryGetValue(path) with
            | true, tex -> tex
            | _ ->
                let tex = Raylib.LoadTexture(path)
                textures.Add(path, tex)
                tex

        member _.Font(path) =
            match fonts.TryGetValue(path) with
            | true, font -> font
            | _ ->
                let font = Raylib.LoadFont(path)
                fonts.Add(path, font)
                font

        member _.Dispose() =
            for kv in textures do
                Raylib.UnloadTexture(kv.Value)
            textures.Clear()

            for kv in fonts do
                Raylib.UnloadFont(kv.Value)
            fonts.Clear()

module AssetsService =
    let create() = new AssetsService() :> IAssets
