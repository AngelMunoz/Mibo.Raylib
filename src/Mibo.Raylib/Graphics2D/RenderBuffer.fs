namespace Mibo.Elmish.Graphics2D

open System
open System.Buffers
open System.Collections.Generic

/// <summary>
/// An allocation-friendly buffer for 2D render commands, sorted by layer.
/// </summary>
/// <remarks>
/// Commands are accumulated each frame via <see cref="M:Mibo.Elmish.Graphics2D.RenderBuffer2D.Add"/>,
/// sorted by <see cref="P:Mibo.Elmish.Graphics2D.IRenderCommand2D.Layer"/>, then executed in order.
/// Uses <see cref="T:System.Buffers.ArrayPool`1"/> for the backing store to avoid per-frame
/// heap allocations.
///
/// The buffer is designed to be cleared and repopulated each frame.
/// <see cref="M:Mibo.Elmish.Graphics2D.RenderBuffer2D.Clear"/> resets the count
/// without deallocating the internal array.
/// </remarks>
type RenderBuffer2D
  (
    /// <summary>Initial capacity. Defaults to 1024 if not specified.</summary>
    ?capacity: int
  ) =

  let mutable items =
    ArrayPool<IRenderCommand2D>.Shared.Rent(defaultArg capacity 1024)

  let mutable count = 0

  let layerComparer =
    { new IComparer<IRenderCommand2D> with
        member _.Compare(a, b) =
          int a.Layer - int b.Layer
    }

  let ensureCapacity(needed: int) =
    if count + needed > items.Length then
      let newSize = max (items.Length * 2) (count + needed)

      let newArr =
        ArrayPool<IRenderCommand2D>.Shared.Rent(newSize)

      Array.Copy(items, newArr, count)
      ArrayPool<IRenderCommand2D>.Shared.Return(items)
      items <- newArr

  /// <summary>The number of commands currently in the buffer.</summary>
  member _.Count = count

  /// <summary>Gets the command at the specified index.</summary>
  member _.Item(i: int) = items[i]

  /// <summary>Adds a render command to the buffer.</summary>
  member _.Add(cmd: IRenderCommand2D) =
    ensureCapacity 1
    items[count] <- cmd
    count <- count + 1

  /// <summary>
  /// Clears all commands from the buffer without deallocating the backing array.
  /// Call this at the start of each frame before populating with new commands.
  /// </summary>
  member _.Clear() = count <- 0

  /// <summary>
  /// Sorts commands by <see cref="P:Mibo.Elmish.Graphics2D.IRenderCommand2D.Layer"/> in ascending order.
  /// Must be called after <see cref="M:Mibo.Elmish.Graphics2D.RenderBuffer2D.Clear"/>
  /// and population, before iteration.
  /// </summary>
  member _.Sort() =
    Array.Sort(items, 0, count, layerComparer)
