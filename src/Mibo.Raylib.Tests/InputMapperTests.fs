module Mibo.Raylib.Tests.InputMapper

open Expecto
open Raylib_cs
open Mibo.Input

type Action =
  | MoveUp
  | MoveDown
  | Jump

[<Tests>]
let tests =
  testList "InputMapper" [
    let emptyMap = InputMap.empty

    let map =
      emptyMap
      |> InputMap.key MoveUp KeyboardKey.W
      |> InputMap.key MoveDown KeyboardKey.S
      |> InputMap.key Jump KeyboardKey.Space

    testCase "ActionState.update starts an action"
    <| fun _ ->
      let state = ActionState.empty
      let newState = ActionState.update map true (Key KeyboardKey.W) state

      Expect.contains newState.Started MoveUp "MoveUp should have started"
      Expect.contains newState.Held MoveUp "MoveUp should be held"

      Expect.equal
        (Map.find MoveUp newState.Values)
        1.0f
        "MoveUp value should be 1.0"

    testCase "ActionState.update releases an action"
    <| fun _ ->
      let state = {
        ActionState.empty with
            Held = Set.singleton MoveUp
            HeldTriggers = Set.singleton(Key KeyboardKey.W)
      }

      let newState = ActionState.update map false (Key KeyboardKey.W) state

      Expect.isFalse (newState.Held.Contains MoveUp) "MoveUp should not be held"

      Expect.contains
        newState.Released
        MoveUp
        "MoveUp should be in released set"

      Expect.isFalse
        (newState.Values.ContainsKey MoveUp)
        "MoveUp value should be removed"

    testCase "ActionState.update handles multiple triggers for same action"
    <| fun _ ->
      let map =
        emptyMap |> InputMap.key Jump KeyboardKey.Space |> InputMap.mouse Jump 0

      let state = ActionState.empty
      // Press Space
      let state2 = ActionState.update map true (Key KeyboardKey.Space) state
      Expect.contains state2.Held Jump "Jump should be held by Space"

      // Left mouse click
      let state3 = ActionState.update map true (MouseBut 0) state2

      Expect.contains state3.Held Jump "Jump should still be held"

      // Release Space (Jump still held by Mouse)
      let state4 = ActionState.update map false (Key KeyboardKey.Space) state3
      Expect.contains state4.Held Jump "Jump should still be held by Mouse"

      Expect.isFalse
        (state4.Released.Contains Jump)
        "Jump should NOT be released yet"

      // Release Mouse
      let state5 = ActionState.update map false (MouseBut 0) state4

      Expect.isFalse
        (state5.Held.Contains Jump)
        "Jump should finally be released"

      Expect.contains state5.Released Jump "Jump should be in released set"

    testCase "ActionState.nextFrame clears one-shots"
    <| fun _ ->
      let state = {
        ActionState.empty with
            Started = Set.singleton Jump
            Released = Set.singleton MoveUp
      }

      let next = ActionState.nextFrame state

      Expect.isEmpty next.Started "Started should be cleared"
      Expect.isEmpty next.Released "Released should be cleared"
  ]
