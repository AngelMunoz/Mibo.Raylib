namespace Mibo.Elmish

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
