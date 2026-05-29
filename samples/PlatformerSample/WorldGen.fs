module PlatformerSample.WorldGen

open System
open System.Collections.Generic
open System.Numerics
open Raylib_cs
open Mibo.Layout
open Mibo.Elmish.Graphics2D.Lighting
open PlatformerSample.Constants
open PlatformerSample.Types

// -------------------------------------------------------------
// Chunk Seeding & Generation
// -------------------------------------------------------------

let chunkSeed (cx: int) (cy: int) (worldSeed: int) =
  cx * 73856093 + cy * 19349663 + worldSeed

let extractPlatforms(grid: CellGrid2D<TileType>) : Rectangle[] =
  let platforms = ResizeArray<Rectangle>()
  let cellW = grid.CellSize.X
  let cellH = grid.CellSize.Y

  for y in 0 .. grid.Height - 1 do
    let mutable x = 0

    while x < grid.Width do
      match CellGrid2D.get x y grid with
      | ValueSome Ground
      | ValueSome Platform ->
        let startX = x
        let mutable runLength = 1
        let mutable more = true

        while more && x + runLength < grid.Width do
          match CellGrid2D.get (x + runLength) y grid with
          | ValueSome Ground
          | ValueSome Platform -> runLength <- runLength + 1
          | _ -> more <- false

        let wx = grid.Origin.X + float32 startX * cellW
        let wy = grid.Origin.Y + float32 y * cellH
        platforms.Add(Rectangle(wx, wy, float32 runLength * cellW, cellH))
        x <- x + runLength
      | _ -> x <- x + 1

  platforms.ToArray()

let extractTorches (grid: CellGrid2D<TileType>) (rng: Random) : TorchLight[] =
  let torches = ResizeArray<TorchLight>()
  let cellW = grid.CellSize.X

  for y in 0 .. grid.Height - 1 do
    let mutable x = 0

    while x < grid.Width do
      match CellGrid2D.get x y grid with
      | ValueSome Ground
      | ValueSome Platform ->
        match CellGrid2D.get x (y - 1) grid with
        | ValueNone ->
          if rng.NextDouble() > 0.92 then
            let wx = grid.Origin.X + float32 x * cellW + cellW * 0.5f
            let wy = grid.Origin.Y + float32 y * grid.CellSize.Y - 10.0f

            torches.Add {
              Position = Vector2(wx, wy)
              Color = Color(255uy, 160uy, 60uy)
              Radius = 100.0f + float32(rng.Next(-20, 20))
            }
        | _ -> ()

        x <- x + 1
      | _ -> x <- x + 1

  torches.ToArray()

let generateChunk (cx: int) (cy: int) (worldSeed: int) : Chunk =
  let rng = Random(chunkSeed cx cy worldSeed)
  let origin = Vector2(float32 cx * chunkWorldSize, float32 cy * chunkWorldSize)

  let grid =
    CellGrid2D.create chunkCells chunkCells (Vector2(tileSize, tileSize)) origin

  let groundY = int worldHeight // 12

  if cy = 0 then
    // Ground chunk: floor with pits and floating platforms
    Layout.run
      (fun section ->
        section
        |> Layout.section 0 groundY (fun groundSection ->
          groundSection |> Platformer.platform chunkCells Ground |> ignore

          // Pits, but never in spawn-protected area
          let pitCount = rng.Next(1, 4)

          for _ in 1..pitCount do
            let px = rng.Next(spawnProtectedCells, chunkCells - 5)
            let pw = rng.Next(2, 5)

            groundSection
            |> Layout.section px 0 (Platformer.pit pw 1)
            |> ignore

          groundSection)
        |> ignore

        // Floating platforms above ground (reachable: 64-128px jump)
        let platCount = rng.Next(1, 4)

        for _ in 1..platCount do
          let px = rng.Next(0, chunkCells - 8)
          let py = rng.Next(groundY - 3, groundY - 1) // rows 9-10: 128-192px above ground
          let pw = rng.Next(3, 8)

          section
          |> Layout.section px py (Platformer.platform pw Platform)
          |> ignore

        section)
      grid
    |> ignore

  // Air chunks (cy < 0) kept empty for now

  let platforms = extractPlatforms grid
  let torches = extractTorches grid rng
  // Only floating platforms cast shadows — ground does not
  let occluders =
    GridOccluders.fromCellGrid
      (fun t -> t = Platform)
      (GridOccluders.Edge.Bottom
       ||| GridOccluders.Edge.Left
       ||| GridOccluders.Edge.Right)
      grid

  {
    Grid = grid
    Platforms = platforms
    Occluders = occluders
    Torches = torches
    Bounds = Rectangle(origin.X, origin.Y, chunkWorldSize, chunkWorldSize)
  }

let loadChunks
  (playerPos: Vector2)
  (chunks: Dictionary<struct (int * int), Chunk>)
  (seed: int)
  =
  let pcx = int(Math.Floor(float playerPos.X / float chunkWorldSize))
  let pcy = int(Math.Floor(float playerPos.Y / float chunkWorldSize))

  for x in pcx - chunkLoadRadius .. pcx + chunkLoadRadius do
    for y in pcy - chunkLoadRadius .. pcy + chunkLoadRadius do
      if x >= 0 then // no generation before spawn
        let key = struct (x, y)

        if not(chunks.ContainsKey(key)) then
          chunks[key] <- generateChunk x y seed

let evictDistantChunks
  (playerPos: Vector2)
  (chunks: Dictionary<struct (int * int), Chunk>)
  (keysToRemove: ResizeArray<struct (int * int)>)
  =
  let pcx = int(Math.Floor(float playerPos.X / float chunkWorldSize))
  let pcy = int(Math.Floor(float playerPos.Y / float chunkWorldSize))
  keysToRemove.Clear()

  for KeyValue(key, _) in chunks do
    let struct (cx, cy) = key

    if abs(cx - pcx) > chunkEvictRadius || abs(cy - pcy) > chunkEvictRadius then
      keysToRemove.Add key

  for i = 0 to keysToRemove.Count - 1 do
    chunks.Remove keysToRemove[i] |> ignore
