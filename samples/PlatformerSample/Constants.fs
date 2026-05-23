module PlatformerSample.Constants

open System.Numerics

let tileSize = 64.0f
let chunkCells = 32
let chunkWorldSize = float32 chunkCells * tileSize  // 2048
let playerWidth = 40.0f
let playerHeight = 54.0f
let gravity = 1200.0f
let moveSpeed = 300.0f
let jumpSpeed = -700.0f
let worldHeight = 12.0f
let groundLevel = worldHeight * tileSize
let groundSurface = groundLevel - tileSize
let chunkLoadRadius = 2
let chunkEvictRadius = 4
let maxOccluders = 128
let maxTorchLights = 16
let viewportWidth = 1280.0f
let viewportHeight = 720.0f
let spawnX = 200.0f
let spawnProtectedCells = 5  // first 5 cells (0-320px) are pit-free
