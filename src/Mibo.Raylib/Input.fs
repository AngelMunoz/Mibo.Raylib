namespace Mibo.Elmish

open System
open System.Collections.Generic
open Raylib_cs

type KeyboardKey = Raylib_cs.KeyboardKey

type ActionState<'Action when 'Action: comparison> = {
    Held: Set<'Action>
    Started: Set<'Action>
    Released: Set<'Action>
}

module ActionState =
    let empty: ActionState<'Action> = {
        Held = Set.empty
        Started = Set.empty
        Released = Set.empty
    }

type InputMap<'Action when 'Action: comparison> =
    private { Bindings: Map<KeyboardKey, 'Action> }

module InputMap =
    let empty<'Action when 'Action: comparison> : InputMap<'Action> =
        { Bindings = Map.empty }

    let key action k (map: InputMap<'Action>) =
        { Bindings = Map.add k action map.Bindings }

module Keyboard =
    let poll<'Action when 'Action: comparison>
        (map: InputMap<'Action>)
        (previous: ActionState<'Action>)
        : ActionState<'Action> =

        let mutable held = Set.empty
        let mutable started = Set.empty
        let mutable released = Set.empty

        for KeyValue(k, action) in map.Bindings do
            let isDown = RaylibHelpers.isKeyDown(k)
            let wasDown = previous.Held.Contains(action)

            if isDown then
                held <- held.Add(action)
                if not wasDown then
                    started <- started.Add(action)
            elif wasDown then
                released <- released.Add(action)

        { Held = held; Started = started; Released = released }
