module PlatformerSample.View

open System
open System.Collections.Generic
open System.Numerics
open Raylib_cs
open Mibo.Elmish
open Mibo.Elmish.Graphics2D
open Mibo.Elmish.Graphics2D.Lighting
open Mibo.Layout
open Mibo.Animation
open PlatformerSample.Constants
open PlatformerSample.Types
open PlatformerSample.DayNight
open PlatformerSample.WorldGen

let inline r (x: int) (y: int) (w: int) (h: int) =
  Rectangle(float32 x, float32 y, float32 w, float32 h)

// -------------------------------------------------------------
// Lighting & Rendering
// -------------------------------------------------------------

let view (ctx: GameContext) (model: Model) (buffer: RenderBuffer2D) =
  model.Lighting.Reset()

  let playerCenterX = model.PlayerPosition.X + playerWidth / 2.0f
  let playerCenterY = model.PlayerPosition.Y + playerHeight / 2.0f

  let camera =
    Camera2D(
      Vector2(float32 ctx.WindowWidth / 2.0f, float32 ctx.WindowHeight / 2.0f),
      Vector2(model.CameraPos.X, model.CameraPos.Y),
      0.0f,
      1.0f
    )

  // Sky background and day/night ambient
  let time = model.DayNightTimeOfDay
  let dayNight = { TimeOfDay = model.DayNightTimeOfDay; DayDuration = model.DayNightDuration }
  let skyTop, skyBot = DayNight.getSkyColors time
  let ambient = DayNight.getAmbientColor time
  let sunIntensity = DayNight.getSunIntensity time
  let moonIntensity = DayNight.getMoonIntensity time
  let sunPos, moonPos = DayNight.orbitalPositions playerCenterX dayNight

  let viewBounds =
    Camera2D.viewportBoundsFromRaylib
      camera
      (float32 ctx.WindowWidth)
      (float32 ctx.WindowHeight)

  buffer
  |> Draw.rectGradientV
    (-1000<RenderLayer>)
    (0, 0, ctx.WindowWidth, ctx.WindowHeight, skyTop, skyBot)
  |> Draw.beginCamera 0<RenderLayer> camera
  |> LightDraw.setAmbient model.Lighting (5<RenderLayer>, { Color = ambient })
  |> Draw.drop

  // Sun directional light
  if sunIntensity > 0.0f then
    let sunDir =
      Vector2.Normalize(Vector2(playerCenterX, groundLevel - 200.0f) - sunPos)
    buffer
    |> LightDraw.addDirectionalLight model.Lighting (6<RenderLayer>) {
      Direction = sunDir
      Color = Color(255uy, 245uy, 220uy)
      Intensity = sunIntensity * 1.5f
      CastsShadows = true
    }
    |> Draw.drop

  // Moon directional light
  if moonIntensity > 0.0f then
    let moonDir =
      Vector2.Normalize(Vector2(playerCenterX, groundLevel - 200.0f) - moonPos)
    buffer
    |> LightDraw.addDirectionalLight model.Lighting (6<RenderLayer>) {
      Direction = moonDir
      Color = Color(180uy, 200uy, 255uy)
      Intensity = moonIntensity * 0.8f
      CastsShadows = true
    }
    |> Draw.drop

  // Collect occluders and torches from nearby chunks
  let pcx = int(Math.Floor(float model.PlayerPosition.X / float chunkWorldSize))
  let pcy = int(Math.Floor(float model.PlayerPosition.Y / float chunkWorldSize))

  let mutable nearbyOccluders = []
  let mutable nearbyTorches = []

  for KeyValue(key, chunk) in model.Chunks do
    let struct(cx, cy) = key
    if abs (cx - pcx) <= chunkLoadRadius && abs (cy - pcy) <= chunkLoadRadius then
      nearbyOccluders <- chunk.Occluders |> Array.toList |> List.append nearbyOccluders
      nearbyTorches <- chunk.Torches |> Array.toList |> List.append nearbyTorches

  // Sort by distance to player and take nearest N
  let playerPos = model.PlayerPosition
  let occludersSorted =
    nearbyOccluders
    |> List.sortBy (fun o ->
      let mx = (o.P1.X + o.P2.X) * 0.5f
      let my = (o.P1.Y + o.P2.Y) * 0.5f
      (mx - playerPos.X) * (mx - playerPos.X) + (my - playerPos.Y) * (my - playerPos.Y)
    )
    |> List.truncate maxOccluders

  let torchesSorted =
    nearbyTorches
    |> List.sortBy (fun t ->
      let dx = t.Position.X - playerPos.X
      let dy = t.Position.Y - playerPos.Y
      dx * dx + dy * dy
    )
    |> List.truncate maxTorchLights

  // Add torches as point lights and draw sprites
  let torchSrc = AnimatedSprite.currentSource model.TorchSprite
  for torch in torchesSorted do
    buffer
    |> LightDraw.addPointLight model.Lighting (7<RenderLayer>) {
      Position = torch.Position
      Color = torch.Color
      Intensity = 1.2f
      Radius = torch.Radius
      Falloff = 1.5f
      CastsShadows = false
    }
    |> Draw.drop

    let torchDest = r (int torch.Position.X - 16) (int torch.Position.Y - 32) 32 32
    buffer
    |> LightDraw.litSprite model.Lighting {
      Texture = model.Assets.TorchSheet.Texture
      Dest = torchDest
      Source = torchSrc
      Origin = Vector2.Zero
      Rotation = 0.0f
      Color = Color.White
      Layer = 7<RenderLayer>
    }
    |> Draw.drop

  // Add occluders
  for occluder in occludersSorted do
    buffer
    |> LightDraw.addOccluder model.Lighting (8<RenderLayer>) occluder
    |> Draw.drop

  // Render visible tiles from nearby chunks only
  let tileSrc = r 260 585 64 64

  for KeyValue(key, chunk) in model.Chunks do
    let struct(cx, cy) = key
    if abs (cx - pcx) <= chunkLoadRadius && abs (cy - pcy) <= chunkLoadRadius then
      if Culling.isVisible2D viewBounds chunk.Bounds then
        CellGrid2D.iterVisible
          (int viewBounds.X)
          (int viewBounds.Y)
          (int (viewBounds.X + viewBounds.Width))
          (int (viewBounds.Y + viewBounds.Height))
          (fun x y tile ->
            if tile <> TileType.Empty then
              let wx = chunk.Grid.Origin.X + float32 x * tileSize
              let wy = chunk.Grid.Origin.Y + float32 y * tileSize
              let dest = Rectangle(wx, wy, tileSize, tileSize)
              buffer
              |> LightDraw.litSprite model.Lighting {
                Texture = model.Assets.TileTexture
                Dest = dest
                Source = tileSrc
                Origin = Vector2.Zero
                Rotation = 0.0f
                Color = Color.White
                Layer = 10<RenderLayer>
              }
              |> Draw.drop
          )
          chunk.Grid

  // Lit player sprite
  let playerSrc = AnimatedSprite.currentSource model.PlayerSprite
  let mutable playerSrcMut = playerSrc
  if model.PlayerSprite.FlipX then
    playerSrcMut <- Rectangle(
      playerSrcMut.X, playerSrcMut.Y,
      -playerSrcMut.Width, playerSrcMut.Height
    )

  let playerDrawY = int(model.PlayerPosition.Y + playerHeight - 64.0f)
  let playerDest = r (int model.PlayerPosition.X) playerDrawY 64 64
  buffer
  |> LightDraw.litSprite model.Lighting {
    Texture = model.Assets.PlayerSheet.Texture
    Dest = playerDest
    Source = playerSrcMut
    Origin = Vector2.Zero
    Rotation = 0.0f
    Color = Color.White
    Layer = 20<RenderLayer>
  }
  |> Draw.drop

  // Particles
  buffer
  |> ParticleDraw.particles
    model.Assets.ParticleTexture
    model.Particles
    model.ParticleCount
    3<RenderLayer>

  // End lighting
  |> LightDraw.endLighting model.Lighting 999<RenderLayer>
  // End camera
  |> Draw.endCamera 1000<RenderLayer>
  // UI
  |> Draw.text {
    Font = model.Assets.Font
    Text =
      $"Day/Night Cycle | Time: {model.DayNightTimeOfDay:F1}h | Chunks: {model.Chunks.Count} | Pos: %.1f{model.PlayerPosition.X},%.1f{model.PlayerPosition.Y} | WASD/Arrows: Move | Space: Jump | R: Respawn"
    Position = Vector2(10.0f, 10.0f)
    FontSize = 20.0f
    Spacing = 1.0f
    Color = Color.White
    Layer = 1001<RenderLayer>
  }
  |> Draw.drop
