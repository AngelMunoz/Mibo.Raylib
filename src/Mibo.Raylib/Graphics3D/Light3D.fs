namespace Mibo.Elmish.Graphics3D

open System.Numerics
open Raylib_cs

/// <summary>Ambient light configuration for 3D scenes.</summary>
[<Struct>]
type AmbientLight3D = {
  /// <summary>Base color of the ambient light.</summary>
  Color: Color
  /// <summary>Intensity multiplier for the ambient light.</summary>
  Intensity: float32
}

/// <summary>Directional light configuration for 3D scenes.</summary>
[<Struct>]
type DirectionalLight3D = {
  /// <summary>Direction the light shines (should be normalized).</summary>
  Direction: Vector3
  /// <summary>Color of the directional light.</summary>
  Color: Color
  /// <summary>Intensity multiplier.</summary>
  Intensity: float32
  /// <summary>Whether this directional light casts CSM shadows.</summary>
  CastsShadows: bool
}

/// <summary>Point light configuration for 3D scenes.</summary>
[<Struct>]
type PointLight3D = {
  /// <summary>World-space position of the light.</summary>
  Position: Vector3
  /// <summary>Color of the point light.</summary>
  Color: Color
  /// <summary>Maximum radius of influence.</summary>
  Radius: float32
}

/// <summary>Spot light configuration (reserved for future use).</summary>
[<Struct>]
type SpotLight3D = {
  /// <summary>World-space position of the light.</summary>
  Position: Vector3
  /// <summary>Direction the spot light points (should be normalized).</summary>
  Direction: Vector3
  /// <summary>Color of the spot light.</summary>
  Color: Color
  /// <summary>Intensity multiplier.</summary>
  Intensity: float32
  /// <summary>Inner cone angle in radians.</summary>
  InnerCutoff: float32
  /// <summary>Outer cone angle in radians.</summary>
  OuterCutoff: float32
}
