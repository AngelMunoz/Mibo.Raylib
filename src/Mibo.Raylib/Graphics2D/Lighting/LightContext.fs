namespace Mibo.Elmish.Graphics2D.Lighting

open System
open System.Collections.Generic
open System.Numerics
open Raylib_cs

/// <summary>
/// User-owned mutable context that collects lighting state each frame and
/// uploads it to the GPU when lit sprites render. Shadows use analytic
/// Signed Distance Field (SDF) raymarching in the pixel shader.
/// </summary>
/// <remarks>
/// <para>
/// Create one in <c>init</c>, store it in your model, call <c>Reset()</c> at the start
/// of each frame's view, then accumulate lights via <c>LightCommands.setAmbient</c>,
/// <c>addDirectionalLight</c>, <c>addPointLight</c>, <c>addOccluder</c>,
/// and draw lit sprites via <c>LightCommands.litSprite</c>.
/// </para>
/// <para>
/// Occluder line segments are uploaded as a uniform array to the GPU each frame.
/// Shadow quality is controlled by the <c>softness</c> and <c>maxShadowDistance</c>
/// parameters. SDF sphere tracing in the pixel shader produces physically-plausible
/// soft shadows with configurable penumbra — no shadow atlas or extra render
/// passes required.
/// </para>
/// </remarks>
/// <param name="litShader">The lit-sprite shader. If not provided, loads the built-in shader.</param>
/// <param name="maxDirLights">Maximum directional lights the shader supports. Must match MAX_DIR_LIGHTS.</param>
/// <param name="maxPointLights">Maximum point lights the shader supports. Must match MAX_POINT_LIGHTS.</param>
/// <param name="maxOccluders">Maximum occluder segments uploaded per frame. Must match MAX_OCCLUDERS in the shader. Default 128.</param>
/// <param name="softness">Shadow penumbra softness. 0.0 = hard shadows, 0.05 = soft, 0.2 = very soft. Default 0.05.</param>
/// <param name="maxShadowDistance">Maximum raymarch distance for directional light shadows. Default 5000.0.</param>
type LightContext2D
  (
    ?litShader: Shader,
    ?maxDirLights: int,
    ?maxPointLights: int,
    ?maxOccluders: int,
    ?softness: float32,
    ?maxShadowDistance: float32
  ) =

  let maxDir = defaultArg maxDirLights 4
  let maxPoint = defaultArg maxPointLights 16
  let maxOccluders = defaultArg maxOccluders 128

  let litShader = defaultArg litShader (LitShader.load())
  let shadowSoftness = defaultArg softness 0.05f
  let shadowMaxDist = defaultArg maxShadowDistance 5000.0f

  let dirLights = ResizeArray<DirectionalLight2D>()
  let pointLights = ResizeArray<PointLight2D>()
  let occluders = ResizeArray<Occluder2D>()

  let mutable ambientColor = Color(0uy, 0uy, 0uy, 255uy)

  let colorToVec3(c: Color) =
    Vector3(float32 c.R / 255.0f, float32 c.G / 255.0f, float32 c.B / 255.0f)

  // Uniform locations (cached on first UploadUniforms call)
  let mutable locsCached = false

  let mutable locAmbientColor = -1
  let mutable locDirCount = -1
  let locDirDirs = Array.zeroCreate<int> maxDir
  let locDirColors = Array.zeroCreate<int> maxDir
  let locDirIntensities = Array.zeroCreate<int> maxDir
  let locDirShadowIdx = Array.zeroCreate<int> maxDir

  let mutable locPointCount = -1
  let locPointPos = Array.zeroCreate<int> maxPoint
  let locPointColors = Array.zeroCreate<int> maxPoint
  let locPointIntensities = Array.zeroCreate<int> maxPoint
  let locPointRadii = Array.zeroCreate<int> maxPoint
  let locPointFalloffs = Array.zeroCreate<int> maxPoint
  let locPointShadowIdx = Array.zeroCreate<int> maxPoint

  let locOccluders = Array.zeroCreate<int> maxOccluders
  let mutable locOccluderCount = -1
  let mutable locSoftness = -1
  let mutable locMaxDist = -1

  let cacheLocations() =
    if not locsCached then
      locAmbientColor <- Raylib.GetShaderLocation(litShader, "ambientColor")
      locDirCount <- Raylib.GetShaderLocation(litShader, "dirLightCount")

      for i = 0 to maxDir - 1 do
        locDirDirs[i] <-
          Raylib.GetShaderLocation(litShader, $"dirLightDirs[{i}]")

        locDirColors[i] <-
          Raylib.GetShaderLocation(litShader, $"dirLightColors[{i}]")

        locDirIntensities[i] <-
          Raylib.GetShaderLocation(litShader, $"dirLightIntensities[{i}]")

        locDirShadowIdx[i] <-
          Raylib.GetShaderLocation(litShader, $"dirLightShadowIdx[{i}]")

      locPointCount <- Raylib.GetShaderLocation(litShader, "pointLightCount")

      for i = 0 to maxPoint - 1 do
        locPointPos[i] <-
          Raylib.GetShaderLocation(litShader, $"pointLightPos[{i}]")

        locPointColors[i] <-
          Raylib.GetShaderLocation(litShader, $"pointLightColors[{i}]")

        locPointIntensities[i] <-
          Raylib.GetShaderLocation(litShader, $"pointLightIntensities[{i}]")

        locPointRadii[i] <-
          Raylib.GetShaderLocation(litShader, $"pointLightRadii[{i}]")

        locPointFalloffs[i] <-
          Raylib.GetShaderLocation(litShader, $"pointLightFalloffs[{i}]")

        locPointShadowIdx[i] <-
          Raylib.GetShaderLocation(litShader, $"pointLightShadowIdx[{i}]")

      for i = 0 to maxOccluders - 1 do
        locOccluders[i] <-
          Raylib.GetShaderLocation(litShader, $"occluders[{i}]")

      locOccluderCount <- Raylib.GetShaderLocation(litShader, "occluderCount")
      locSoftness <- Raylib.GetShaderLocation(litShader, "shadowSoftness")
      locMaxDist <- Raylib.GetShaderLocation(litShader, "shadowMaxDistance")

      locsCached <- true

  /// <summary>Whether the lit shader is currently active via BeginShaderMode. Managed by commands.</summary>
  member val ShaderActive = false with get, set

  /// <summary>Whether light uniforms need to be re-uploaded to the GPU.</summary>
  member val UniformsDirty = true with get, set

  /// <summary>Whether shadow raymarching is enabled for this context. Default true.</summary>
  member val ShadowsEnabled = true with get, set

  /// <summary>Clears accumulated lights, occluders, and resets ambient to black. Call at the start of each frame's view.</summary>
  member this.Reset() =
    dirLights.Clear()
    pointLights.Clear()
    occluders.Clear()
    ambientColor <- Color(0uy, 0uy, 0uy, 255uy)
    this.ShaderActive <- false
    this.UniformsDirty <- true
    this.ShadowsEnabled <- true

  /// <summary>Current ambient light color.</summary>
  member _.Ambient
    with get () = ambientColor
    and set (v) = ambientColor <- v

  /// <summary>Directional lights accumulated this frame.</summary>
  member _.DirLights = dirLights

  /// <summary>Point lights accumulated this frame.</summary>
  member _.PointLights = pointLights

  /// <summary>Occluder segments accumulated this frame.</summary>
  member _.Occluders = occluders

  /// <summary>The lit-sprite shader (built-in or user-supplied).</summary>
  member _.Shader = litShader

  /// <summary>
  /// Uploads all accumulated light data, occluder segments, and shadow
  /// parameters to the GPU. The lit shader must already be active
  /// (BeginShaderMode called).
  /// </summary>
  member this.UploadUniforms() =
    cacheLocations()

    Raylib.SetShaderValue(
      litShader,
      locAmbientColor,
      colorToVec3 ambientColor,
      ShaderUniformDataType.Vec3
    )

    let dirCount = min dirLights.Count maxDir

    Raylib.SetShaderValue(
      litShader,
      locDirCount,
      dirCount,
      ShaderUniformDataType.Int
    )

    for i = 0 to dirCount - 1 do
      let l = dirLights[i]

      Raylib.SetShaderValue(
        litShader,
        locDirDirs[i],
        l.Direction,
        ShaderUniformDataType.Vec2
      )

      Raylib.SetShaderValue(
        litShader,
        locDirColors[i],
        colorToVec3 l.Color,
        ShaderUniformDataType.Vec3
      )

      Raylib.SetShaderValue(
        litShader,
        locDirIntensities[i],
        l.Intensity,
        ShaderUniformDataType.Float
      )

      Raylib.SetShaderValue(
        litShader,
        locDirShadowIdx[i],
        (if l.CastsShadows then 0 else -1),
        ShaderUniformDataType.Int
      )

    let ptCount = min pointLights.Count maxPoint

    Raylib.SetShaderValue(
      litShader,
      locPointCount,
      ptCount,
      ShaderUniformDataType.Int
    )

    for i = 0 to ptCount - 1 do
      let l = pointLights[i]

      Raylib.SetShaderValue(
        litShader,
        locPointPos[i],
        l.Position,
        ShaderUniformDataType.Vec2
      )

      Raylib.SetShaderValue(
        litShader,
        locPointColors[i],
        colorToVec3 l.Color,
        ShaderUniformDataType.Vec3
      )

      Raylib.SetShaderValue(
        litShader,
        locPointIntensities[i],
        l.Intensity,
        ShaderUniformDataType.Float
      )

      Raylib.SetShaderValue(
        litShader,
        locPointRadii[i],
        l.Radius,
        ShaderUniformDataType.Float
      )

      Raylib.SetShaderValue(
        litShader,
        locPointFalloffs[i],
        l.Falloff,
        ShaderUniformDataType.Float
      )

      Raylib.SetShaderValue(
        litShader,
        locPointShadowIdx[i],
        (if l.CastsShadows then 0 else -1),
        ShaderUniformDataType.Int
      )

    // Occluder segments for SDF shadow raymarching
    let ocCount =
      if this.ShadowsEnabled then
        min occluders.Count maxOccluders
      else
        0

    Raylib.SetShaderValue(
      litShader,
      locOccluderCount,
      ocCount,
      ShaderUniformDataType.Int
    )

    for i = 0 to ocCount - 1 do
      let o = occluders[i]

      Raylib.SetShaderValue(
        litShader,
        locOccluders[i],
        Vector4(o.P1.X, o.P1.Y, o.P2.X, o.P2.Y),
        ShaderUniformDataType.Vec4
      )

    Raylib.SetShaderValue(
      litShader,
      locSoftness,
      shadowSoftness,
      ShaderUniformDataType.Float
    )

    Raylib.SetShaderValue(
      litShader,
      locMaxDist,
      shadowMaxDist,
      ShaderUniformDataType.Float
    )

  interface IDisposable with
    member _.Dispose() = Raylib.UnloadShader(litShader)
