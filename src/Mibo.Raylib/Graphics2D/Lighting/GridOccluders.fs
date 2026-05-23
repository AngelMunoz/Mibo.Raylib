namespace Mibo.Elmish.Graphics2D.Lighting

open System
open System.Numerics
open Mibo.Layout

/// <summary>
/// Helpers for generating shadow-casting occluders from grid-based levels.
/// </summary>
module GridOccluders =

  /// <summary>
  /// Generates <see cref="Occluder2D"/> line segments for every exposed edge
  /// of solid cells in a <see cref="CellGrid2D"/>.
  /// </summary>
  /// <param name="isSolid">Predicate that returns true for solid/obstacle cell contents.</param>
  /// <param name="grid">The grid to scan.</param>
  let fromCellGrid (isSolid: 'T -> bool) (grid: CellGrid2D<'T>) : Occluder2D[] =
    let occluders = ResizeArray<Occluder2D>()
    let cellW = grid.CellSize.X
    let cellH = grid.CellSize.Y

    for y in 0 .. grid.Height - 1 do
      for x in 0 .. grid.Width - 1 do
        match CellGrid2D.get x y grid with
        | ValueNone -> ()
        | ValueSome tile ->
          if isSolid tile then
            let wx = grid.Origin.X + float32 x * cellW
            let wy = grid.Origin.Y + float32 y * cellH

            // NOTE: We intentionally do NOT generate top-edge occluders.
            // In a 2D platformer, the top surface of tiles is the ground
            // the player stands on — it must receive light from above.
            // Shadow casting is handled by bottom, left, and right edges.

            // Bottom edge
            match CellGrid2D.get x (y + 1) grid with
            | ValueNone ->
              occluders.Add({
                P1 = Vector2(wx, wy + cellH)
                P2 = Vector2(wx + cellW, wy + cellH)
              })
            | ValueSome neighbor ->
              if not (isSolid neighbor) then
                occluders.Add({
                  P1 = Vector2(wx, wy + cellH)
                  P2 = Vector2(wx + cellW, wy + cellH)
                })

            // Left edge
            match CellGrid2D.get (x - 1) y grid with
            | ValueNone ->
              occluders.Add({
                P1 = Vector2(wx, wy)
                P2 = Vector2(wx, wy + cellH)
              })
            | ValueSome neighbor ->
              if not (isSolid neighbor) then
                occluders.Add({
                  P1 = Vector2(wx, wy)
                  P2 = Vector2(wx, wy + cellH)
                })

            // Right edge
            match CellGrid2D.get (x + 1) y grid with
            | ValueNone ->
              occluders.Add({
                P1 = Vector2(wx + cellW, wy)
                P2 = Vector2(wx + cellW, wy + cellH)
              })
            | ValueSome neighbor ->
              if not (isSolid neighbor) then
                occluders.Add({
                  P1 = Vector2(wx + cellW, wy)
                  P2 = Vector2(wx + cellW, wy + cellH)
                })

    occluders.ToArray()
