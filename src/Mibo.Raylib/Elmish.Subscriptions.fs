namespace Mibo.Elmish

open System
open System.Collections.Generic
open FSharp.UMX

[<Measure>]
type subId

type SubId = string<subId>

module SubId =
    let inline ofString (value: string) : SubId = UMX.tag<subId> value
    let inline value (id: SubId) : string = UMX.untag id

    let inline prefix (prefix: string) (id: SubId) : SubId =
        if String.IsNullOrEmpty(prefix) then
            id
        else
            let idStr = value id

            if String.IsNullOrEmpty(idStr) then
                ofString prefix
            else
                ofString (prefix + "/" + idStr)

type Dispatch<'Msg> = 'Msg -> unit
type Subscribe<'Msg> = Dispatch<'Msg> -> IDisposable

[<Struct>]
type Sub<'Msg> =
    | NoSub
    | Active of SubId * Subscribe<'Msg>
    | BatchSub of Sub<'Msg>[]

module Sub =
    let none = NoSub

    let batch (subs: seq<Sub<'Msg>>) : Sub<'Msg> =
        let inline isNoSub s =
            match s with
            | NoSub -> true
            | _ -> false

        let mutable count = 0

        for s in subs do
            match s with
            | NoSub -> ()
            | Active _ -> count <- count + 1
            | BatchSub b -> count <- count + b.Length

        if count = 0 then
            NoSub
        elif count = 1 then
            let mutable found = NoSub

            for s in subs do
                match s with
                | NoSub -> ()
                | Active _ ->
                    if isNoSub found then
                        found <- s
                | BatchSub b ->
                    if b.Length = 1 && isNoSub found then
                        found <- b[0]

            found
        else
            let arr = Array.zeroCreate<Sub<'Msg>> count
            let mutable i = 0

            for s in subs do
                match s with
                | NoSub -> ()
                | Active _ ->
                    arr[i] <- s
                    i <- i + 1
                | BatchSub b ->
                    Array.Copy(b, 0, arr, i, b.Length)
                    i <- i + b.Length

            BatchSub arr

    let inline batch2 (a: Sub<'Msg>, b: Sub<'Msg>) : Sub<'Msg> = batch [ a; b ]

    let inline batch3 (a: Sub<'Msg>, b: Sub<'Msg>, c: Sub<'Msg>) : Sub<'Msg> = batch [ a; b; c ]

    let inline batch4 (a: Sub<'Msg>, b: Sub<'Msg>, c: Sub<'Msg>, d: Sub<'Msg>) : Sub<'Msg> = batch [ a; b; c; d ]

    [<TailCall>]
    let rec internal flatten (stack: ResizeArray<Sub<'Msg>>) (results: ResizeArray<struct (SubId * Subscribe<'Msg>)>) =
        if stack.Count = 0 then
            ()
        else
            let last = stack.Count - 1
            let s = stack.[last]
            stack.RemoveAt(last)

            match s with
            | NoSub -> ()
            | Active(id, func) -> results.Add(struct (id, func))
            | BatchSub subs ->
                for i = subs.Length - 1 downto 0 do
                    stack.Add(subs[i])

            flatten stack results

    [<Struct>]
    type private MapWork<'A> =
        | Visit of sub: Sub<'A>
        | BuildBatch of len: int

    let map (idPrefix: string) (f: 'A -> 'Msg) (sub: Sub<'A>) : Sub<'Msg> =
        let work = ResizeArray<MapWork<'A>>(64)
        let results = ResizeArray<Sub<'Msg>>(64)

        work.Add(Visit sub)

        while work.Count <> 0 do
            let last = work.Count - 1
            let item = work.[last]
            work.RemoveAt(last)

            match item with
            | Visit s ->
                match s with
                | NoSub -> results.Add(NoSub)
                | Active(subId, subscribe) ->
                    let newId = SubId.prefix idPrefix subId

                    let newSub (dispatch: Dispatch<'Msg>) =
                        let innerDispatch msgA = dispatch (f msgA)
                        subscribe innerDispatch

                    results.Add(Active(newId, newSub))
                | BatchSub subs ->
                    let len = subs.Length
                    work.Add(BuildBatch len)

                    for i = len - 1 downto 0 do
                        work.Add(Visit subs[i])

            | BuildBatch len ->
                if len = 0 then
                    results.Add(BatchSub [||])
                else
                    let start = results.Count - len
                    let mapped = Array.zeroCreate<Sub<'Msg>> len

                    for i = 0 to len - 1 do
                        mapped.[i] <- results.[start + i]

                    results.RemoveRange(start, len)
                    results.Add(BatchSub mapped)

        if results.Count = 0 then
            NoSub
        else
            results.[results.Count - 1]
