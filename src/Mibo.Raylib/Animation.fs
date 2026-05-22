namespace Mibo.Animation

open System
open System.Collections.Generic
open System.Numerics
open Raylib_cs
open Mibo.Elmish

[<Struct>]
type Point = {
  X: int
  Y: int
}
with
  static member Zero = { X = 0; Y = 0 }

[<Struct>]
type Animation = {
  Frames: Rectangle[]
  FrameDuration: float32
  Loop: bool
}

[<Struct>]
type GridAnimationDef = {
  Name: string
  Row: int
  StartCol: int
  FrameCount: int
  Fps: float32
  Loop: bool
}

type SpriteSheet = {
  Texture: Texture2D
  NormalMap: Texture2D voption
  Animations: IReadOnlyDictionary<string, Animation>
  AnimationsByIndex: Animation[]
  AnimationIndices: IReadOnlyDictionary<string, int>
  Origin: Vector2
  FrameSize: Point
}

[<Struct>]
type AnimatedSprite = {
  Sheet: SpriteSheet
  AnimationIndex: int
  CurrentFrame: int
  TimeInFrame: float32
  Finished: bool
  FlipX: bool
  FlipY: bool
  Color: Color
  Scale: float32
  Rotation: float32
}

module Animation =
  let inline duration(anim: Animation) =
    float32 anim.Frames.Length * anim.FrameDuration

module SpriteSheet =
  let fromFrames
    (texture: Texture2D)
    (origin: Vector2)
    (animations: struct (string * Animation)[])
    : SpriteSheet =
    let dict = Dictionary<string, Animation>(animations.Length)
    let indices = Dictionary<string, int>(animations.Length)
    let arr = Array.zeroCreate<Animation> animations.Length

    for i = 0 to animations.Length - 1 do
      let struct (name, anim) = animations.[i]
      dict.[name] <- anim
      indices.[name] <- i
      arr.[i] <- anim

    let frameSize =
      if animations.Length > 0 then
        let struct (_, firstAnim) = animations.[0]

        if firstAnim.Frames.Length > 0 then
          let f = firstAnim.Frames.[0]
          { X = int f.Width; Y = int f.Height }
        else
          { X = 0; Y = 0 }
      else
        { X = 0; Y = 0 }

    {
      Texture = texture
      NormalMap = ValueNone
      Origin = origin
      Animations = dict
      AnimationsByIndex = arr
      AnimationIndices = indices
      FrameSize = frameSize
    }

  let withNormalMap (nm: Texture2D) (sheet: SpriteSheet) = {
    sheet with
        NormalMap = ValueSome nm
  }

  let fromGrid
    (texture: Texture2D)
    (frameWidth: int)
    (frameHeight: int)
    (columns: int)
    (animations: GridAnimationDef[])
    : SpriteSheet =
    let origin = Vector2(float32 frameWidth / 2.0f, float32 frameHeight / 2.0f)

    let dict = Dictionary<string, Animation>(animations.Length)
    let indices = Dictionary<string, int>(animations.Length)
    let arr = Array.zeroCreate<Animation> animations.Length

    for i = 0 to animations.Length - 1 do
      let def = animations[i]
      let frames = Array.zeroCreate<Rectangle> def.FrameCount

      for j = 0 to def.FrameCount - 1 do
        let col = (def.StartCol + j) % columns
        let actualRow = def.Row + (def.StartCol + j) / columns

        frames[j] <-
          Rectangle(
            float32(col * frameWidth),
            float32(actualRow * frameHeight),
            float32 frameWidth,
            float32 frameHeight
          )

      let anim = {
        Frames = frames
        FrameDuration = 1.0f / def.Fps
        Loop = def.Loop
      }

      dict[def.Name] <- anim
      indices[def.Name] <- i
      arr[i] <- anim

    {
      Texture = texture
      NormalMap = ValueNone
      Origin = origin
      Animations = dict
      AnimationsByIndex = arr
      AnimationIndices = indices
      FrameSize = { X = frameWidth; Y = frameHeight }
    }

  let single
    (texture: Texture2D)
    (frames: Rectangle[])
    (fps: float32)
    (loop: bool)
    : SpriteSheet =
    let origin =
      if frames.Length > 0 then
        Vector2(frames.[0].Width / 2.0f, frames.[0].Height / 2.0f)
      else
        Vector2.Zero

    let frameSize =
      if frames.Length > 0 then
        { X = int frames.[0].Width; Y = int frames.[0].Height }
      else
        { X = 0; Y = 0 }

    let anim = {
      Frames = frames
      FrameDuration = 1.0f / fps
      Loop = loop
    }

    let dict = Dictionary<string, Animation>(1)
    dict.["default"] <- anim
    let indices = Dictionary<string, int>(1)
    indices.["default"] <- 0

    {
      Texture = texture
      NormalMap = ValueNone
      Origin = origin
      Animations = dict
      AnimationsByIndex = [| anim |]
      AnimationIndices = indices
      FrameSize = frameSize
    }

  let static' (texture: Texture2D) (sourceRect: Rectangle) : SpriteSheet =
    single texture [| sourceRect |] 1.0f false

  let inline tryGetAnimationIndex
    (name: string)
    (sheet: SpriteSheet)
    : int voption =
    match sheet.AnimationIndices.TryGetValue(name) with
    | true, idx -> ValueSome idx
    | false, _ -> ValueNone

  let animationNames(sheet: SpriteSheet) : string seq =
    sheet.AnimationIndices.Keys

module AnimatedSprite =

  let create (sheet: SpriteSheet) (animationName: string) : AnimatedSprite =
    let idx =
      match sheet.AnimationIndices.TryGetValue(animationName) with
      | true, i -> i
      | false, _ -> 0

    {
      Sheet = sheet
      AnimationIndex = idx
      CurrentFrame = 0
      TimeInFrame = 0.0f
      Finished = false
      FlipX = false
      FlipY = false
      Color = Color.White
      Scale = 1.0f
      Rotation = 0.0f
    }

  let createWith
    (sheet: SpriteSheet)
    (animationName: string)
    (color: Color)
    (scale: float32)
    : AnimatedSprite =
    {
      create sheet animationName with
          Color = color
          Scale = scale
    }

  let play (animationName: string) (sprite: AnimatedSprite) : AnimatedSprite =
    match sprite.Sheet.AnimationIndices.TryGetValue(animationName) with
    | false, _ -> sprite
    | true, idx when idx = sprite.AnimationIndex && not sprite.Finished ->
      sprite
    | true, idx ->
        {
          sprite with
              AnimationIndex = idx
              CurrentFrame = 0
              TimeInFrame = 0.0f
              Finished = false
        }

  let playByIndex (animIndex: int) (sprite: AnimatedSprite) : AnimatedSprite =
    if animIndex = sprite.AnimationIndex && not sprite.Finished then
      sprite
    elif
      animIndex < 0 || animIndex >= sprite.Sheet.AnimationsByIndex.Length
    then
      sprite
    else
      {
        sprite with
            AnimationIndex = animIndex
            CurrentFrame = 0
            TimeInFrame = 0.0f
            Finished = false
      }

  let playIfNot
    (animationName: string)
    (sprite: AnimatedSprite)
    : AnimatedSprite =
    match sprite.Sheet.AnimationIndices.TryGetValue(animationName) with
    | true, idx when idx = sprite.AnimationIndex -> sprite
    | true, _ -> play animationName sprite
    | false, _ -> sprite

  let restart(sprite: AnimatedSprite) : AnimatedSprite = {
    sprite with
        CurrentFrame = 0
        TimeInFrame = 0.0f
        Finished = false
  }

  let update (deltaSeconds: float32) (sprite: AnimatedSprite) : AnimatedSprite =
    if sprite.Finished then
      sprite
    else
      let anim = sprite.Sheet.AnimationsByIndex.[sprite.AnimationIndex]

      if anim.Frames.Length = 0 || anim.FrameDuration <= 0.0f then
        sprite
      else
        let totalTime = sprite.TimeInFrame + deltaSeconds
        let framesToSkip = int(totalTime / anim.FrameDuration)

        if framesToSkip = 0 then
          { sprite with TimeInFrame = totalTime }
        else
          let remainingTime = totalTime % anim.FrameDuration
          let nextFrame = sprite.CurrentFrame + framesToSkip

          if nextFrame < anim.Frames.Length then
            {
              sprite with
                  CurrentFrame = nextFrame
                  TimeInFrame = remainingTime
            }
          elif anim.Loop then
            {
              sprite with
                  CurrentFrame = nextFrame % anim.Frames.Length
                  TimeInFrame = remainingTime
            }
          else
            {
              sprite with
                  Finished = true
                  CurrentFrame = anim.Frames.Length - 1
                  TimeInFrame = 0.0f
            }

  let inline currentSource(sprite: AnimatedSprite) : Rectangle =
    let anim = sprite.Sheet.AnimationsByIndex.[sprite.AnimationIndex]

    if anim.Frames.Length = 0 then
      Rectangle()
    else
      anim.Frames.[min sprite.CurrentFrame (anim.Frames.Length - 1)]

  let inline isFinished(sprite: AnimatedSprite) = sprite.Finished

  let isPlaying (animName: string) (sprite: AnimatedSprite) =
    match sprite.Sheet.AnimationIndices.TryGetValue(animName) with
    | true, idx -> idx = sprite.AnimationIndex && not sprite.Finished
    | false, _ -> false

  let inline duration(sprite: AnimatedSprite) =
    Animation.duration sprite.Sheet.AnimationsByIndex.[sprite.AnimationIndex]

  let inline withColor
    (color: Color)
    (sprite: AnimatedSprite)
    : AnimatedSprite =
    { sprite with Color = color }

  let inline withScale
    (scale: float32)
    (sprite: AnimatedSprite)
    : AnimatedSprite =
    { sprite with Scale = scale }

  let inline withRotation
    (rotation: float32)
    (sprite: AnimatedSprite)
    : AnimatedSprite =
    { sprite with Rotation = rotation }

  let inline flipX(sprite: AnimatedSprite) : AnimatedSprite = {
    sprite with
        FlipX = not sprite.FlipX
  }

  let inline flipY(sprite: AnimatedSprite) : AnimatedSprite = {
    sprite with
        FlipY = not sprite.FlipY
  }

  let inline facingLeft(sprite: AnimatedSprite) : AnimatedSprite = {
    sprite with
        FlipX = true
  }

  let inline facingRight(sprite: AnimatedSprite) : AnimatedSprite = {
    sprite with
        FlipX = false
  }


