namespace Mibo.Elmish.Graphics2D.Lighting

open System
open System.Numerics
open Raylib_cs
open Mibo.Elmish.Graphics2D

/// <summary>A single 2D particle rendered as a textured quad with optional sprite-sheet source rect.</summary>
/// <remarks>
/// This is a render snapshot. Simulation state (velocity, lifetime, spin, color rules) lives in the
/// user's model and is written into this struct at the start of the view function.
/// </remarks>
[<Struct>]
type Particle2D = {
  /// <summary>Center position in world/screen space.</summary>
  Position: Vector2

  /// <summary>Width and height of the quad.</summary>
  Size: Vector2

  /// <summary>Rotation in degrees around the center.</summary>
  Rotation: float32

  /// <summary>Source rectangle within the texture in pixels. Use (0, 0, tw, th) for the full texture.</summary>
  SourceRect: Rectangle

  /// <summary>Tint color. Alpha controls transparency.</summary>
  Color: Color
}

[<Struct>]
type ParticleCommand
  (
    texture: Texture2D,
    particles: Particle2D[],
    count: int,
    cmdLayer: int<RenderLayer>
  ) =
  interface IRenderCommand2D with
    member _.Layer = cmdLayer

    member _.Render _ =
      let tex = texture
      let ps = particles
      let c = count

      for i = 0 to c - 1 do
        let p = ps[i]
        let halfW = p.Size.X * 0.5f
        let halfH = p.Size.Y * 0.5f
        let dest = Rectangle(
          p.Position.X - halfW,
          p.Position.Y - halfH,
          p.Size.X,
          p.Size.Y
        )
        Raylib.DrawTexturePro(tex, p.SourceRect, dest, Vector2.Zero, p.Rotation, p.Color)

/// <summary>Factory functions for particle render commands.</summary>
module ParticleCommands =

  /// <summary>
  /// Creates a batched particle render command. All particles are drawn as textured quads
  /// in a single draw call via Rlgl immediate mode.
  /// </summary>
  /// <param name="texture">The texture applied to every particle.</param>
  /// <param name="particles">Array of particle render snapshots. Managed by the user.</param>
  /// <param name="count">Number of active particles in the array.</param>
  /// <param name="layer">Render layer for ordering.</param>
  let inline particles
    (texture: Texture2D)
    (particles: Particle2D[])
    (count: int)
    (layer: int<RenderLayer>)
    : IRenderCommand2D =
    ParticleCommand(texture, particles, count, layer)

/// <summary>Pipe-friendly wrappers for particle drawing.</summary>
module ParticleDraw =

  /// <summary>Adds a batched particle render command to the buffer. Returns the buffer for chaining.</summary>
  let inline particles
    (texture: Texture2D)
    (particles: Particle2D[])
    (count: int)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer2D)
    =
    buffer.Add(ParticleCommands.particles texture particles count layer)
    buffer

/// <summary>Helpers for particle simulation. Called in the user's update function.</summary>
/// <remarks>
/// These operate on the simulation state (velocity, lifetime, etc.), not the render snapshot.
/// After simulation, map your sim state to <see cref="T:Mibo.Elmish.Graphics2D.Lighting.Particle2D"/>
/// render snapshots for the view function.
/// </remarks>
module ParticleSimulation =

  /// <summary>
  /// Fades particles by reducing alpha and compacts the dead ones out of the array.
  /// Particles with alpha &lt;= 0 are removed. Returns the new count via the byref parameter.
  /// Call this in your Tick handler after updating positions/velocities.
  /// </summary>
  /// <param name="particles">The particle render snapshot array. Mutated in place.</param>
  /// <param name="count">Current active count. Updated to reflect compacted array.</param>
  /// <param name="fadeSpeed">Alpha reduction per second. 255.0f means a particle fades completely in 1 second.</param>
  /// <param name="dt">Delta time in seconds.</param>
  let inline fadeAndCompact
    (particles: Particle2D[])
    (count: int byref)
    (fadeSpeed: float32)
    (dt: float32)
    =
    let fadeAmount = fadeSpeed * dt
    let mutable writeIdx = 0

    for readIdx = 0 to count - 1 do
      let p = particles[readIdx]
      let newAlpha = MathF.Max(0.0f, float32 p.Color.A - fadeAmount)

      if newAlpha > 0.0f then
        let newColor = Color(p.Color.R, p.Color.G, p.Color.B, byte newAlpha)
        particles[writeIdx] <- { p with Color = newColor }
        writeIdx <- writeIdx + 1

    count <- writeIdx
