module PlatformerSample.Constants

open System.Numerics

[<Literal>]
let tileSize = 64.0f

[<Literal>]
let chunkCells = 32

let chunkWorldSize = float32 chunkCells * tileSize // 2048

[<Literal>]
let playerWidth = 40.0f

[<Literal>]
let playerHeight = 54.0f

[<Literal>]
let gravity = 1200.0f

[<Literal>]
let moveSpeed = 300.0f

[<Literal>]
let jumpSpeed = -700.0f

[<Literal>]
let worldHeight = 12.0f

let groundLevel = worldHeight * tileSize
let groundSurface = groundLevel - tileSize

[<Literal>]
let chunkLoadRadius = 2

[<Literal>]
let chunkEvictRadius = 4

[<Literal>]
let maxOccluders = 128

[<Literal>]
let maxTorchLights = 16

[<Literal>]
let viewportWidth = 1280.0f

[<Literal>]
let viewportHeight = 720.0f

[<Literal>]
let spawnX = 200.0f

[<Literal>]
let spawnProtectedCells = 5 // first 5 cells (0-320px) are pit-free
