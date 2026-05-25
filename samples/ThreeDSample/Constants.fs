module ThreeDSample.Constants

open System.Numerics

[<Literal>]
let cellSize = 1.0f

[<Literal>]
let chunkWidth = 32
[<Literal>]
let chunkHeight = 16
[<Literal>]
let chunkDepth = 32

let chunkWorldWidth = float32 chunkWidth * cellSize
let chunkWorldDepth = float32 chunkDepth * cellSize

[<Literal>]
let playerHeight = 1.8f
[<Literal>]
let playerRadius = 0.3f

[<Literal>]
let gravity = -20.0f
[<Literal>]
let jumpSpeed = 12.0f
[<Literal>]
let moveSpeed = 8.0f
[<Literal>]
let acceleration = 25.0f
[<Literal>]
let friction = 8.0f
[<Literal>]
let fallLimit = -30.0f

[<Literal>]
let cameraDistance = 8.0f
[<Literal>]
let cameraHeightOffset = 2.0f
[<Literal>]
let cameraLerpSpeed = 10.0f
[<Literal>]
let cameraDefaultPitch = 0.15f
[<Literal>]
let cameraDefaultYaw = System.MathF.PI / 4.0f

[<Literal>]
let mouseSensitivity = 0.003f

[<Literal>]
let viewportWidth = 1280.0f
[<Literal>]
let viewportHeight = 720.0f

[<Literal>]
let chunkLoadRadius = 1
[<Literal>]
let chunkEvictRadius = 4

let spawnPosition = Vector3(float32 chunkWidth / 2.0f, 10.0f, float32 chunkDepth / 2.0f)

module KenneyModels =
  let basePath = "assets/kenney_platformer-kit/Models/"

  let blockGrass = basePath + "block-grass.glb"
  let blockGrassLarge = basePath + "block-grass-large.glb"
  let blockGrassTall = basePath + "block-grass-large-tall.glb"
  let blockGrassSlope = basePath + "block-grass-large-slope.glb"
  let blockGrassSlopeSteep = basePath + "block-grass-large-slope-steep.glb"
  let blockGrassNarrow = basePath + "block-grass-narrow.glb"
  let blockGrassEdge = basePath + "block-grass-edge.glb"
  let blockGrassCorner = basePath + "block-grass-corner.glb"

  let blockSnow = basePath + "block-snow.glb"
  let blockSnowLarge = basePath + "block-snow-large.glb"
  let blockSnowTall = basePath + "block-snow-large-tall.glb"
  let blockSnowSlope = basePath + "block-snow-large-slope.glb"

  let platform = basePath + "platform.glb"
  let platformFortified = basePath + "platform-fortified.glb"
  let platformRamp = basePath + "platform-ramp.glb"
  let platformOverhang = basePath + "platform-overhang.glb"

  let characterOobi = basePath + "character-oobi.glb"
  let characterOodi = basePath + "character-oodi.glb"
  let characterOoli = basePath + "character-ooli.glb"
  let characterOopi = basePath + "character-oopi.glb"
  let characterOozi = basePath + "character-oozi.glb"

  let coinGold = basePath + "coin-gold.glb"
  let coinSilver = basePath + "coin-silver.glb"
  let coinBronze = basePath + "coin-bronze.glb"
  let jewel = basePath + "jewel.glb"
  let heart = basePath + "heart.glb"
  let star = basePath + "star.glb"
  let key = basePath + "key.glb"

  let spikeBlock = basePath + "spike-block.glb"
  let spikeBlockWide = basePath + "spike-block-wide.glb"
  let trapSpikes = basePath + "trap-spikes.glb"
  let trapSpikesLarge = basePath + "trap-spikes-large.glb"
  let saw = basePath + "saw.glb"

  let treePine = basePath + "tree-pine.glb"
  let treePineSmall = basePath + "tree-pine-small.glb"
  let treeSnow = basePath + "tree-snow.glb"
  let rocks = basePath + "rocks.glb"
  let stones = basePath + "stones.glb"
  let grass = basePath + "grass.glb"
  let flowers = basePath + "flowers.glb"
  let flowersTall = basePath + "flowers-tall.glb"
  let mushrooms = basePath + "mushrooms.glb"

  let fenceStraight = basePath + "fence-straight.glb"
  let fenceCorner = basePath + "fence-corner.glb"
  let fenceRope = basePath + "fence-rope.glb"

  let crate = basePath + "crate.glb"
  let barrel = basePath + "barrel.glb"
  let chest = basePath + "chest.glb"

  let ladder = basePath + "ladder.glb"
  let ladderLong = basePath + "ladder-long.glb"

  let sign = basePath + "sign.glb"
  let flag = basePath + "flag.glb"
  let arrow = basePath + "arrow.glb"

  let bomb = basePath + "bomb.glb"
  let spring = basePath + "spring.glb"

  let doorOpen = basePath + "door-open.glb"
  let doorRotate = basePath + "door-rotate.glb"

  let brick = basePath + "brick.glb"
  let pipe = basePath + "pipe.glb"
  let poles = basePath + "poles.glb"
  let conveyorBelt = basePath + "conveyor-belt.glb"
  let plant = basePath + "plant.glb"
  let hedge = basePath + "hedge.glb"
  let hedgeCorner = basePath + "hedge-corner.glb"
