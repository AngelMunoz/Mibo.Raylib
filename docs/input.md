---
title: Input
category: Amenities
categoryindex: 5
index: 20
---

# Input (raw + mapped)

Mibo.Raylib supports input via **semantic input mapping** (hardware → actions) using `InputMap` + `Keyboard.poll`.

Raw keyboard polling (`Keyboard.poll`) is available now. Mouse, touch, and gamepad polling are planned.

## Semantic input mapping (actions)

Gameplay reads better when it talks about **actions** (Jump, Fire, Interact) instead of **keys**. Mibo.Raylib provides `InputMap` and `ActionState` for this purpose.

### Define your action type

```fsharp
type Action =
    | MoveLeft
    | MoveRight
    | Jump
    | Fire
```

### Build an `InputMap`

```fsharp
open Raylib_cs
open Mibo.Elmish

let map =
    InputMap.empty
    |> InputMap.key MoveLeft KeyboardKey.A
    |> InputMap.key MoveLeft KeyboardKey.Left
    |> InputMap.key Jump KeyboardKey.Space
```

### Poll and consume

Because Mibo.Raylib has no subscription-based input yet, you poll inside your `Tick` handler using `Keyboard.poll`. You pass the previous frame's `ActionState` to compute deltas (Started/Released).

```fsharp
open Mibo.Elmish

type Msg =
    | InputMapped of ActionState<Action>
    | Tick of GameTime

let init (ctx: GameContext) =
    struct ({ Actions = ActionState.empty }, Cmd.none)

let update msg model =
    match msg with
    | Tick gt ->
        let actions = Keyboard.poll map model.Actions
        if actions.Started.Contains Jump then
            // do jump
            ()
        struct ({ model with Actions = actions }, Cmd.none)
```

`ActionState` gives you three sets each frame:

| Field     | Description                           |
|-----------|---------------------------------------|
| `Held`    | Actions whose keys are currently down |
| `Started` | Actions pressed this frame            |
| `Released`| Actions released this frame           |

### Dynamic remapping

You can swap the `InputMap` at runtime by passing a different map to `Keyboard.poll`.

```fsharp
let mapRef = ref map

let update msg model =
    match msg with
    | Tick _ ->
        let actions = Keyboard.poll mapRef.Value model.Actions
        // ...
```

## Planned features

The following input features are not yet implemented in Mibo.Raylib but are planned:

- **Mouse polling** (`Mouse.poll` with position and button state)
- **Gamepad polling** (`Gamepad.poll` for controller input)
- **Touch input** (mobile touch events)
- **`InputMapper` module** (subscription-based mapping with `IInputMapper` service)
- **Input subscriptions** (`Keyboard.onPressed`, `Mouse.onMove`, etc.)
- **`IInput` service** (raw delta observable pattern)

## See Also

- [Subscriptions](subscriptions.html) - Continuous input handling
- [Scaling](scaling.html) - Input handling patterns
