module Mibo.Raylib.Tests.Animation

open System
open System.Numerics
open Expecto
open Raylib_cs
open Mibo.Animation

// ──────────────────────────────────────────────
// Helpers
// ──────────────────────────────────────────────

let private tex =
  Texture2D(
    Id = 1u,
    Width = 64,
    Height = 64,
    Mipmaps = 1,
    Format = PixelFormat.UncompressedR8G8B8A8
  )

let private makeSheet() =
  SpriteSheet.fromFrames tex (Vector2(32.0f, 32.0f)) [|
    "idle",
    {
      Frames = [|
        Rectangle(0.0f, 0.0f, 32.0f, 32.0f)
        Rectangle(32.0f, 0.0f, 32.0f, 32.0f)
        Rectangle(64.0f, 0.0f, 32.0f, 32.0f)
      |]
      FrameDuration = 0.1f
      Loop = true
    }
    "walk",
    {
      Frames = [|
        Rectangle(0.0f, 32.0f, 32.0f, 32.0f)
        Rectangle(32.0f, 32.0f, 32.0f, 32.0f)
      |]
      FrameDuration = 0.15f
      Loop = true
    }
    "attack",
    {
      Frames = [|
        Rectangle(0.0f, 64.0f, 32.0f, 32.0f)
        Rectangle(32.0f, 64.0f, 32.0f, 32.0f)
        Rectangle(64.0f, 64.0f, 32.0f, 32.0f)
        Rectangle(96.0f, 64.0f, 32.0f, 32.0f)
      |]
      FrameDuration = 0.08f
      Loop = false
    }
  |]

// ──────────────────────────────────────────────
// Animation.duration
// ──────────────────────────────────────────────

let animationDurationTests =
  testList "Animation.duration" [
    test "total duration = frames.Length * frameDuration" {
      let anim = {
        Frames = [| Rectangle(); Rectangle(); Rectangle() |]
        FrameDuration = 0.25f
        Loop = true
      }

      Expect.floatClose
        Accuracy.medium
        (float(Animation.duration anim))
        0.75
        "3 frames * 0.25s = 0.75s"
    }

    test "single frame animation" {
      let anim = {
        Frames = [| Rectangle() |]
        FrameDuration = 0.1f
        Loop = false
      }

      Expect.floatClose
        Accuracy.medium
        (float(Animation.duration anim))
        0.1
        "1 frame * 0.1s = 0.1s"
    }

    test "zero frames gives zero duration" {
      let anim = {
        Frames = [||]
        FrameDuration = 0.1f
        Loop = true
      }

      Expect.floatClose
        Accuracy.medium
        (float(Animation.duration anim))
        0.0
        "Empty frames = 0 duration"
    }
  ]

// ──────────────────────────────────────────────
// SpriteSheet Tests
// ──────────────────────────────────────────────

let spriteSheetTests =
  testList "SpriteSheet" [
    testList "fromFrames" [
      test "creates sheet with correct animation count" {
        let sheet = makeSheet()
        Expect.equal sheet.AnimationsByIndex.Length 3 "Should have 3 animations"
      }

      test "frameSize derived from first frame" {
        let sheet = makeSheet()
        Expect.equal sheet.FrameSize.X 32 "Width should be 32"
        Expect.equal sheet.FrameSize.Y 32 "Height should be 32"
      }

      test "origin is passed through from caller" {
        let sheet = makeSheet()

        Expect.floatClose
          Accuracy.medium
          (float sheet.Origin.X)
          32.0
          "Origin X = 32"

        Expect.floatClose
          Accuracy.medium
          (float sheet.Origin.Y)
          32.0
          "Origin Y = 32"
      }

      test "NormalMap is ValueNone by default" {
        let sheet = makeSheet()
        Expect.isFalse sheet.NormalMap.IsSome "NormalMap should be ValueNone"
      }
    ]

    testList "fromGrid" [
      test "computes correct frame rectangles" {
        let defs = [|
          {
            Name = "idle"
            Row = 0
            StartCol = 0
            FrameCount = 3
            Fps = 10.0f
            Loop = true
          }
          {
            Name = "walk"
            Row = 1
            StartCol = 0
            FrameCount = 4
            Fps = 12.0f
            Loop = true
          }
        |]

        let sheet = SpriteSheet.fromGrid tex 32 32 8 defs

        let idle = sheet.Animations["idle"]
        Expect.equal idle.Frames.Length 3 "Idle should have 3 frames"
        // First frame at col 0, row 0
        Expect.floatClose
          Accuracy.medium
          (float idle.Frames.[0].X)
          0.0
          "Frame 0 X"

        Expect.floatClose
          Accuracy.medium
          (float idle.Frames.[0].Y)
          0.0
          "Frame 0 Y"
        // Second frame at col 1, row 0
        Expect.floatClose
          Accuracy.medium
          (float idle.Frames.[1].X)
          32.0
          "Frame 1 X"

        Expect.floatClose
          Accuracy.medium
          (float idle.Frames.[1].Y)
          0.0
          "Frame 1 Y"
      }

      test "frame duration is 1/fps" {
        let defs = [|
          {
            Name = "anim"
            Row = 0
            StartCol = 0
            FrameCount = 2
            Fps = 10.0f
            Loop = true
          }
        |]

        let sheet = SpriteSheet.fromGrid tex 32 32 4 defs

        Expect.floatClose
          Accuracy.medium
          (float sheet.Animations["anim"].FrameDuration)
          0.1
          "10fps = 0.1s"
      }

      test "grid wrap-around for multi-row frames" {
        let defs = [|
          {
            Name = "wrap"
            Row = 0
            StartCol = 6
            FrameCount = 4
            Fps = 10.0f
            Loop = true
          }
        |]

        let sheet = SpriteSheet.fromGrid tex 32 32 8 defs
        let anim = sheet.Animations["wrap"]
        // Frame 0: col=6, row=0
        Expect.floatClose
          Accuracy.medium
          (float anim.Frames.[0].X)
          (6.0f * 32.0f |> float)
          "Frame 0 at col 6"
        // Frame 1: col=7, row=0
        Expect.floatClose
          Accuracy.medium
          (float anim.Frames.[1].X)
          (7.0f * 32.0f |> float)
          "Frame 1 at col 7"
        // Frame 2: col=0, row=1 (wrapped)
        Expect.floatClose
          Accuracy.medium
          (float anim.Frames.[2].X)
          0.0
          "Frame 2 wrapped to col 0"

        Expect.floatClose
          Accuracy.medium
          (float anim.Frames.[2].Y)
          32.0
          "Frame 2 wrapped to row 1"
      }
    ]

    testList "single" [
      test "creates single animation named 'default'" {
        let frames = [|
          Rectangle(0.0f, 0.0f, 16.0f, 16.0f)
          Rectangle(16.0f, 0.0f, 16.0f, 16.0f)
        |]

        let sheet = SpriteSheet.single tex frames 10.0f true
        Expect.equal sheet.AnimationsByIndex.Length 1 "Should have 1 animation"

        Expect.isTrue
          (sheet.Animations.ContainsKey("default"))
          "Should have 'default' animation"
      }

      test "origin is center of first frame" {
        let frames = [| Rectangle(0.0f, 0.0f, 48.0f, 48.0f) |]
        let sheet = SpriteSheet.single tex frames 10.0f true

        Expect.floatClose
          Accuracy.medium
          (float sheet.Origin.X)
          24.0
          "Origin X = 24"

        Expect.floatClose
          Accuracy.medium
          (float sheet.Origin.Y)
          24.0
          "Origin Y = 24"
      }
    ]

    testList "static'" [
      test "static' creates non-looping single frame sheet" {
        let sheet =
          SpriteSheet.static' tex (Rectangle(0.0f, 0.0f, 32.0f, 32.0f))

        let anim = sheet.Animations["default"]
        Expect.equal anim.Frames.Length 1 "Should have 1 frame"
        Expect.isFalse anim.Loop "Should not loop"
      }
    ]

    testList "tryGetAnimationIndex" [
      test "returns index for existing animation" {
        let sheet = makeSheet()
        let idx = SpriteSheet.tryGetAnimationIndex "walk" sheet
        Expect.equal idx (ValueSome 1) "'walk' should be at index 1"
      }

      test "returns ValueNone for missing animation" {
        let sheet = makeSheet()
        let idx = SpriteSheet.tryGetAnimationIndex "nonexistent" sheet
        Expect.equal idx ValueNone "Missing animation should return ValueNone"
      }
    ]

    testList "animationNames" [
      test "returns all animation names" {
        let sheet = makeSheet()
        let names = SpriteSheet.animationNames sheet |> Seq.toList |> List.sort

        Expect.equal
          names
          [ "attack"; "idle"; "walk" ]
          "Should have all 3 names"
      }
    ]

    testList "withNormalMap" [
      test "sets normal map" {
        let sheet = makeSheet()
        let nm = Texture2D(Id = 2u, Width = 64, Height = 64)
        let sheet' = SpriteSheet.withNormalMap nm sheet
        Expect.isTrue sheet'.NormalMap.IsSome "NormalMap should be set"
      }
    ]
  ]

// ──────────────────────────────────────────────
// AnimatedSprite Tests
// ──────────────────────────────────────────────

let animatedSpriteTests =
  let sheet = makeSheet()

  testList "AnimatedSprite" [
    testList "create" [
      test "starts at frame 0, not finished" {
        let sprite = AnimatedSprite.create sheet "idle"
        Expect.equal sprite.CurrentFrame 0 "Should start at frame 0"
        Expect.isFalse sprite.Finished "Should not be finished"

        Expect.floatClose
          Accuracy.medium
          (float sprite.TimeInFrame)
          0.0
          "TimeInFrame should be 0"
      }

      test "resolves animation index by name" {
        let sprite = AnimatedSprite.create sheet "walk"
        Expect.equal sprite.AnimationIndex 1 "Walk should be index 1"
      }

      test "defaults to index 0 for unknown name" {
        let sprite = AnimatedSprite.create sheet "nonexistent"
        Expect.equal sprite.AnimationIndex 0 "Unknown should default to 0"
      }

      test "default visual properties" {
        let sprite = AnimatedSprite.create sheet "idle"
        Expect.isFalse sprite.FlipX "FlipX should be false"
        Expect.isFalse sprite.FlipY "FlipY should be false"
        Expect.equal sprite.Color Color.White "Color should be White"

        Expect.floatClose
          Accuracy.medium
          (float sprite.Scale)
          1.0
          "Scale should be 1"

        Expect.floatClose
          Accuracy.medium
          (float sprite.Rotation)
          0.0
          "Rotation should be 0"
      }
    ]

    testList "createWith" [
      test "sets color and scale" {
        let sprite = AnimatedSprite.createWith sheet "idle" Color.Red 2.0f
        Expect.equal sprite.Color Color.Red "Color should be Red"

        Expect.floatClose
          Accuracy.medium
          (float sprite.Scale)
          2.0
          "Scale should be 2"
      }
    ]

    testList "update" [
      test "advances time within frame" {
        let sprite = AnimatedSprite.create sheet "idle"
        let updated = AnimatedSprite.update 0.05f sprite

        Expect.floatClose
          Accuracy.medium
          (float updated.TimeInFrame)
          0.05
          "TimeInFrame should be 0.05"

        Expect.equal updated.CurrentFrame 0 "Should still be on frame 0"
      }

      test "advances frame when time exceeds frameDuration" {
        let sprite = AnimatedSprite.create sheet "idle" // frameDuration = 0.1
        let updated = AnimatedSprite.update 0.15f sprite
        Expect.equal updated.CurrentFrame 1 "Should be on frame 1"

        Expect.floatClose
          Accuracy.medium
          (float updated.TimeInFrame)
          0.05
          "Remaining time should be 0.05"
      }

      test "looping animation wraps around" {
        let sprite = AnimatedSprite.create sheet "idle" // 3 frames, 0.1s each, loop=true
        // 0.35s = 3 frames + 0.05s remainder → wraps to frame 0
        let updated = AnimatedSprite.update 0.35f sprite
        Expect.equal updated.CurrentFrame 0 "Should wrap to frame 0"

        Expect.floatClose
          Accuracy.medium
          (float updated.TimeInFrame)
          0.05
          "Remainder should be 0.05"

        Expect.isFalse updated.Finished "Should not be finished"
      }

      test "non-looping animation finishes at last frame" {
        let sprite = AnimatedSprite.create sheet "attack" // 4 frames, 0.08s each, loop=false
        // 0.4s = 5 frames worth, but only 4 frames → finishes
        let updated = AnimatedSprite.update 0.4f sprite
        Expect.isTrue updated.Finished "Should be finished"
        Expect.equal updated.CurrentFrame 3 "Should be on last frame (3)"
      }

      test "finished animation does not update" {
        let sprite = AnimatedSprite.create sheet "attack"
        let finished = AnimatedSprite.update 1.0f sprite
        let doubleUpdate = AnimatedSprite.update 1.0f finished

        Expect.equal
          doubleUpdate.CurrentFrame
          finished.CurrentFrame
          "Frame should not change"

        Expect.isTrue doubleUpdate.Finished "Should still be finished"
      }

      test "zero dt does not advance" {
        let sprite = AnimatedSprite.create sheet "idle"
        let updated = AnimatedSprite.update 0.0f sprite
        Expect.equal updated.CurrentFrame 0 "Frame should stay 0"

        Expect.floatClose
          Accuracy.medium
          (float updated.TimeInFrame)
          0.0
          "TimeInFrame should stay 0"
      }

      test "large dt skips multiple frames" {
        let sprite = AnimatedSprite.create sheet "idle" // 3 frames, 0.1s each
        let updated = AnimatedSprite.update 0.25f sprite
        // 0.25 / 0.1 = 2 frames to skip, so frame 0 + 2 = frame 2
        Expect.equal updated.CurrentFrame 2 "Should be on frame 2"
      }
    ]

    testList "play" [
      test "switches to named animation" {
        let sprite = AnimatedSprite.create sheet "idle"
        let walking = AnimatedSprite.play "walk" sprite
        Expect.equal walking.AnimationIndex 1 "Should switch to walk (index 1)"
        Expect.equal walking.CurrentFrame 0 "Should reset to frame 0"
        Expect.isFalse walking.Finished "Should not be finished"
      }

      test "ignores unknown animation name" {
        let sprite = AnimatedSprite.create sheet "idle"
        let unchanged = AnimatedSprite.play "nonexistent" sprite

        Expect.equal
          unchanged.AnimationIndex
          sprite.AnimationIndex
          "Should stay on same animation"
      }

      test "no-op when already playing same animation" {
        let sprite = AnimatedSprite.create sheet "idle"
        let same = AnimatedSprite.play "idle" sprite

        Expect.equal
          same.AnimationIndex
          sprite.AnimationIndex
          "Index should stay same"

        Expect.equal
          same.CurrentFrame
          sprite.CurrentFrame
          "Frame should stay same"
      }

      test "restarts finished animation of same name" {
        let sprite = AnimatedSprite.create sheet "attack"
        let finished = AnimatedSprite.update 1.0f sprite
        Expect.isTrue finished.Finished "Precondition: should be finished"
        let restarted = AnimatedSprite.play "attack" finished
        // play returns same struct when idx matches AND not finished
        // But since it IS finished, it should restart
        Expect.isFalse restarted.Finished "Should restart"
        Expect.equal restarted.CurrentFrame 0 "Should reset to frame 0"
      }
    ]

    testList "playByIndex" [
      test "switches by index" {
        let sprite = AnimatedSprite.create sheet "idle"
        let walking = AnimatedSprite.playByIndex 1 sprite
        Expect.equal walking.AnimationIndex 1 "Should switch to index 1"
      }

      test "out of range index is no-op" {
        let sprite = AnimatedSprite.create sheet "idle"
        let unchanged = AnimatedSprite.playByIndex 99 sprite

        Expect.equal
          unchanged.AnimationIndex
          sprite.AnimationIndex
          "Should stay same"
      }

      test "negative index is no-op" {
        let sprite = AnimatedSprite.create sheet "idle"
        let unchanged = AnimatedSprite.playByIndex -1 sprite

        Expect.equal
          unchanged.AnimationIndex
          sprite.AnimationIndex
          "Should stay same"
      }
    ]

    testList "playIfNot" [
      test "switches when different animation" {
        let sprite = AnimatedSprite.create sheet "idle"
        let walking = AnimatedSprite.playIfNot "walk" sprite
        Expect.equal walking.AnimationIndex 1 "Should switch to walk"
      }

      test "no-op when same animation already playing" {
        let sprite = AnimatedSprite.create sheet "idle"
        let same = AnimatedSprite.playIfNot "idle" sprite

        Expect.equal
          same.AnimationIndex
          sprite.AnimationIndex
          "Index should stay same"

        Expect.equal
          same.CurrentFrame
          sprite.CurrentFrame
          "Frame should stay same"
      }

      test "ignores unknown animation" {
        let sprite = AnimatedSprite.create sheet "idle"
        let unchanged = AnimatedSprite.playIfNot "nonexistent" sprite

        Expect.equal
          unchanged.AnimationIndex
          sprite.AnimationIndex
          "Index should stay same"
      }
    ]

    testList "restart" [
      test "resets to frame 0 and clears finished" {
        let sprite = AnimatedSprite.create sheet "attack"
        let finished = AnimatedSprite.update 1.0f sprite
        let restarted = AnimatedSprite.restart finished
        Expect.equal restarted.CurrentFrame 0 "Should be frame 0"

        Expect.floatClose
          Accuracy.medium
          (float restarted.TimeInFrame)
          0.0
          "TimeInFrame should be 0"

        Expect.isFalse restarted.Finished "Should not be finished"
      }
    ]

    testList "currentSource" [
      test "returns correct rectangle for current frame" {
        let sprite = AnimatedSprite.create sheet "idle"
        let src = AnimatedSprite.currentSource sprite
        Expect.floatClose Accuracy.medium (float src.X) 0.0 "Frame 0 X"
        Expect.floatClose Accuracy.medium (float src.Y) 0.0 "Frame 0 Y"
        Expect.floatClose Accuracy.medium (float src.Width) 32.0 "Width 32"
      }

      test "returns correct rectangle after advancing" {
        let sprite = AnimatedSprite.create sheet "idle"
        let advanced = AnimatedSprite.update 0.15f sprite // advance to frame 1
        let src = AnimatedSprite.currentSource advanced
        Expect.floatClose Accuracy.medium (float src.X) 32.0 "Frame 1 X = 32"
      }
    ]

    testList "isFinished" [
      test "false for fresh sprite" {
        let sprite = AnimatedSprite.create sheet "attack"

        Expect.isFalse
          (AnimatedSprite.isFinished sprite)
          "Fresh sprite should not be finished"
      }

      test "true after non-looping animation completes" {
        let sprite = AnimatedSprite.create sheet "attack"
        let updated = AnimatedSprite.update 1.0f sprite
        Expect.isTrue (AnimatedSprite.isFinished updated) "Should be finished"
      }
    ]

    testList "isPlaying" [
      test "true for current animation" {
        let sprite = AnimatedSprite.create sheet "idle"

        Expect.isTrue
          (AnimatedSprite.isPlaying "idle" sprite)
          "Should be playing idle"
      }

      test "false for different animation" {
        let sprite = AnimatedSprite.create sheet "idle"

        Expect.isFalse
          (AnimatedSprite.isPlaying "walk" sprite)
          "Should not be playing walk"
      }

      test "false for unknown animation" {
        let sprite = AnimatedSprite.create sheet "idle"

        Expect.isFalse
          (AnimatedSprite.isPlaying "nonexistent" sprite)
          "Unknown should be false"
      }

      test "false when finished" {
        let sprite = AnimatedSprite.create sheet "attack"
        let finished = AnimatedSprite.update 1.0f sprite

        Expect.isFalse
          (AnimatedSprite.isPlaying "attack" finished)
          "Finished should not be playing"
      }
    ]

    testList "duration" [
      test "returns correct duration for current animation" {
        let sprite = AnimatedSprite.create sheet "idle" // 3 frames * 0.1s = 0.3s

        Expect.floatClose
          Accuracy.medium
          (float(AnimatedSprite.duration sprite))
          0.3
          "Idle duration = 0.3s"
      }
    ]

    testList "flip and facing" [
      test "flipX toggles" {
        let sprite = AnimatedSprite.create sheet "idle"
        Expect.isFalse sprite.FlipX "Initially false"
        let flipped = AnimatedSprite.flipX sprite
        Expect.isTrue flipped.FlipX "Should be true after flip"
        let unflipped = AnimatedSprite.flipX flipped
        Expect.isFalse unflipped.FlipX "Should be false after double flip"
      }

      test "flipY toggles" {
        let sprite = AnimatedSprite.create sheet "idle"
        let flipped = AnimatedSprite.flipY sprite
        Expect.isTrue flipped.FlipY "Should be true"
      }

      test "facingLeft sets FlipX true" {
        let sprite = AnimatedSprite.create sheet "idle"
        let left = AnimatedSprite.facingLeft sprite
        Expect.isTrue left.FlipX "Should be facing left"
      }

      test "facingRight sets FlipX false" {
        let sprite =
          AnimatedSprite.create sheet "idle" |> AnimatedSprite.facingLeft

        let right = AnimatedSprite.facingRight sprite
        Expect.isFalse right.FlipX "Should be facing right"
      }
    ]

    testList "withColor / withScale / withRotation" [
      test "withColor sets color" {
        let sprite = AnimatedSprite.create sheet "idle"
        let colored = AnimatedSprite.withColor Color.Red sprite
        Expect.equal colored.Color Color.Red "Color should be Red"
      }

      test "withScale sets scale" {
        let sprite = AnimatedSprite.create sheet "idle"
        let scaled = AnimatedSprite.withScale 3.0f sprite

        Expect.floatClose
          Accuracy.medium
          (float scaled.Scale)
          3.0
          "Scale should be 3"
      }

      test "withRotation sets rotation" {
        let sprite = AnimatedSprite.create sheet "idle"
        let rotated = AnimatedSprite.withRotation (MathF.PI / 2.0f) sprite

        Expect.floatClose
          Accuracy.medium
          (float rotated.Rotation)
          (float(MathF.PI / 2.0f))
          "Rotation should be pi/2"
      }
    ]
  ]

// ──────────────────────────────────────────────
// Main test list
// ──────────────────────────────────────────────

[<Tests>]
let tests =
  testList "Animation" [
    animationDurationTests
    spriteSheetTests
    animatedSpriteTests
  ]
