namespace Mibo.Elmish.Graphics2D.Lighting

open System.Numerics
open Raylib_cs

/// <summary>Global ambient light applied to all lit sprites.</summary>
[<Struct>]
type AmbientLight2D = {
  /// <summary>Base illumination color. Lower values produce darker scenes.</summary>
  Color: Color
}

/// <summary>A radial point light (torch, lamp, explosion).</summary>
[<Struct>]
type PointLight2D = {
  /// <summary>World-space position of the light source.</summary>
  Position: Vector2

  /// <summary>Color of the light.</summary>
  Color: Color

  /// <summary>Brightness multiplier. 1.0 = full, 0.0 = off.</summary>
  Intensity: float32

  /// <summary>Maximum distance the light reaches in world units.</summary>
  Radius: float32

  /// <summary>Falloff curve exponent. 1.0 = linear, 2.0 = quadratic.</summary>
  Falloff: float32

  /// <summary>Whether this light should cast shadows.</summary>
  CastsShadows: bool
}

/// <summary>A directional light (sun, moon) with parallel rays.</summary>
[<Struct>]
type DirectionalLight2D = {
  /// <summary>Normalized direction the light travels IN (not toward). E.g. (0.3, -0.7) for sun shining down-right.</summary>
  Direction: Vector2

  /// <summary>Color of the light.</summary>
  Color: Color

  /// <summary>Brightness multiplier. 1.0 = full, 0.0 = off.</summary>
  Intensity: float32

  /// <summary>Whether this light should cast shadows.</summary>
  CastsShadows: bool
}

/// <summary>A shadow-casting occluder defined as a line segment.</summary>
[<Struct>]
type Occluder2D = {
  /// <summary>Start point of the occluder segment in world space.</summary>
  P1: Vector2

  /// <summary>End point of the occluder segment in world space.</summary>
  P2: Vector2
}
