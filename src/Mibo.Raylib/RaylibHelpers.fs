namespace Mibo.Elmish

open System.Numerics
open System.Runtime.CompilerServices
open Raylib_cs

/// Extensions for raylib-cs wrapper types to provide idiomatic F# access.
[<Extension>]
type RaylibExtensions =
  /// Convert a raylib-cs CBool wrapper to a native F# bool.
  [<Extension>]
  static member inline AsBool(c: CBool) : bool = CBool.op_Implicit(c)

module RaylibHelpers =
  let inline windowShouldClose() : bool = Raylib.WindowShouldClose().AsBool()

  let inline getFrameTime() : float32 = Raylib.GetFrameTime()

  let inline isKeyDown(key: KeyboardKey) : bool = Raylib.IsKeyDown(key).AsBool()

  let inline isKeyPressed(key: KeyboardKey) : bool =
    Raylib.IsKeyPressed(key).AsBool()

  let createCamera3D
    (position: Vector3)
    (target: Vector3)
    (up: Vector3)
    (fovY: float32)
    =
    let mutable c = Camera3D()
    c.Position <- position
    c.Target <- target
    c.Up <- up
    c.FovY <- fovY
    c.Projection <- CameraProjection.Perspective
    c
