namespace Mibo.Elmish.Graphics3D.Pipelines

open System.Numerics
open Raylib_cs
open Mibo.Elmish

/// <summary>A single post-processing pass applied to the rendered 3D scene.</summary>
[<Struct>]
type PostProcessPass3D = {

  /// <summary>Shader used for this pass. Receives the scene texture as <c>texture0</c>.</summary>
  Shader: Shader

  /// <summary>
  /// Optional callback to set shader uniforms before rendering the fullscreen quad.
  /// Called once per frame when this pass executes. The raylib <see cref="T:Raylib_cs.Shader"/>
  /// is already active via <c>BeginShaderMode</c> when this callback runs.
  /// </summary>
  OnSetup: (Shader -> GameContext -> unit) voption
}

/// <summary>Configuration for post-processing in a 3D pipeline.</summary>
[<Struct>]
type PostProcessConfig3D = {
  /// <summary>Post-processing passes applied in order after scene rendering.</summary>
  Passes: PostProcessPass3D[] voption
}

/// <summary>Convenience values for <see cref="T:Mibo.Elmish.Graphics3D.Pipelines.PostProcessConfig3D"/>.</summary>
module PostProcessConfig3D =

  /// <summary>No post-processing.</summary>
  let none: PostProcessConfig3D = { Passes = ValueNone }
