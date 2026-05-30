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
  /// <summary>
  /// Whether this directional light casts shadows.
  /// </summary>
  /// <remarks>
  /// <b>Pipeline-dependent:</b> Not all rendering pipelines support shadow casting.
  /// Pipelines that don't support shadows will ignore this field.
  /// Check your pipeline's documentation for shadow support details.
  /// </remarks>
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
  /// <summary>
  /// Whether this point light casts shadows.
  /// </summary>
  /// <remarks>
  /// <b>Pipeline-dependent:</b> Not all rendering pipelines support shadow casting.
  /// Pipelines that don't support shadows will ignore this field.
  /// Check your pipeline's documentation for shadow support details.
  /// </remarks>
  CastsShadows: bool
  /// <summary>
  /// Per-light shadow bias override. When None, uses the pipeline's global bias setting.
  /// </summary>
  /// <remarks>
  /// <b>Pipeline-dependent:</b> Only used by pipelines that support shadow casting.
  /// Adjust this value to fix shadow acne (too low) or peter-panning (too high).
  /// Typical range: 0.001 to 0.01.
  /// </remarks>
  ShadowBias: float32 voption
}

/// <summary>Spot light configuration for cone-shaped lights with distance attenuation.</summary>
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
  /// <summary>Maximum distance the light reaches.</summary>
  Radius: float32
  /// <summary>Cosine of the inner cone half-angle (full brightness).</summary>
  InnerCutoff: float32
  /// <summary>Cosine of the outer cone half-angle (fade to zero).</summary>
  OuterCutoff: float32
  /// <summary>
  /// Whether this spot light casts shadows.
  /// </summary>
  /// <remarks>
  /// <b>Pipeline-dependent:</b> Not all rendering pipelines support shadow casting.
  /// Pipelines that don't support shadows will ignore this field.
  /// Check your pipeline's documentation for shadow support details.
  /// </remarks>
  CastsShadows: bool
  /// <summary>
  /// Per-light shadow bias override. When None, uses the pipeline's global bias setting.
  /// </summary>
  /// <remarks>
  /// <b>Pipeline-dependent:</b> Only used by pipelines that support shadow casting.
  /// Adjust this value to fix shadow acne (too low) or peter-panning (too high).
  /// Typical range: 0.001 to 0.01.
  /// </remarks>
  ShadowBias: float32 voption
}
