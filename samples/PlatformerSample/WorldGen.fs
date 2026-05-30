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
  cx * 73856093 ^^^ cy * 19349663 ^^^ worldSeed

// -------------------------------------------------------------
// Tile Extraction Helpers
// -------------------------------------------------------------

let private collectTiles
  (grid: CellGrid2D<TileType>)
  (predicate: TileType -> bool)
  : Rectangle[] =
  let result = ResizeArray<Rectangle>()
  let cellW = grid.CellSize.X
  let cellH = grid.CellSize.Y

  for y in 0 .. grid.Height - 1 do
    for x in 0 .. grid.Width - 1 do
      match CellGrid2D.get x y grid with
      | ValueSome tile when predicate tile ->
        let wx = grid.Origin.X + float32 x * cellW
        let wy = grid.Origin.Y + float32 y * cellH
        result.Add(Rectangle(wx, wy, cellW, cellH))
      | _ -> ()

  result.ToArray()

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

let extractSpikes(grid: CellGrid2D<TileType>) : Rectangle[] =
  collectTiles grid (fun t -> t = Spikes)

let extractCoins(grid: CellGrid2D<TileType>) : Rectangle[] =
  collectTiles grid (fun t -> t = Coin)

let extractFlags(grid: CellGrid2D<TileType>) : Rectangle[] =
  collectTiles grid (fun t -> t = Flag)

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

// -------------------------------------------------------------
// Chunk Generation
// -------------------------------------------------------------

let generateChunk (cx: int) (cy: int) (worldSeed: int) : Chunk =
  let rng = Random(chunkSeed cx cy worldSeed)
  let origin = Vector2(float32 cx * chunkWorldSize, float32 cy * chunkWorldSize)

  let grid =
    CellGrid2D.create chunkCells chunkCells (Vector2(tileSize, tileSize)) origin

  let groundY = int worldHeight

  // Biome based on chunk position (like ThreeDSample's isSnow)
  let biome =
    let v = (abs cx + abs cy) % 4
    match v with
    | 0 -> Grass
    | 1 -> Stone
    | 2 -> Snow
    | _ -> Sand

  if cy = 0 then
    // ── Ground level: 5 archetypes ──
    let archetype = rng.Next(100)

    if archetype < 40 then
      // Archetype: Ground + pits
      Layout.run
        (fun section ->
          section
          |> Layout.section 0 groundY (fun groundSection ->
            groundSection |> Platformer.platform chunkCells Ground |> ignore

            let pitCount = rng.Next(1, 4)

            for _ in 1..pitCount do
              let px = rng.Next(spawnProtectedCells, chunkCells - 5)
              let pw = rng.Next(2, 5)

              groundSection
              |> Layout.section px 0 (Platformer.pit pw 1)
              |> ignore

            groundSection)
          |> ignore

          let platCount = rng.Next(1, 4)

          for _ in 1..platCount do
            let px = rng.Next(0, chunkCells - 8)
            let py = rng.Next(groundY - 3, groundY - 1)
            let pw = rng.Next(3, 8)

            section
            |> Layout.section px py (Platformer.platform pw Platform)
            |> ignore

          section)
        grid
      |> ignore

    elif archetype < 60 then
      // Archetype: Ground + stairs
      Layout.run
        (fun section ->
          section
          |> Layout.section 0 groundY (fun groundSection ->
            groundSection |> Platformer.platform chunkCells Ground |> ignore

            let pitCount = rng.Next(0, 2)

            for _ in 1..pitCount do
              let px = rng.Next(spawnProtectedCells, chunkCells - 5)
              let pw = rng.Next(2, 4)

              groundSection
              |> Layout.section px 0 (Platformer.pit pw 1)
              |> ignore

            groundSection)
          |> ignore

          // Stairs
          let sx = rng.Next(4, chunkCells - 10)

          let stairDir =
            if rng.Next(2) = 0 then
              Platformer.UpRight
            else
              Platformer.UpLeft

          section
          |> Layout.section
            sx
            (groundY - 6)
            (Platformer.stairs 5 Platform stairDir)
          |> ignore

          // Some floating platforms
          let platCount = rng.Next(1, 3)

          for _ in 1..platCount do
            let px = rng.Next(0, chunkCells - 6)
            let py = rng.Next(groundY - 4, groundY - 2)
            let pw = rng.Next(3, 6)

            section
            |> Layout.section px py (Platformer.platform pw Platform)
            |> ignore

          section)
        grid
      |> ignore

    elif archetype < 85 then
      // Archetype: Ground + dense floating platforms
      Layout.run
        (fun section ->
          section
          |> Layout.section 0 groundY (fun groundSection ->
            groundSection |> Platformer.platform chunkCells Ground |> ignore

            let pitCount = rng.Next(0, 3)

            for _ in 1..pitCount do
              let px = rng.Next(spawnProtectedCells, chunkCells - 4)
              let pw = rng.Next(2, 4)

              groundSection
              |> Layout.section px 0 (Platformer.pit pw 1)
              |> ignore

            groundSection)
          |> ignore

          // Dense floating platforms at multiple heights
          for row in 1..3 do
            let platCount = rng.Next(2, 5)

            for _ in 1..platCount do
              let px = rng.Next(0, chunkCells - 6)
              let py = groundY - 1 - row * 2 + rng.Next(0, 2)
              let pw = rng.Next(2, 6)

              if py >= 0 then
                section
                |> Layout.section px py (Platformer.platform pw Platform)
                |> ignore

          section)
        grid
      |> ignore

    elif archetype < 95 then
      // Archetype: Ground + spikes
      Layout.run
        (fun section ->
          section
          |> Layout.section 0 groundY (fun groundSection ->
            groundSection |> Platformer.platform chunkCells Ground |> ignore

            // Spikes on some ground rows
            let spikeRow = groundY - 1

            for x in 0 .. chunkCells - 1 do
              if rng.Next(8) = 0 then
                Layout.set x spikeRow Spikes groundSection |> ignore

            groundSection)
          |> ignore

          // Fewer platforms since spikes add danger
          let platCount = rng.Next(1, 3)

          for _ in 1..platCount do
            let px = rng.Next(0, chunkCells - 6)
            let py = rng.Next(groundY - 3, groundY - 1)
            let pw = rng.Next(3, 6)

            section
            |> Layout.section px py (Platformer.platform pw Platform)
            |> ignore

          section)
        grid
      |> ignore

    else
      // Archetype: Ground + treasures
      Layout.run
        (fun section ->
          section
          |> Layout.section 0 groundY (fun groundSection ->
            groundSection |> Platformer.platform chunkCells Ground |> ignore

            let pitCount = rng.Next(1, 3)

            for _ in 1..pitCount do
              let px = rng.Next(spawnProtectedCells, chunkCells - 5)
              let pw = rng.Next(2, 4)

              groundSection
              |> Layout.section px 0 (Platformer.pit pw 1)
              |> ignore

            groundSection)
          |> ignore

          // Platforms with coins above
          let platCount = rng.Next(2, 5)

          for _ in 1..platCount do
            let px = rng.Next(0, chunkCells - 6)
            let py = rng.Next(groundY - 4, groundY - 1)
            let pw = rng.Next(3, 6)

            section
            |> Layout.section px py (Platformer.platform pw Platform)
            |> ignore

            // Coins above platform
            for cx in 0 .. pw - 1 do
              if rng.Next(3) = 0 then
                Layout.set (px + cx) (py - 1) Coin section |> ignore

          section)
        grid
      |> ignore

    // Flag on ground level (one per chunk)
    let flagX = rng.Next(2, chunkCells - 2)

    Layout.run (fun s -> s |> Layout.set flagX (groundY - 1) Flag) grid
    |> ignore

    // Decorations on ground surface
    let decoCount = rng.Next(2, 5)

    for _ in 1..decoCount do
      let dx = rng.Next(0, chunkCells)

      match CellGrid2D.get dx (groundY - 1) grid with
      | ValueSome Ground ->
        match CellGrid2D.get dx (groundY - 2) grid with
        | ValueNone ->
          if rng.Next(3) = 0 then
            Layout.run (fun s -> s |> Layout.set dx (groundY - 2) Ground) grid
            |> ignore
        | _ -> ()
      | _ -> ()

  elif cy < 0 then
    // ── Air chunks: 3 archetypes ──
    let archetype = rng.Next(100)

    if archetype < 30 then
      () // Empty sky
    elif archetype < 80 then
      // Floating platform clusters — placed near bottom of chunk (close to ground below)
      let clusterCount = rng.Next(1, 4)

      for _ in 1..clusterCount do
        let px = rng.Next(0, chunkCells - 8)
        let py = rng.Next(chunkCells - 8, chunkCells - 2)
        let pw = rng.Next(3, 8)

        Layout.run
          (fun s -> s |> Layout.section px py (Platformer.platform pw Platform))
          grid
        |> ignore

        // Occasional coin above platform
        if rng.Next(3) = 0 then
          let coinX = px + rng.Next(0, pw)

          Layout.run (fun s -> s |> Layout.set coinX (py - 1) Coin) grid
          |> ignore

    else
      // Pillar chains — limited height
      let pillarCount = rng.Next(1, 3)

      for _ in 1..pillarCount do
        let px = rng.Next(2, chunkCells - 4)
        let pillarHeight = rng.Next(2, 5)
        let py = chunkCells - pillarHeight - 1

        for dy in 0 .. pillarHeight - 1 do
          if py + dy < chunkCells then
            Layout.run (fun s -> s |> Layout.set px (py + dy) Platform) grid
            |> ignore

        // Horizontal platform at top
        let hWidth = rng.Next(3, 6)

        if py - 1 >= 0 then
          Layout.run
            (fun s ->
              s
              |> Layout.section
                (px - 1)
                (py - 1)
                (Platformer.platform hWidth Platform))
            grid
          |> ignore

  else
    // ── Underground: 2 archetypes ──
    let archetype = rng.Next(100)

    if archetype < 70 then
      // Gaps/caves — ground with large gaps
      Layout.run
        (fun section ->
          section |> Platformer.platform chunkCells Ground |> ignore

          // Large gaps
          let gapCount = rng.Next(2, 5)

          for _ in 1..gapCount do
            let gx = rng.Next(1, chunkCells - 6)
            let gy = rng.Next(1, chunkCells - 4)
            let gw = rng.Next(3, 6)
            let gh = rng.Next(2, 4)

            section |> Layout.section gx gy (Platformer.gap gw gh) |> ignore

          // Occasional coins in caves
          let coinCount = rng.Next(1, 4)

          for _ in 1..coinCount do
            let cx = rng.Next(1, chunkCells - 2)
            let cy = rng.Next(1, chunkCells - 2)

            match CellGrid2D.get cx cy grid with
            | ValueSome Ground ->
              match CellGrid2D.get cx (cy - 1) grid with
              | ValueNone ->
                Layout.run (fun s -> s |> Layout.set cx (cy - 1) Coin) grid
                |> ignore
              | _ -> ()
            | _ -> ()

          section)
        grid
      |> ignore

    else
      // Dense platforms
      Layout.run
        (fun section ->
          section |> Platformer.platform chunkCells Ground |> ignore

          let platCount = rng.Next(3, 7)

          for _ in 1..platCount do
            let px = rng.Next(0, chunkCells - 6)
            let py = rng.Next(2, chunkCells - 3)
            let pw = rng.Next(3, 7)

            section
            |> Layout.section px py (Platformer.platform pw Platform)
            |> ignore

          section)
        grid
      |> ignore

  // ── Extract game objects ──
  let platforms = extractPlatforms grid
  let spikes = extractSpikes grid
  let coins = extractCoins grid
  let flags = extractFlags grid
  let torches = extractTorches grid rng

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
    Spikes = spikes
    Coins = coins
    Flags = flags
    Occluders = occluders
    Torches = torches
    Bounds = Rectangle(origin.X, origin.Y, chunkWorldSize, chunkWorldSize)
    Biome = biome
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
      let key = struct (x, y)

      if not (chunks.ContainsKey(key)) then
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
