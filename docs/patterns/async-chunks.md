---
title: Async Chunk Loading
category: Patterns
categoryindex: 6
index: 63
---

# Async Chunk Loading

## What and Why

Open-world games can't generate all terrain at startup. You load chunks around the player as they move and evict distant ones to reclaim memory. The key constraint: generation must not block the main thread.

This pattern uses `Cmd.ofAsync` to run world generation on a background thread, with a `HashSet` to prevent duplicate requests and a `ConcurrentDictionary` for thread-safe chunk storage.

## When to use

- Your world is larger than what fits in memory at once.
- Terrain generation takes more than a few milliseconds (noise, procedural placement, etc.).
- You want smooth movement without frame hitches when entering new areas.
- Multiple systems read chunk data concurrently (rendering, collision, minimap).

## Quick Start

```fsharp
// 1. Store chunks in a ConcurrentDictionary
model.Chunks : ConcurrentDictionary<struct (int * int), Chunk>

// 2. Track pending requests to avoid duplicates
model.PendingChunks : HashSet<struct (int * int)>

// 3. Generate on background thread, deliver result as a message
let generateChunkAsync (cx: int) (cz: int) (seed: int) : Cmd<Msg> =
  Cmd.ofAsync
    (async { return generateChunk cx cz seed })
    (fun chunk -> ChunkCreated(struct (cx, cz), chunk))
    (fun _ex -> ChunkCreated(struct (cx, cz), generateChunk cx cz seed))
```

## Deep Dive

### The chunk system

`chunkSystem` runs every frame. It checks which chunks near the player are missing, queues async generation for each, and evicts distant chunks.

```fsharp
let chunkSystem (dt: float32) (model: GameModel) : struct (GameModel * Cmd<Msg>) =
  // Which chunk is the player in?
  let pcx = int(Math.Floor(float model.PlayerPosition.X / float chunkWorldWidth))
  let pcz = int(Math.Floor(float model.PlayerPosition.Z / float chunkWorldDepth))

  let keysToGenerate = ResizeArray<struct (int * int)>()

  for x in pcx - chunkLoadRadius .. pcx + chunkLoadRadius do
    for z in pcz - chunkLoadRadius .. pcz + chunkLoadRadius do
      let key = struct (x, z)

      if not(model.Chunks.ContainsKey(key))
        && not(model.PendingChunks.Contains(key)) then
        model.PendingChunks.Add(key) |> ignore
        keysToGenerate.Add(key)

  evictDistantChunks model.PlayerPosition model.Chunks model.KeysToRemove

  if keysToGenerate.Count = 0 then
    struct (model, Cmd.none)
  else
    let cmd =
      Cmd.batch [|
        for struct (x, z) in keysToGenerate do
          generateChunkAsync x z model.Seed
      |]
    struct (model, cmd)
```

### Preventing duplicate requests

The `PendingChunks` HashSet is the guard. Before queuing a chunk for generation:

1. Check if it already exists in `Chunks` (loaded).
2. Check if it's in `PendingChunks` (already requested).
3. If neither, add to `PendingChunks` and fire the async command.

When the `ChunkCreated` message arrives, the chunk is stored and the key is removed from `PendingChunks`:

```fsharp
| ChunkCreated(key, chunk) ->
  model.Chunks[key] <- chunk
  model.PendingChunks.Remove(key) |> ignore
  struct (model, Cmd.none)
```

### Evicting distant chunks

Chunks far from the player get removed to free memory:

```fsharp
let evictDistantChunks playerPos chunks keysToRemove =
  let pcx = int(Math.Floor(float playerPos.X / float chunkWorldWidth))
  let pcz = int(Math.Floor(float playerPos.Z / float chunkWorldDepth))
  keysToRemove.Clear()

  for KeyValue(key, _) in chunks do
    let struct (cx, cz) = key
    if abs(cx - pcx) > chunkEvictRadius || abs(cz - pcz) > chunkEvictRadius then
      keysToRemove.Add key

  for i = 0 to keysToRemove.Count - 1 do
    chunks.TryRemove(keysToRemove[i]) |> ignore
```

> _**TIP**_: Use `chunkLoadRadius` for loading and a larger `chunkEvictRadius` for eviction. This creates a buffer zone — chunks don't load and unload every time the player crosses a boundary.

### Thread safety

`ConcurrentDictionary` lets the main thread (rendering, collision) read chunks while background threads write new ones. No locks needed for reads.

> _**IMPORTANT**_: Never iterate `ConcurrentDictionary` and mutate it in the same pass. The eviction code collects keys first, then removes in a separate loop.

### Error handling

The error callback in `Cmd.ofAsync` provides a fallback. In the sample it retries synchronously, but you could return a placeholder chunk or skip it:

```fsharp
Cmd.ofAsync
  (async { return generateChunk cx cz seed })
  (fun chunk -> ChunkCreated(struct (cx, cz), chunk))
  (fun _ex -> ChunkCreated(struct (cx, cz), generateChunk cx cz seed))
```

### Loading chunks at startup

For the initial area, generate synchronously so chunks are ready before the first frame:

```fsharp
let loadInitialChunks (model: GameModel) =
  let pcx = int(Math.Floor(float spawnPosition.X / float chunkWorldWidth))
  let pcz = int(Math.Floor(float spawnPosition.Z / float chunkWorldDepth))

  for x in pcx - chunkLoadRadius .. pcx + chunkLoadRadius do
    for z in pcz - chunkLoadRadius .. pcz + chunkLoadRadius do
      let key = struct (x, z)
      if not(model.Chunks.ContainsKey key) then
        model.Chunks[key] <- generateChunk x z model.Seed
```

See also: [System Pipeline](system-pipeline.html), [3D Rendering Overview](graphics3d/overview.html).
