module ThreeDSample.WorldGen

open System
open System.Collections.Generic
open System.Numerics
open Raylib_cs
open Mibo.Layout3D
open ThreeDSample.Constants
open ThreeDSample.Types

let chunkSeed cx cz worldSeed =
  cx * 73856093 ^^^ cz * 19349663 ^^^ worldSeed

let generateChunk cx cz worldSeed : Chunk =
  let rng = Random(chunkSeed cx cz worldSeed)

  let origin =
    Vector3(float32 cx * chunkWorldWidth, 0.0f, float32 cz * chunkWorldDepth)

  let isSnow = (abs cx + abs cz) % 3 = 0

  let grid =
    CellGrid3D.create
      chunkWidth
      chunkHeight
      chunkDepth
      (Vector3(cellSize, cellSize, cellSize))
      origin

  // ── Ground floor via DSL ──
  Layout3D.run
    (fun s ->
      s
      |> Layout3D.floorXZ
        0
        0
        0
        chunkWidth
        chunkDepth
        (if isSnow then BlockType.SnowGround else BlockType.Ground))
    grid
  |> ignore

  // ── Pits (gaps in ground) ──
  let pitCount = rng.Next(2, 6)

  for _ in 0..pitCount do
    let px = rng.Next(1, chunkWidth - 6)
    let pz = rng.Next(1, chunkDepth - 6)
    let pw = rng.Next(2, 5)
    let pd = rng.Next(2, 5)

    Layout3D.run (fun s -> s |> Layout3D.clear px 0 pz pw 1 pd) grid |> ignore

  // ── Mid-level platforms via DSL fill ──
  let platformCount = rng.Next(2, 5)

  for _ in 0..platformCount do
    let px = rng.Next(1, chunkWidth - 8)
    let pz = rng.Next(1, chunkDepth - 8)
    let py = 3 + rng.Next(0, 3)
    let pw = rng.Next(2, 7)
    let pd = rng.Next(2, 7)

    Layout3D.run
      (fun s -> s |> Layout3D.floorXZ px py pz pw pd BlockType.Platform)
      grid
    |> ignore

  // ── Stairs (procedural) ──
  if rng.Next(2) = 0 then
    let sx = rng.Next(4, chunkWidth - 10)
    let sz = rng.Next(4, chunkDepth - 10)

    for step in 0..4 do
      CellGrid3D.set (sx + step) (1 + step) sz BlockType.Ground grid

  // ── High platforms ──
  if rng.Next(3) = 0 then
    let px = rng.Next(2, chunkWidth - 8)
    let pz = rng.Next(2, chunkDepth - 8)
    let py = 6 + rng.Next(0, 4)
    let pw = rng.Next(2, 5)
    let pd = rng.Next(2, 5)

    Layout3D.run
      (fun s -> s |> Layout3D.floorXZ px py pz pw pd BlockType.Platform)
      grid
    |> ignore

  // ── Pillars via DSL column ──
  let pillarCount = rng.Next(0, 3)

  for _ in 0..pillarCount do
    let px = rng.Next(1, chunkWidth - 3)
    let pz = rng.Next(1, chunkDepth - 3)
    let ph = rng.Next(2, 6)

    Layout3D.run
      (fun s -> s |> Layout3D.column px 0 pz ph BlockType.Ground)
      grid
    |> ignore

  // ── Spikes ──
  if rng.Next(3) = 0 then
    let sx = rng.Next(2, chunkWidth - 6)
    let sz = rng.Next(2, chunkDepth - 6)
    let sw = rng.Next(1, 4)
    let sd = rng.Next(1, 4)

    Layout3D.run
      (fun s -> s |> Layout3D.floorXZ sx 1 sz sw sd BlockType.Spikes)
      grid
    |> ignore

  // ── Decorations via scatter ──
  let treeCount = rng.Next(1, 4)
  let treeType = if isSnow then BlockType.TreeSnow else BlockType.TreePine

  Layout3D.run
    (fun s -> s |> Layout3D.scatterXZ 1 treeCount (rng.Next()) treeType)
    grid
  |> ignore

  let rockCount = rng.Next(0, 3)

  Layout3D.run
    (fun s -> s |> Layout3D.scatterXZ 1 rockCount (rng.Next()) BlockType.Rock)
    grid
  |> ignore

  let grassCount = rng.Next(2, 8)

  Layout3D.run
    (fun s ->
      s |> Layout3D.scatterXZ 1 grassCount (rng.Next()) BlockType.GrassTuft)
    grid
  |> ignore

  // ── Glowing mushrooms (light sources) ──
  let lanternCount = rng.Next(1, 3)

  Layout3D.run
    (fun s ->
      s
      |> Layout3D.scatterXZ 1 lanternCount (rng.Next()) BlockType.MushroomLight)
    grid
  |> ignore

  // ── Coins on elevated platforms (procedural — needs neighbor checks) ──
  let coinCount = rng.Next(2, 8)

  for _ in 0..coinCount do
    let cx' = rng.Next(2, chunkWidth - 3)
    let cz' = rng.Next(2, chunkDepth - 3)

    for cy in 1 .. chunkHeight - 2 do
      let below =
        match CellGrid3D.get cx' (cy - 1) cz' grid with
        | ValueSome bt when BlockType.isSolid bt -> true
        | _ -> false

      let current =
        match CellGrid3D.get cx' cy cz' grid with
        | ValueSome _ -> true
        | _ -> false

      if below && not current then
        CellGrid3D.set cx' cy cz' BlockType.Coin grid

  // ── Flag ──
  let fx = rng.Next(2, chunkWidth - 3)
  let fz = rng.Next(2, chunkDepth - 3)

  for fy in 1 .. chunkHeight - 2 do
    let below =
      match CellGrid3D.get fx (fy - 1) fz grid with
      | ValueSome bt when BlockType.isSolid bt -> true
      | _ -> false

    let current =
      match CellGrid3D.get fx fy fz grid with
      | ValueSome _ -> true
      | _ -> false

    if below && not current then
      CellGrid3D.set fx fy fz BlockType.Flag grid

  {
    Grid = grid
    Bounds =
      BoundingBox(
        origin,
        origin
        + Vector3(
          chunkWorldWidth,
          float32 chunkHeight * cellSize,
          chunkWorldDepth
        )
      )
    OriginX = cx
    OriginZ = cz
  }

let loadChunks
  (playerPos: Vector3)
  (chunks: Dictionary<struct (int * int), Chunk>)
  (seed: int)
  =
  let pcx = int(Math.Floor(float playerPos.X / float chunkWorldWidth))
  let pcz = int(Math.Floor(float playerPos.Z / float chunkWorldDepth))

  for x in pcx - chunkLoadRadius .. pcx + chunkLoadRadius do
    for z in pcz - chunkLoadRadius .. pcz + chunkLoadRadius do
      let key = struct (x, z)

      if not(chunks.ContainsKey(key)) then
        chunks[key] <- generateChunk x z seed

let evictDistantChunks
  (playerPos: Vector3)
  (chunks: Dictionary<struct (int * int), Chunk>)
  (keysToRemove: ResizeArray<struct (int * int)>)
  =
  let pcx = int(Math.Floor(float playerPos.X / float chunkWorldWidth))
  let pcz = int(Math.Floor(float playerPos.Z / float chunkWorldDepth))
  keysToRemove.Clear()

  for KeyValue(key, _) in chunks do
    let struct (cx, cz) = key

    if abs(cx - pcx) > chunkEvictRadius || abs(cz - pcz) > chunkEvictRadius then
      keysToRemove.Add key

  for i = 0 to keysToRemove.Count - 1 do
    chunks.Remove keysToRemove[i] |> ignore
