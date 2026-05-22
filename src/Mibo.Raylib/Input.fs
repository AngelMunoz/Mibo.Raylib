namespace Mibo.Input

open System
open System.Numerics
open Raylib_cs
open Mibo.Elmish

/// <summary>Keyboard state delta containing keys that changed this frame.</summary>
[<Struct>]
type KeyboardDelta = {
  Pressed: KeyboardKey[]
  Released: KeyboardKey[]
}

/// <summary>Mouse button state changes for a single frame.</summary>
[<Struct>]
type MouseButtons = {
  Pressed: MouseButton[]
  Released: MouseButton[]
}

/// <summary>Mouse state delta containing position and button changes.</summary>
[<Struct>]
type MouseDelta = {
  Position: Vector2
  PositionDelta: Vector2
  Buttons: MouseButtons
  ScrollDelta: float32
  ScrollDeltaV: Vector2
}

/// <summary>Touch point state for tracking individual touch lifecycle.</summary>
type TouchState =
  | Pressed
  | Moved
  | Released

/// <summary>A single touch point for touch input.</summary>
[<Struct>]
type TouchPoint = {
  Id: int
  Position: Vector2
  State: TouchState
}

/// <summary>Touch input state containing all active touch points.</summary>
[<Struct>]
type TouchDelta = { Touches: TouchPoint[] }

/// <summary>Gamepad button state changes for a single frame.</summary>
[<Struct>]
type GamepadButtons = {
  Pressed: GamepadButton[]
  Released: GamepadButton[]
}

/// <summary>Gamepad analog input values (thumbsticks and triggers).</summary>
/// <remarks>Thumbsticks range from -1 to 1, triggers range from 0 to 1.</remarks>
[<Struct>]
type GamepadAnalog = {
  LeftThumbstick: Vector2
  RightThumbstick: Vector2
  LeftTrigger: float32
  RightTrigger: float32
}

/// <summary>Per-player gamepad delta containing button and analog changes.</summary>
[<Struct>]
type GamepadDelta = {
  PlayerIndex: int
  Buttons: GamepadButtons
  Analog: GamepadAnalog
}

/// <summary>Gamepad connection state change event.</summary>
[<Struct>]
type GamepadConnection = { PlayerIndex: int; IsConnected: bool }

/// <summary>Gesture detection events for touch-capable devices.</summary>
[<Struct>]
type GestureDelta = {
  Gesture: Gesture
  HoldDuration: float32
  DragVector: Vector2
  DragAngle: float32
  PinchVector: Vector2
  PinchAngle: float32
}

/// <summary>Per-game input service providing reactive observables for hardware input.</summary>
type IInput =
  abstract Poll: unit -> unit
  abstract KeyboardDelta: IObservable<KeyboardDelta>
  abstract MouseDelta: IObservable<MouseDelta>
  abstract TouchDelta: IObservable<TouchDelta>
  abstract GamepadDelta: IObservable<GamepadDelta>
  abstract GamepadConnection: IObservable<GamepadConnection>
  abstract GestureDelta: IObservable<GestureDelta>

// ─────────────────────────────────────────────────────────────────────────────
// Input Polling Functions (module-level, composable)
// ─────────────────────────────────────────────────────────────────────────────

module InputPolling =
  let private allKeyboardKeys =
    Enum.GetValues(typeof<KeyboardKey>) :?> KeyboardKey[]
    |> Array.filter(fun k ->
      k <> KeyboardKey.Null && int k >= 32 && int k <= 348)

  let private allMouseButtons =
    Enum.GetValues(typeof<MouseButton>) :?> MouseButton[]

  let private allGamepadButtons =
    Enum.GetValues(typeof<GamepadButton>) :?> GamepadButton[]

  let pollKeyboard
    (pressedBuf: ResizeArray<KeyboardKey>)
    (releasedBuf: ResizeArray<KeyboardKey>)
    (trigger: KeyboardDelta -> unit)
    =
    pressedBuf.Clear()
    releasedBuf.Clear()

    for k in allKeyboardKeys do
      if Raylib.IsKeyPressed(k).AsBool() then
        pressedBuf.Add(k)
      elif Raylib.IsKeyReleased(k).AsBool() then
        releasedBuf.Add(k)

    if pressedBuf.Count > 0 || releasedBuf.Count > 0 then
      trigger {
        Pressed = pressedBuf.ToArray()
        Released = releasedBuf.ToArray()
      }

  let pollMouse
    (pressedBuf: ResizeArray<MouseButton>)
    (releasedBuf: ResizeArray<MouseButton>)
    (trigger: MouseDelta -> unit)
    =
    pressedBuf.Clear()
    releasedBuf.Clear()

    for btn in allMouseButtons do
      if Raylib.IsMouseButtonPressed(btn).AsBool() then
        pressedBuf.Add(btn)
      elif Raylib.IsMouseButtonReleased(btn).AsBool() then
        releasedBuf.Add(btn)

    let pos = Raylib.GetMousePosition()
    let delta = Raylib.GetMouseDelta()
    let scroll = Raylib.GetMouseWheelMove()
    let scrollV = Raylib.GetMouseWheelMoveV()

    let hasButtonChange = pressedBuf.Count > 0 || releasedBuf.Count > 0
    let hasMove = delta.X <> 0.0f || delta.Y <> 0.0f
    let hasScroll = scroll <> 0.0f || scrollV.X <> 0.0f || scrollV.Y <> 0.0f

    if hasButtonChange || hasMove || hasScroll then
      trigger {
        Position = pos
        PositionDelta = delta
        Buttons = {
          Pressed = pressedBuf.ToArray()
          Released = releasedBuf.ToArray()
        }
        ScrollDelta = scroll
        ScrollDeltaV = scrollV
      }

  let pollTouch (prevTouchIds: ResizeArray<int>) (trigger: TouchDelta -> unit) =
    let count = Raylib.GetTouchPointCount()

    if count > 0 then
      let currentIds = ResizeArray<int>(count)
      let points = Array.zeroCreate<TouchPoint> count

      for i = 0 to count - 1 do
        let id = Raylib.GetTouchPointId(i)
        let pos = Raylib.GetTouchPosition(i)
        currentIds.Add(id)

        let state =
          if prevTouchIds.Contains(id) then
            TouchState.Moved
          else
            TouchState.Pressed

        points[i] <- {
          Id = id
          Position = pos
          State = state
        }

      let releasedIds = prevTouchIds |> Seq.filter(not << currentIds.Contains)

      let releasedPoints =
        releasedIds
        |> Seq.map(fun id -> {
          Id = id
          Position = Vector2.Zero
          State = TouchState.Released
        })
        |> Seq.toArray

      trigger {
        Touches = Array.append points releasedPoints
      }

      prevTouchIds.Clear()
      prevTouchIds.AddRange(currentIds)
    else if prevTouchIds.Count > 0 then
      let releasedPoints =
        prevTouchIds
        |> Seq.map(fun id -> {
          Id = id
          Position = Vector2.Zero
          State = TouchState.Released
        })
        |> Seq.toArray

      trigger { Touches = releasedPoints }
      prevTouchIds.Clear()

  let pollGamepad
    (prevConnected: bool[])
    (pressedBuf: ResizeArray<GamepadButton>)
    (releasedBuf: ResizeArray<GamepadButton>)
    (triggerDelta: GamepadDelta -> unit)
    (triggerConnection: GamepadConnection -> unit)
    =
    for i = 0 to 3 do
      let isConnected = Raylib.IsGamepadAvailable(i).AsBool()

      if prevConnected[i] <> isConnected then
        triggerConnection {
          PlayerIndex = i
          IsConnected = isConnected
        }

      prevConnected[i] <- isConnected

      if isConnected then
        pressedBuf.Clear()
        releasedBuf.Clear()

        for btn in allGamepadButtons do
          if btn <> GamepadButton.Unknown then
            if Raylib.IsGamepadButtonPressed(i, btn).AsBool() then
              pressedBuf.Add(btn)
            elif Raylib.IsGamepadButtonReleased(i, btn).AsBool() then
              releasedBuf.Add(btn)

        let hasButtonChange = pressedBuf.Count > 0 || releasedBuf.Count > 0

        let leftStick =
          Vector2(
            Raylib.GetGamepadAxisMovement(i, GamepadAxis.LeftX),
            Raylib.GetGamepadAxisMovement(i, GamepadAxis.LeftY)
          )

        let rightStick =
          Vector2(
            Raylib.GetGamepadAxisMovement(i, GamepadAxis.RightX),
            Raylib.GetGamepadAxisMovement(i, GamepadAxis.RightY)
          )

        let leftTrigger =
          Raylib.GetGamepadAxisMovement(i, GamepadAxis.LeftTrigger)

        let rightTrigger =
          Raylib.GetGamepadAxisMovement(i, GamepadAxis.RightTrigger)

        let hasAnalogChange =
          leftStick.X <> 0.0f
          || leftStick.Y <> 0.0f
          || rightStick.X <> 0.0f
          || rightStick.Y <> 0.0f
          || leftTrigger <> 0.0f
          || rightTrigger <> 0.0f

        if hasButtonChange || hasAnalogChange then
          triggerDelta {
            PlayerIndex = i
            Buttons = {
              Pressed = pressedBuf.ToArray()
              Released = releasedBuf.ToArray()
            }
            Analog = {
              LeftThumbstick = leftStick
              RightThumbstick = rightStick
              LeftTrigger = leftTrigger
              RightTrigger = rightTrigger
            }
          }

  let pollGestures(trigger: GestureDelta -> unit) =
    let detected = Raylib.GetGestureDetected()

    if detected <> Gesture.None then
      trigger {
        Gesture = detected
        HoldDuration = Raylib.GetGestureHoldDuration()
        DragVector = Raylib.GetGestureDragVector()
        DragAngle = Raylib.GetGestureDragAngle()
        PinchVector = Raylib.GetGesturePinchVector()
        PinchAngle = Raylib.GetGesturePinchAngle()
      }

// ─────────────────────────────────────────────────────────────────────────────
// Input Service (IInput implementation via object expression)
// ─────────────────────────────────────────────────────────────────────────────

module Input =

  let internal create(gestures: Gesture list) : IInput =
    let keyboardDelta = Event<KeyboardDelta>()
    let mouseDelta = Event<MouseDelta>()
    let touchDelta = Event<TouchDelta>()
    let gamepadDelta = Event<GamepadDelta>()
    let gamepadConnection = Event<GamepadConnection>()
    let gestureDelta = Event<GestureDelta>()

    let pressedKeysBuf = ResizeArray<KeyboardKey>(8)
    let releasedKeysBuf = ResizeArray<KeyboardKey>(8)
    let pressedMouseBuf = ResizeArray<MouseButton>(4)
    let releasedMouseBuf = ResizeArray<MouseButton>(4)
    let pressedGpBuf = ResizeArray<GamepadButton>(8)
    let releasedGpBuf = ResizeArray<GamepadButton>(8)
    let prevTouchIds = ResizeArray<int>(8)
    let prevConnected = Array.create 4 false

    // Enable requested gestures (bitwise OR of flags)
    let gestureFlags = gestures |> List.reduce(fun acc g -> acc ||| g)

    if gestures.Length > 0 then
      Raylib.SetGesturesEnabled(gestureFlags)

    { new IInput with
        member _.Poll() =
          InputPolling.pollKeyboard
            pressedKeysBuf
            releasedKeysBuf
            keyboardDelta.Trigger

          InputPolling.pollMouse
            pressedMouseBuf
            releasedMouseBuf
            mouseDelta.Trigger

          InputPolling.pollTouch prevTouchIds touchDelta.Trigger

          InputPolling.pollGamepad
            prevConnected
            pressedGpBuf
            releasedGpBuf
            gamepadDelta.Trigger
            gamepadConnection.Trigger

          InputPolling.pollGestures gestureDelta.Trigger

        member _.KeyboardDelta = keyboardDelta.Publish
        member _.MouseDelta = mouseDelta.Publish
        member _.TouchDelta = touchDelta.Publish
        member _.GamepadDelta = gamepadDelta.Publish
        member _.GamepadConnection = gamepadConnection.Publish
        member _.GestureDelta = gestureDelta.Publish
    }

  let tryGetService(ctx: GameContext) : IInput voption =
    GameContext.tryGetService<IInput> ctx

  let getService(ctx: GameContext) : IInput =
    match tryGetService ctx with
    | ValueSome i -> i
    | ValueNone ->
      failwith
        "IInput service not registered. Add Program.withInput to your program."

// ─────────────────────────────────────────────────────────────────────────────
// Subscription Helpers (Elmish)
// ─────────────────────────────────────────────────────────────────────────────

module Keyboard =
  let listen
    (onPressed: KeyboardKey -> 'Msg)
    (onReleased: KeyboardKey -> 'Msg)
    (ctx: GameContext)
    : Sub<'Msg> =
    let subId = SubId.ofString "Mibo/Input/Keyboard/listen"

    let subscribe(dispatch: Dispatch<'Msg>) =
      (Input.getService ctx)
        .KeyboardDelta.Subscribe(fun delta ->
          for k in delta.Pressed do
            dispatch(onPressed k)

          for k in delta.Released do
            dispatch(onReleased k))

    Sub.Active(subId, subscribe)

  let onPressed (handler: KeyboardKey -> 'Msg) (ctx: GameContext) : Sub<'Msg> =
    let subId = SubId.ofString "Mibo/Input/Keyboard/onPressed"

    let subscribe(dispatch: Dispatch<'Msg>) =
      (Input.getService ctx)
        .KeyboardDelta.Subscribe(fun delta ->
          for k in delta.Pressed do
            dispatch(handler k))

    Sub.Active(subId, subscribe)

  let onReleased (handler: KeyboardKey -> 'Msg) (ctx: GameContext) : Sub<'Msg> =
    let subId = SubId.ofString "Mibo/Input/Keyboard/onReleased"

    let subscribe(dispatch: Dispatch<'Msg>) =
      (Input.getService ctx)
        .KeyboardDelta.Subscribe(fun delta ->
          for k in delta.Released do
            dispatch(handler k))

    Sub.Active(subId, subscribe)

module Mouse =
  let listen (handler: MouseDelta -> 'Msg) (ctx: GameContext) : Sub<'Msg> =
    let subId = SubId.ofString "Mibo/Input/Mouse/listen"

    let subscribe(dispatch: Dispatch<'Msg>) =
      (Input.getService ctx)
        .MouseDelta.Subscribe(fun delta -> dispatch(handler delta))

    Sub.Active(subId, subscribe)

  let onMove (handler: Vector2 -> 'Msg) (ctx: GameContext) : Sub<'Msg> =
    let subId = SubId.ofString "Mibo/Input/Mouse/onMove"

    let subscribe(dispatch: Dispatch<'Msg>) =
      (Input.getService ctx)
        .MouseDelta.Subscribe(fun delta ->
          if
            delta.PositionDelta.X <> 0.0f || delta.PositionDelta.Y <> 0.0f
          then
            dispatch(handler delta.Position))

    Sub.Active(subId, subscribe)

  let onButton
    (handler: MouseButton * Vector2 -> 'Msg)
    (ctx: GameContext)
    : Sub<'Msg> =
    let subId = SubId.ofString "Mibo/Input/Mouse/onButton"

    let subscribe(dispatch: Dispatch<'Msg>) =
      (Input.getService ctx)
        .MouseDelta.Subscribe(fun delta ->
          for btn in delta.Buttons.Pressed do
            dispatch(handler(btn, delta.Position)))

    Sub.Active(subId, subscribe)

  let onLeftClick (handler: Vector2 -> 'Msg) (ctx: GameContext) : Sub<'Msg> =
    let subId = SubId.ofString "Mibo/Input/Mouse/onLeftClick"

    let subscribe(dispatch: Dispatch<'Msg>) =
      (Input.getService ctx)
        .MouseDelta.Subscribe(fun delta ->
          if delta.Buttons.Pressed |> Array.contains MouseButton.Left then
            dispatch(handler delta.Position))

    Sub.Active(subId, subscribe)

  let onRightClick (handler: Vector2 -> 'Msg) (ctx: GameContext) : Sub<'Msg> =
    let subId = SubId.ofString "Mibo/Input/Mouse/onRightClick"

    let subscribe(dispatch: Dispatch<'Msg>) =
      (Input.getService ctx)
        .MouseDelta.Subscribe(fun delta ->
          if delta.Buttons.Pressed |> Array.contains MouseButton.Right then
            dispatch(handler delta.Position))

    Sub.Active(subId, subscribe)

  let onScroll (handler: float32 -> 'Msg) (ctx: GameContext) : Sub<'Msg> =
    let subId = SubId.ofString "Mibo/Input/Mouse/onScroll"

    let subscribe(dispatch: Dispatch<'Msg>) =
      (Input.getService ctx)
        .MouseDelta.Subscribe(fun delta ->
          if delta.ScrollDelta <> 0.0f then
            dispatch(handler delta.ScrollDelta))

    Sub.Active(subId, subscribe)

module Touch =
  let listen (handler: TouchPoint[] -> 'Msg) (ctx: GameContext) : Sub<'Msg> =
    let subId = SubId.ofString "Mibo/Input/Touch/listen"

    let subscribe(dispatch: Dispatch<'Msg>) =
      (Input.getService ctx)
        .TouchDelta.Subscribe(fun delta -> dispatch(handler delta.Touches))

    Sub.Active(subId, subscribe)

module Gamepad =
  let listen (handler: GamepadDelta -> 'Msg) (ctx: GameContext) : Sub<'Msg> =
    let subId = SubId.ofString "Mibo/Input/Gamepad/listen"

    let subscribe(dispatch: Dispatch<'Msg>) =
      (Input.getService ctx)
        .GamepadDelta.Subscribe(fun delta -> dispatch(handler delta))

    Sub.Active(subId, subscribe)

  let listenPlayer
    (player: int)
    (handler: GamepadDelta -> 'Msg)
    (ctx: GameContext)
    : Sub<'Msg> =
    let subId = SubId.ofString $"Mibo/Input/Gamepad/listenPlayer/{player}"

    let subscribe(dispatch: Dispatch<'Msg>) =
      (Input.getService ctx)
        .GamepadDelta.Subscribe(fun delta ->
          if delta.PlayerIndex = player then
            dispatch(handler delta))

    Sub.Active(subId, subscribe)

  let onConnected (handler: int -> 'Msg) (ctx: GameContext) : Sub<'Msg> =
    let subId = SubId.ofString "Mibo/Input/Gamepad/onConnected"

    let subscribe(dispatch: Dispatch<'Msg>) =
      (Input.getService ctx)
        .GamepadConnection.Subscribe(fun conn ->
          if conn.IsConnected then
            dispatch(handler conn.PlayerIndex))

    Sub.Active(subId, subscribe)

  let onDisconnected (handler: int -> 'Msg) (ctx: GameContext) : Sub<'Msg> =
    let subId = SubId.ofString "Mibo/Input/Gamepad/onDisconnected"

    let subscribe(dispatch: Dispatch<'Msg>) =
      (Input.getService ctx)
        .GamepadConnection.Subscribe(fun conn ->
          if not conn.IsConnected then
            dispatch(handler conn.PlayerIndex))

    Sub.Active(subId, subscribe)

  let onConnectionChange
    (handler: GamepadConnection -> 'Msg)
    (ctx: GameContext)
    : Sub<'Msg> =
    let subId = SubId.ofString "Mibo/Input/Gamepad/onConnectionChange"

    let subscribe(dispatch: Dispatch<'Msg>) =
      (Input.getService ctx)
        .GamepadConnection.Subscribe(fun conn -> dispatch(handler conn))

    Sub.Active(subId, subscribe)

module Gesture =
  let listen (handler: GestureDelta -> 'Msg) (ctx: GameContext) : Sub<'Msg> =
    let subId = SubId.ofString "Mibo/Input/Gesture/listen"

    let subscribe(dispatch: Dispatch<'Msg>) =
      (Input.getService ctx)
        .GestureDelta.Subscribe(fun delta -> dispatch(handler delta))

    Sub.Active(subId, subscribe)
