namespace Mibo.Elmish

open System

type Effect<'Msg> = delegate of ('Msg -> unit) -> unit

[<Struct>]
type Cmd<'Msg> =
    | Empty
    | Single of single: Effect<'Msg>
    | Batch of batch: Effect<'Msg>[]
    | DeferNextFrame of batch: Effect<'Msg>[]
    | NowAndDeferNextFrame of now: Effect<'Msg>[] * next: Effect<'Msg>[]

module Cmd =
    let none: Cmd<'Msg> = Empty

    let inline ofEffect(eff: Effect<'Msg>) = Single eff

    let inline ofMsg(msg: 'Msg) : Cmd<'Msg> =
        Single(Effect<'Msg>(fun dispatch -> dispatch msg))

    let inline deferNextFrame(cmd: Cmd<'Msg>) : Cmd<'Msg> =
        match cmd with
        | Empty -> Empty
        | Single eff -> DeferNextFrame [| eff |]
        | Batch effs -> DeferNextFrame effs
        | DeferNextFrame effs -> DeferNextFrame effs
        | NowAndDeferNextFrame(now, next) ->
            let combined = Array.zeroCreate<Effect<'Msg>>(now.Length + next.Length)
            Array.Copy(now, 0, combined, 0, now.Length)
            Array.Copy(next, 0, combined, now.Length, next.Length)
            DeferNextFrame combined

    let inline private split(cmd: Cmd<'Msg>) : struct (Effect<'Msg>[] * Effect<'Msg>[]) =
        match cmd with
        | Empty -> struct ([||], [||])
        | Single eff -> struct ([| eff |], [||])
        | Batch effs -> struct (effs, [||])
        | DeferNextFrame effs -> struct ([||], effs)
        | NowAndDeferNextFrame(now, next) -> struct (now, next)

    let map (f: 'A -> 'Msg) (cmd: Cmd<'A>) : Cmd<'Msg> =
        match cmd with
        | Empty -> Empty
        | Single eff ->
            Single(
                Effect<'Msg>(fun dispatch ->
                    let innerDispatch(a: 'A) = dispatch(f a)
                    eff.Invoke(innerDispatch))
            )
        | Batch effs ->
            let mapped = Array.zeroCreate<Effect<'Msg>> effs.Length
            for i = 0 to effs.Length - 1 do
                let eff = effs[i]
                mapped[i] <-
                    Effect<'Msg>(fun dispatch ->
                        let innerDispatch(a: 'A) = dispatch(f a)
                        eff.Invoke(innerDispatch))
            Batch mapped
        | DeferNextFrame effs ->
            let mapped = Array.zeroCreate<Effect<'Msg>> effs.Length
            for i = 0 to effs.Length - 1 do
                let eff = effs[i]
                mapped[i] <-
                    Effect<'Msg>(fun dispatch ->
                        let innerDispatch(a: 'A) = dispatch(f a)
                        eff.Invoke(innerDispatch))
            DeferNextFrame mapped
        | NowAndDeferNextFrame(now, next) ->
            let mapBatch(effs: Effect<'A>[]) : Effect<'Msg>[] =
                let mapped = Array.zeroCreate<Effect<'Msg>> effs.Length
                for i = 0 to effs.Length - 1 do
                    let eff = effs[i]
                    mapped[i] <-
                        Effect<'Msg>(fun dispatch ->
                            let innerDispatch(a: 'A) = dispatch(f a)
                            eff.Invoke(innerDispatch))
                mapped
            NowAndDeferNextFrame(mapBatch now, mapBatch next)

    let batch(cmds: seq<Cmd<'Msg>>) : Cmd<'Msg> =
        let mutable nowCount = 0
        let mutable nextCount = 0

        for c in cmds do
            let struct (now, next) = split c
            nowCount <- nowCount + now.Length
            nextCount <- nextCount + next.Length

        if nowCount = 0 && nextCount = 0 then
            Empty
        elif nextCount = 0 then
            if nowCount = 1 then
                let mutable eff = Unchecked.defaultof<Effect<'Msg>>
                for c in cmds do
                    match c with
                    | Single e -> eff <- e
                    | Batch b when b.Length = 1 -> eff <- b[0]
                    | _ -> ()
                Single eff
            else
                let arr = Array.zeroCreate<Effect<'Msg>> nowCount
                let mutable i = 0
                for c in cmds do
                    let struct (now, _) = split c
                    if now.Length <> 0 then
                        Array.Copy(now, 0, arr, i, now.Length)
                        i <- i + now.Length
                Batch arr
        elif nowCount = 0 then
            let arr = Array.zeroCreate<Effect<'Msg>> nextCount
            let mutable i = 0
            for c in cmds do
                let struct (_, next) = split c
                if next.Length <> 0 then
                    Array.Copy(next, 0, arr, i, next.Length)
                    i <- i + next.Length
            DeferNextFrame arr
        else
            let nowArr = Array.zeroCreate<Effect<'Msg>> nowCount
            let nextArr = Array.zeroCreate<Effect<'Msg>> nextCount
            let mutable ni = 0
            let mutable xi = 0
            for c in cmds do
                let struct (now, next) = split c
                if now.Length <> 0 then
                    Array.Copy(now, 0, nowArr, ni, now.Length)
                    ni <- ni + now.Length
                if next.Length <> 0 then
                    Array.Copy(next, 0, nextArr, xi, next.Length)
                    xi <- xi + next.Length
            NowAndDeferNextFrame(nowArr, nextArr)

    let batch2(a: Cmd<'Msg>, b: Cmd<'Msg>) : Cmd<'Msg> =
        batch [ a; b ]

    let batch3(a: Cmd<'Msg>, b: Cmd<'Msg>, c: Cmd<'Msg>) : Cmd<'Msg> =
        batch [ a; b; c ]

    let batch4(a: Cmd<'Msg>, b: Cmd<'Msg>, c: Cmd<'Msg>, d: Cmd<'Msg>) : Cmd<'Msg> =
        batch [ a; b; c; d ]

    let ofAsync
        (task: Async<'T>)
        (ofSuccess: 'T -> 'Msg)
        (ofError: exn -> 'Msg)
        : Cmd<'Msg> =
        Single(
            Effect<'Msg>(fun dispatch ->
                async {
                    try
                        let! result = task
                        dispatch(ofSuccess result)
                    with ex ->
                        dispatch(ofError ex)
                }
                |> Async.StartImmediate)
        )

    let ofTask
        (task: Threading.Tasks.Task<'T>)
        (ofSuccess: 'T -> 'Msg)
        (ofError: exn -> 'Msg)
        : Cmd<'Msg> =
        Single(
            Effect<'Msg>(fun dispatch ->
                async {
                    try
                        let! result = task |> Async.AwaitTask
                        dispatch(ofSuccess result)
                    with ex ->
                        dispatch(ofError ex)
                }
                |> Async.StartImmediate)
        )
