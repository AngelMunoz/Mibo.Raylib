namespace Mibo.Elmish

open System
open System.Collections.Generic

type GameContext =
    { WindowWidth: int
      WindowHeight: int
      Assets: IAssets }

type IRenderer<'Model> =
    abstract Draw: GameContext * 'Model * GameTime -> unit

type RenderBuffer<'Key, 'Cmd when 'Key: comparison>(?capacity: int, ?keyComparer: IComparer<'Key>) =

    let initialCapacity = defaultArg capacity 1024

    let mutable items =
        System.Buffers.ArrayPool<struct ('Key * 'Cmd)>.Shared.Rent initialCapacity

    let mutable count = 0
    let keyComparer = defaultArg keyComparer Comparer<'Key>.Default

    let sortComparer =
        { new IComparer<struct ('Key * 'Cmd)> with
            member _.Compare(struct (k1, _), struct (k2, _)) = keyComparer.Compare(k1, k2) }

    let ensureCapacity (needed: int) =
        if count + needed > items.Length then
            let newSize = max (items.Length * 2) (count + needed)
            let newArr = System.Buffers.ArrayPool<struct ('Key * 'Cmd)>.Shared.Rent(newSize)
            items.AsSpan(0, count).CopyTo(newArr.AsSpan())
            System.Buffers.ArrayPool<struct ('Key * 'Cmd)>.Shared.Return items
            items <- newArr

    member _.Clear() = count <- 0

    member _.Add(key: 'Key, cmd: 'Cmd) =
        ensureCapacity 1
        items[count] <- struct (key, cmd)
        count <- count + 1

    member _.Sort() =
        System.Array.Sort(items, 0, count, sortComparer)

    member _.Count = count

    member _.Item(i) = items[i]
