namespace Mibo.Input

open System
open System.Numerics
open Mibo.Elmish

/// <summary>
/// Keyboard state delta containing keys that changed this frame.
/// </summary>
/// <remarks>
/// This struct is emitted when keyboard state changes.
/// </remarks>
[<Struct>]
type KeyboardDelta = {
  /// Keys that were pressed this frame (were up, now down).
  Pressed: Raylib_cs.KeyboardKey[]
  /// Keys that were released this frame (were down, now up).
  Released: Raylib_cs.KeyboardKey[]
}

/// <summary>Mouse button state changes for a single frame.</summary>
[<Struct>]
type MouseButtons = {
  LeftPressed: bool
  LeftReleased: bool
  RightPressed: bool
  RightReleased: bool
  MiddlePressed: bool
  MiddleReleased: bool
}

/// <summary>
/// Mouse state delta containing position and button changes.
/// </summary>
/// <remarks>
/// This struct is emitted when mouse state changes (movement, button press,
/// or scroll wheel).
/// </remarks>
[<Struct>]
type MouseDelta = {
  /// Current mouse position in screen coordinates.
  Position: Vector2
  /// Change in position since last frame.
  PositionDelta: Vector2
  /// Button state changes.
  Buttons: MouseButtons
  /// Scroll wheel delta (positive = up, negative = down).
  ScrollDelta: int
}

/// <summary>
/// Per-game input service providing reactive observables for hardware input.
/// </summary>
/// <remarks>
/// Subscribe to these observables to receive input deltas.
/// </remarks>
type IInput =
  /// Emits when keyboard state changes.
  abstract KeyboardDelta: IObservable<KeyboardDelta>
  /// Emits when mouse state changes (position, buttons, or scroll).
  abstract MouseDelta: IObservable<MouseDelta>

// ─────────────────────────────────────────────────────────────────────────────
// Internal service registry (provides IInput without a DI container)
// ─────────────────────────────────────────────────────────────────────────────

module private InputHolder =
  let mutable instance: IInput voption = ValueNone

/// <summary>Accessor for the registered <see cref="T:Mibo.Input.IInput"/> service.</summary>
module Input =
  /// <summary>Registers the IInput service. Typically called during program setup.</summary>
  let setService (s: IInput) = InputHolder.instance <- ValueSome s

  /// <summary>Gets the registered IInput service.</summary>
  /// <exception cref="T:System.InvalidOperationException">Thrown when no service is registered.</exception>
  let getService () : IInput =
    match InputHolder.instance with
    | ValueSome s -> s
    | ValueNone ->
      invalidOp
        "IInput service not registered. Call Input.setService during program setup."

// ─────────────────────────────────────────────────────────────────────────────
// Input Mapping Types
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Represents a physical hardware input trigger.
/// </summary>
/// <remarks>
/// Triggers are the "source" side of input mapping - they represent actual
/// hardware inputs that can be detected.
/// </remarks>
type Trigger =
  /// A keyboard key.
  | Key of Raylib_cs.KeyboardKey
  /// A mouse button (0=Left, 1=Right, 2=Middle).
  | MouseBut of int

/// <summary>
/// Configuration mapping game actions to their trigger inputs.
/// </summary>
/// <remarks>
/// InputMap is immutable and can be stored in your model. Use the <see cref="T:Mibo.Input.InputMap"/>
/// module functions to build mappings.
/// </remarks>
/// <example>
/// <code>
/// type Action = MoveLeft | MoveRight | Jump | Fire
///
/// let inputMap =
///     InputMap.empty
///     |&gt; InputMap.key MoveLeft KeyboardKey.A
///     |&gt; InputMap.key MoveLeft KeyboardKey.Left
///     |&gt; InputMap.key MoveRight KeyboardKey.D
///     |&gt; InputMap.key MoveRight KeyboardKey.Right
///     |&gt; InputMap.key Jump KeyboardKey.Space
///     |&gt; InputMap.mouse Fire 0  // Left click
/// </code>
/// </example>
type InputMap<'Action when 'Action: comparison> = {

  /// Map from action to all triggers that can activate it.
  ActionToTriggers: Map<'Action, Trigger list>
  /// Reverse lookup: map from trigger to all actions it activates.
  TriggerToActions: Map<Trigger, 'Action list>
}

/// Functions for building InputMap configurations.
module InputMap =
  /// An empty input map with no bindings.
  let empty = {
    ActionToTriggers = Map.empty
    TriggerToActions = Map.empty
  }

  /// Binds a trigger to an action. Multiple triggers can map to the same action.
  let bind (action: 'Action) (trigger: Trigger) (map: InputMap<'Action>) =
    let existingTriggers =
      map.ActionToTriggers |> Map.tryFind action |> Option.defaultValue []

    let existingActions =
      map.TriggerToActions |> Map.tryFind trigger |> Option.defaultValue []

    {
      ActionToTriggers =
        map.ActionToTriggers |> Map.add action (trigger :: existingTriggers)
      TriggerToActions =
        map.TriggerToActions |> Map.add trigger (action :: existingActions)
    }

  /// Binds a keyboard key to an action.
  let key (action: 'Action) (k: Raylib_cs.KeyboardKey) (map: InputMap<'Action>) =
    bind action (Key k) map

  /// Binds a mouse button to an action (0=Left, 1=Right, 2=Middle).
  let mouse (action: 'Action) (btn: int) (map: InputMap<'Action>) =
    bind action (MouseBut btn) map

/// <summary>
/// Runtime state tracking which actions are currently active.
/// </summary>
/// <remarks>
/// ActionState is the "output" of the input mapping system. It tells you
/// which actions are held, just started, or just released.
/// </remarks>
/// <example>
/// <code>
/// if actionState.Started.Contains Jump then
///     // Player just pressed jump this frame
///
/// if actionState.Held.Contains MoveLeft then
///     // Player is holding left
/// </code>
/// </example>
type ActionState<'Action when 'Action: comparison> = {
  /// Actions currently being held down.
  Held: Set<'Action>
  /// Actions that started (pressed) this frame.
  Started: Set<'Action>
  /// Actions that ended (released) this frame.
  Released: Set<'Action>
  /// Analog values for actions (0.0 to 1.0). Used for triggers/thumbsticks.
  Values: Map<'Action, float32>
  /// Internal: tracks which raw triggers are currently held.
  HeldTriggers: Set<Trigger>
}

module ActionState =
  let empty = {
    Held = Set.empty
    Started = Set.empty
    Released = Set.empty
    Values = Map.empty
    HeldTriggers = Set.empty
  }

  /// Internal update logic (pure)
  let update map isDown trigger state =
    let newHeldTriggers =
      if isDown then
        state.HeldTriggers |> Set.add trigger
      else
        state.HeldTriggers |> Set.remove trigger

    let actions =
      map.TriggerToActions |> Map.tryFind trigger |> Option.defaultValue []

    let mutable newHeld = state.Held
    let mutable newStarted = state.Started
    let mutable newReleased = state.Released
    let mutable newValues = state.Values

    for action in actions do
      let allTriggers =
        map.ActionToTriggers |> Map.tryFind action |> Option.defaultValue []

      let isActionHeld = allTriggers |> List.exists newHeldTriggers.Contains
      let wasHeld = state.Held.Contains action

      if isActionHeld && not wasHeld then
        newHeld <- newHeld |> Set.add action
        newStarted <- newStarted |> Set.add action
        newValues <- newValues |> Map.add action 1.0f
      elif not isActionHeld && wasHeld then
        newHeld <- newHeld |> Set.remove action
        newReleased <- newReleased |> Set.add action
        newValues <- newValues |> Map.remove action

    {
      Held = newHeld
      Started = newStarted
      Released = newReleased
      Values = newValues
      HeldTriggers = newHeldTriggers
    }

  let nextFrame (state: ActionState<'Action>) = {
    state with
        Started = Set.empty
        Released = Set.empty
  }

/// Service Interface for Input Mapping
type IInputMapper<'Action when 'Action: comparison> =
  abstract CurrentState: ActionState<'Action>
  abstract Update: unit -> unit

// ─────────────────────────────────────────────────────────────────────────────
// IInputMapper service registry (type-indexed)
// ─────────────────────────────────────────────────────────────────────────────

module private MapperRegistry =
  let private store = System.Collections.Generic.Dictionary<Type, obj>()

  let set<'Action when 'Action: comparison>(m: IInputMapper<'Action>) =
    store[typeof<IInputMapper<'Action>>] <- box m

  let tryGet<'Action when 'Action: comparison>() =
    match store.TryGetValue(typeof<IInputMapper<'Action>>) with
    | true, (:? IInputMapper<'Action> as m) -> ValueSome m
    | _ -> ValueNone

/// <summary>
/// Subscription-based input mapping.
/// </summary>
/// <remarks>
/// <para>This is intentionally "push" driven: it listens to raw <see cref="T:Mibo.Input.IInput"/> deltas and dispatches a
/// user message that contains the mapped <see cref="T:Mibo.Input.ActionState`1"/>.</para>
/// <para>Why this exists:</para>
/// <ul>
/// <li>keeps the user's update signature unchanged (no context parameter)</li>
/// <li>user can opt into any model strategy by handling a single message</li>
/// <li>supports dynamic remapping via a <c>getMap</c> callback (e.g., backed by a ref/agent)</li>
/// </ul>
/// </remarks>
module InputMapper =

  /// <summary>
  /// Subscribe to mapped action state changes.
  /// </summary>
  /// <remarks>
  /// <para><see cref="F:Mibo.Input.ActionState`1.Started"/>/<see cref="F:Mibo.Input.ActionState`1.Released"/> are one-shot sets relative to the most recent hardware delta batch.</para>
  /// <para>If you store the state in your model, you typically want to clear one-shots each frame
  /// (or just treat them as event-like).</para>
  /// </remarks>
  let subscribe
    (getMap: unit -> InputMap<'Action>)
    (toMsg: ActionState<'Action> -> 'Msg)
    (ctx: GameContext)
    : Sub<'Msg> =
    let subId = SubId.ofString "Mibo/Input/InputMapper/subscribe"

    let subscribeFn(dispatch: Dispatch<'Msg>) =
      let input = Input.getService ()
      let mutable state = ActionState.empty

      let apply(isDown: bool, trigger: Trigger) =
        let map = getMap ()
        state <- ActionState.update map isDown trigger state

      let subKey =
        input.KeyboardDelta.Subscribe(fun d ->
          // Treat each delta batch as a logical "tick" for one-shot fields.
          state <- ActionState.nextFrame state

          for k in d.Pressed do
            apply (true, Key k)

          for k in d.Released do
            apply (false, Key k)

          dispatch (toMsg state))

      let subMouse =
        input.MouseDelta.Subscribe(fun d ->
          state <- ActionState.nextFrame state

          if d.Buttons.LeftPressed then
            apply (true, MouseBut 0)

          if d.Buttons.LeftReleased then
            apply (false, MouseBut 0)

          if d.Buttons.RightPressed then
            apply (true, MouseBut 1)

          if d.Buttons.RightReleased then
            apply (false, MouseBut 1)

          if d.Buttons.MiddlePressed then
            apply (true, MouseBut 2)

          if d.Buttons.MiddleReleased then
            apply (false, MouseBut 2)

          dispatch (toMsg state))

      { new IDisposable with
          member _.Dispose() =
            subKey.Dispose()
            subMouse.Dispose()
      }

    Sub.Active(subId, subscribeFn)

  /// Convenience overload for static mappings.
  let subscribeStatic
    (map: InputMap<'Action>)
    (toMsg: ActionState<'Action> -> 'Msg)
    (ctx: GameContext)
    : Sub<'Msg> =
    subscribe (fun () -> map) toMsg ctx

  /// Attempts to get the IInputMapper service from the registry.
  ///
  /// Returns ValueNone if the service is not registered.
  let tryGetService<'Action when 'Action: comparison>
    (ctx: GameContext)
    : IInputMapper<'Action> voption =
    MapperRegistry.tryGet<'Action> ()

  /// Gets the IInputMapper service from the registry.
  ///
  /// Throws if the service is not registered.
  let getService<'Action when 'Action: comparison>
    (ctx: GameContext)
    : IInputMapper<'Action> =
    match MapperRegistry.tryGet<'Action> () with
    | ValueSome m -> m
    | ValueNone ->
      failwith
        "IInputMapper service not registered."

/// Service Implementation
type InputMapperService<'Action when 'Action: comparison>
  (input: IInput, initialMap: InputMap<'Action>) as this =
  let mutable map = initialMap
  let mutable state = ActionState.empty

  // Internal buffer for incoming events this frame
  let pendingEvents = ResizeArray<(bool * Trigger)>()

  let subKey =
    input.KeyboardDelta.Subscribe(fun d ->
      for k in d.Pressed do
        pendingEvents.Add(true, Key k)

      for k in d.Released do
        pendingEvents.Add(false, Key k))

  let subMouse =
    input.MouseDelta.Subscribe(fun d ->
      if d.Buttons.LeftPressed then
        pendingEvents.Add(true, MouseBut 0)

      if d.Buttons.LeftReleased then
        pendingEvents.Add(false, MouseBut 0)

      if d.Buttons.RightPressed then
        pendingEvents.Add(true, MouseBut 1)

      if d.Buttons.RightReleased then
        pendingEvents.Add(false, MouseBut 1))

  do
    MapperRegistry.set (this :> IInputMapper<'Action>)

  interface IDisposable with
    member _.Dispose() =
      subKey.Dispose()
      subMouse.Dispose()

  interface IInputMapper<'Action> with
    member _.CurrentState = state

    /// Called at start of frame to process pending hardware events
    member _.Update() =
      // Reset one-shot states
      state <- ActionState.nextFrame state

      // Process pending
      for (isDown, trigger) in pendingEvents do
        state <- ActionState.update map isDown trigger state

      pendingEvents.Clear()
