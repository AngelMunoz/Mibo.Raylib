#nowarn "9"

namespace Mibo.Elmish.Graphics3D.Pipelines

open System
open System.Collections.Generic
open System.Numerics
open FSharp.NativeInterop
open Raylib_cs
open Mibo.Elmish
open Mibo.Elmish.Graphics3D

// ------------------------------------------------------------------
// NativePtr helpers — void* with DisableRuntimeMarshalling requires
// explicit fixed + NativePtr.toVoidPtr.
// ------------------------------------------------------------------
[<AutoOpen>]
module private NativeHelpers =

  let setShaderInt (shader: Shader) (loc: int) (value: int) =
    use p = fixed &value

    Raylib.SetShaderValue(
      shader,
      loc,
      NativePtr.toVoidPtr p,
      ShaderUniformDataType.Int
    )

  let setShaderFloat (shader: Shader) (loc: int) (value: float32) =
    use p = fixed &value

    Raylib.SetShaderValue(
      shader,
      loc,
      NativePtr.toVoidPtr p,
      ShaderUniformDataType.Float
    )

  let setShaderVec3 (shader: Shader) (loc: int) (v: Vector3) =
    use p = fixed &v

    Raylib.SetShaderValue(
      shader,
      loc,
      NativePtr.toVoidPtr p,
      ShaderUniformDataType.Vec3
    )

  let setShaderVec4 (shader: Shader) (loc: int) (v: Vector4) =
    use p = fixed &v

    Raylib.SetShaderValue(
      shader,
      loc,
      NativePtr.toVoidPtr p,
      ShaderUniformDataType.Vec4
    )

  let setShaderVec2 (shader: Shader) (loc: int) (v: Vector2) =
    use p = fixed &v

    Raylib.SetShaderValue(
      shader,
      loc,
      NativePtr.toVoidPtr p,
      ShaderUniformDataType.Vec2
    )

  let rlSetUniformInt (loc: int) (value: int) =
    use p = fixed &value

    Rlgl.SetUniform(
      loc,
      NativePtr.toVoidPtr p,
      int ShaderUniformDataType.Int,
      1
    )

// ------------------------------------------------------------------
// Material Cache Key
// ------------------------------------------------------------------

[<Struct>]
type private MaterialKey = {
  AlbedoMapId: uint
  RoughnessMapId: uint
  MetallicMapId: uint
  NormalMapId: uint
  EmissionMapId: uint
  AlbedoColor: Color
  Roughness: float32
  Metallic: float32
  EmissionColor: Color
  Opacity: float32
  TilingX: float32
  TilingY: float32
}

module private MaterialKey =
  let fromMaterial3D(mat: Material3D) : MaterialKey = {
    AlbedoMapId =
      match mat.AlbedoMap with
      | ValueSome t -> t.Id
      | ValueNone -> 0u
    RoughnessMapId =
      match mat.RoughnessMap with
      | ValueSome t -> t.Id
      | ValueNone -> 0u
    MetallicMapId =
      match mat.MetallicMap with
      | ValueSome t -> t.Id
      | ValueNone -> 0u
    NormalMapId =
      match mat.NormalMap with
      | ValueSome t -> t.Id
      | ValueNone -> 0u
    EmissionMapId =
      match mat.EmissionMap with
      | ValueSome t -> t.Id
      | ValueNone -> 0u
    AlbedoColor = mat.AlbedoColor
    Roughness = mat.Roughness
    Metallic = mat.Metallic
    EmissionColor = mat.EmissionColor
    Opacity = mat.Opacity
    TilingX = mat.Tiling.X
    TilingY = mat.Tiling.Y
  }

// ------------------------------------------------------------------
// Shadow Configuration (uses ShadowAtlas types from ShadowAtlas.fs)
// ------------------------------------------------------------------

// ------------------------------------------------------------------
// Internal Context
// ------------------------------------------------------------------

type private PipelineContext
  (
    forwardShader: Shader,
    materialCache: Dictionary<MaterialKey, Material>,
    maxPointLights: int,
    maxSpotLights: int,
    maxShadowCasters: int
  ) =

  let mutable gameCtx = Unchecked.defaultof<GameContext>
  let mutable cameraActive = false
  let mutable currentCamera = Unchecked.defaultof<Camera3D>
  let mutable shaderActive = false

  let ambient = ResizeArray<AmbientLight3D> 1
  let dirLights = ResizeArray<DirectionalLight3D> 1
  let pointLights = ResizeArray<PointLight3D> maxPointLights
  let spotLights = ResizeArray<SpotLight3D> maxSpotLights
  let mutable lightsDirty = true

  let mutable activeShadowMap: RenderTexture2D =
    Unchecked.defaultof<RenderTexture2D>

  let mutable activeLightViewProj = Matrix4x4.Identity

  let mutable locsCached = false
  let mutable locAlbedoColor = -1
  let mutable locRoughness = -1
  let mutable locMetallic = -1
  let mutable locEmissionColor = -1
  let mutable locOpacity = -1
  let mutable locTiling = -1
  let mutable locUseNormalMap = -1

  let mutable locAmbientColor = -1
  let mutable locAmbientIntensity = -1
  let mutable locDirLightDir = -1
  let mutable locDirLightColor = -1
  let mutable locDirLightIntensity = -1
  let mutable locDirLightCastsShadows = -1
  let mutable locPointLightCount = -1
  let locPointLightPos = Array.zeroCreate<int> maxPointLights
  let locPointLightColor = Array.zeroCreate<int> maxPointLights
  let locPointLightRadius = Array.zeroCreate<int> maxPointLights

  let mutable locSpotLightCount = -1
  let locSpotLightPos = Array.zeroCreate<int> maxSpotLights
  let locSpotLightDir = Array.zeroCreate<int> maxSpotLights
  let locSpotLightColor = Array.zeroCreate<int> maxSpotLights
  let locSpotLightIntensity = Array.zeroCreate<int> maxSpotLights
  let locSpotLightRadius = Array.zeroCreate<int> maxSpotLights
  let locSpotLightInnerCutoff = Array.zeroCreate<int> maxSpotLights
  let locSpotLightOuterCutoff = Array.zeroCreate<int> maxSpotLights

  let mutable locShadowMap = -1
  let mutable locCameraPos = -1

  let mutable locShadowPass = -1
  let mutable locShadowAtlas = -1
  let mutable locShadowCasterCount = -1
  let locShadowViewProjs = Array.zeroCreate<int> maxShadowCasters
  let locShadowUVOffsets = Array.zeroCreate<int> maxShadowCasters
  let locShadowLightPositions = Array.zeroCreate<int> maxShadowCasters
  let locShadowBiases = Array.zeroCreate<int> maxShadowCasters
  let locShadowTypes = Array.zeroCreate<int> maxShadowCasters

  let cacheLocations() =
    if not locsCached then
      locAlbedoColor <- Raylib.GetShaderLocation(forwardShader, "albedoColor")
      locRoughness <- Raylib.GetShaderLocation(forwardShader, "roughness")
      locMetallic <- Raylib.GetShaderLocation(forwardShader, "metallic")

      locEmissionColor <-
        Raylib.GetShaderLocation(forwardShader, "emissionColor")

      locOpacity <- Raylib.GetShaderLocation(forwardShader, "opacity")
      locTiling <- Raylib.GetShaderLocation(forwardShader, "tiling")
      locUseNormalMap <- Raylib.GetShaderLocation(forwardShader, "useNormalMap")

      locAmbientColor <- Raylib.GetShaderLocation(forwardShader, "ambientColor")

      locAmbientIntensity <-
        Raylib.GetShaderLocation(forwardShader, "ambientIntensity")

      locDirLightDir <- Raylib.GetShaderLocation(forwardShader, "dirLightDir")

      locDirLightColor <-
        Raylib.GetShaderLocation(forwardShader, "dirLightColor")

      locDirLightIntensity <-
        Raylib.GetShaderLocation(forwardShader, "dirLightIntensity")

      locDirLightCastsShadows <-
        Raylib.GetShaderLocation(forwardShader, "dirLightCastsShadows")

      locPointLightCount <-
        Raylib.GetShaderLocation(forwardShader, "pointLightCount")

      for i = 0 to maxPointLights - 1 do
        locPointLightPos[i] <-
          Raylib.GetShaderLocation(forwardShader, $"pointLightPos[{i}]")

        locPointLightColor[i] <-
          Raylib.GetShaderLocation(forwardShader, $"pointLightColor[{i}]")

        locPointLightRadius[i] <-
          Raylib.GetShaderLocation(forwardShader, $"pointLightRadius[{i}]")

      locSpotLightCount <-
        Raylib.GetShaderLocation(forwardShader, "spotLightCount")

      for i = 0 to maxSpotLights - 1 do
        locSpotLightPos[i] <-
          Raylib.GetShaderLocation(forwardShader, $"spotLightPos[{i}]")

        locSpotLightDir[i] <-
          Raylib.GetShaderLocation(forwardShader, $"spotLightDir[{i}]")

        locSpotLightColor[i] <-
          Raylib.GetShaderLocation(forwardShader, $"spotLightColor[{i}]")

        locSpotLightIntensity[i] <-
          Raylib.GetShaderLocation(forwardShader, $"spotLightIntensity[{i}]")

        locSpotLightRadius[i] <-
          Raylib.GetShaderLocation(forwardShader, $"spotLightRadius[{i}]")

        locSpotLightInnerCutoff[i] <-
          Raylib.GetShaderLocation(forwardShader, $"spotLightInnerCutoff[{i}]")

        locSpotLightOuterCutoff[i] <-
          Raylib.GetShaderLocation(forwardShader, $"spotLightOuterCutoff[{i}]")

      locCameraPos <- Raylib.GetShaderLocation(forwardShader, "cameraPos")
      locShadowMap <- Raylib.GetShaderLocation(forwardShader, "shadowMap")

      // Set shadowAtlas sampler to texture unit 15 to avoid material overrides
      locShadowAtlas <- Raylib.GetShaderLocation(forwardShader, "shadowAtlas")
      rlSetUniformInt locShadowAtlas 15

      locShadowPass <- Raylib.GetShaderLocation(forwardShader, "shadowPass")

      locShadowCasterCount <-
        Raylib.GetShaderLocation(forwardShader, "shadowCasterCount")

      for i = 0 to maxShadowCasters - 1 do
        locShadowViewProjs[i] <-
          Raylib.GetShaderLocation(forwardShader, $"shadowViewProjs[{i}]")

        locShadowUVOffsets[i] <-
          Raylib.GetShaderLocation(forwardShader, $"shadowUVOffsets[{i}]")

        locShadowLightPositions[i] <-
          Raylib.GetShaderLocation(forwardShader, $"shadowLightPositions[{i}]")

        locShadowBiases[i] <-
          Raylib.GetShaderLocation(forwardShader, $"shadowBiases[{i}]")

        locShadowTypes[i] <-
          Raylib.GetShaderLocation(forwardShader, $"shadowTypes[{i}]")

      locsCached <- true

  let colorToVec3(c: Color) =
    Vector3(float32 c.R / 255.0f, float32 c.G / 255.0f, float32 c.B / 255.0f)

  let colorToVec4(c: Color) =
    Vector4(
      float32 c.R / 255.0f,
      float32 c.G / 255.0f,
      float32 c.B / 255.0f,
      float32 c.A / 255.0f
    )

  let ensureShaderActive() =
    if not shaderActive then
      Raylib.BeginShaderMode forwardShader
      shaderActive <- true

  let ensureShaderInactive() =
    if shaderActive then
      Raylib.EndShaderMode()
      shaderActive <- false

  let uploadLights() =
    cacheLocations()
    ensureShaderActive()

    match ambient.Count with
    | 0 ->
      setShaderVec3 forwardShader locAmbientColor Vector3.Zero
      setShaderFloat forwardShader locAmbientIntensity 0.0f
    | _ ->
      let a = ambient[0]
      setShaderVec3 forwardShader locAmbientColor (colorToVec3 a.Color)
      setShaderFloat forwardShader locAmbientIntensity a.Intensity

    match dirLights.Count with
    | 0 ->
      setShaderVec3 forwardShader locDirLightDir Vector3.Zero
      setShaderVec3 forwardShader locDirLightColor Vector3.Zero
      setShaderFloat forwardShader locDirLightIntensity 0.0f
      setShaderInt forwardShader locDirLightCastsShadows 0
    | _ ->
      let d = dirLights[0]
      setShaderVec3 forwardShader locDirLightDir d.Direction
      setShaderVec3 forwardShader locDirLightColor (colorToVec3 d.Color)
      setShaderFloat forwardShader locDirLightIntensity d.Intensity

      setShaderInt
        forwardShader
        locDirLightCastsShadows
        (if d.CastsShadows then 1 else 0)

    let ptCount = min pointLights.Count maxPointLights
    setShaderInt forwardShader locPointLightCount ptCount

    for i = 0 to ptCount - 1 do
      let l = pointLights[i]
      setShaderVec3 forwardShader locPointLightPos[i] l.Position
      setShaderVec3 forwardShader locPointLightColor[i] (colorToVec3 l.Color)
      setShaderFloat forwardShader locPointLightRadius[i] l.Radius

    let spCount = min spotLights.Count maxSpotLights
    setShaderInt forwardShader locSpotLightCount spCount

    for i = 0 to spCount - 1 do
      let s: SpotLight3D = spotLights[i]
      setShaderVec3 forwardShader locSpotLightPos[i] s.Position
      setShaderVec3 forwardShader locSpotLightDir[i] s.Direction
      setShaderVec3 forwardShader locSpotLightColor[i] (colorToVec3 s.Color)
      setShaderFloat forwardShader locSpotLightIntensity[i] s.Intensity
      setShaderFloat forwardShader locSpotLightRadius[i] s.Radius
      setShaderFloat forwardShader locSpotLightInnerCutoff[i] s.InnerCutoff
      setShaderFloat forwardShader locSpotLightOuterCutoff[i] s.OuterCutoff

    lightsDirty <- false

  let getOrCreateMaterial(mat3d: Material3D) : Material =
    let key = MaterialKey.fromMaterial3D mat3d

    match materialCache.TryGetValue key with
    | true, mat -> mat
    | false, _ ->
      let mutable mat = Raylib.LoadMaterialDefault()
      mat.Shader <- forwardShader

      match mat3d.AlbedoMap with
      | ValueSome t ->
        Raylib.SetMaterialTexture(&mat, MaterialMapIndex.Albedo, t)
      | ValueNone -> ()

      match mat3d.RoughnessMap with
      | ValueSome t ->
        Raylib.SetMaterialTexture(&mat, MaterialMapIndex.Roughness, t)
      | ValueNone -> ()

      match mat3d.MetallicMap with
      | ValueSome t ->
        Raylib.SetMaterialTexture(&mat, MaterialMapIndex.Metalness, t)
      | ValueNone -> ()

      match mat3d.NormalMap with
      | ValueSome t ->
        Raylib.SetMaterialTexture(&mat, MaterialMapIndex.Normal, t)
      | ValueNone -> ()

      match mat3d.EmissionMap with
      | ValueSome t ->
        Raylib.SetMaterialTexture(&mat, MaterialMapIndex.Emission, t)
      | ValueNone -> ()

      materialCache[key] <- mat
      mat

  let setMaterialUniforms(mat3d: Material3D) =
    cacheLocations()
    ensureShaderActive()

    setShaderVec4 forwardShader locAlbedoColor (colorToVec4 mat3d.AlbedoColor)
    setShaderFloat forwardShader locRoughness mat3d.Roughness
    setShaderFloat forwardShader locMetallic mat3d.Metallic

    setShaderVec4
      forwardShader
      locEmissionColor
      (colorToVec4 mat3d.EmissionColor)

    setShaderFloat forwardShader locOpacity mat3d.Opacity
    setShaderVec2 forwardShader locTiling mat3d.Tiling

    let useNormal =
      match mat3d.NormalMap with
      | ValueSome _ -> 1
      | ValueNone -> 0

    setShaderInt forwardShader locUseNormalMap useNormal

  let drawMeshCore (mesh: Mesh) (transform: Matrix4x4) (material: Material3D) =
    if cameraActive then
      if lightsDirty then
        uploadLights()

      setMaterialUniforms material
      let mat = getOrCreateMaterial material
      Raylib.DrawMesh(mesh, mat, transform)

  // ------------------------------------------------------------------
  // IRenderContext3D Implementation
  // ------------------------------------------------------------------

  interface IRenderContext3D with
    member _.GameContext = gameCtx

    member _.BeginCamera cam =
      if cameraActive then
        ensureShaderInactive()
        Raylib.EndMode3D()

      Raylib.BeginMode3D cam
      cameraActive <- true
      currentCamera <- cam

    member _.EndCamera() =
      if cameraActive then
        ensureShaderInactive()
        Raylib.EndMode3D()
        cameraActive <- false

    member _.DrawMesh(mesh, transform, material) =
      drawMeshCore mesh transform material

    member _.DrawBillboard(texture, position, size, color) =
      if cameraActive then
        let transform =
          Matrix4x4.CreateBillboard(
            position,
            currentCamera.Position,
            Vector3.UnitY,
            Vector3.UnitY
          )

        let scaled = Matrix4x4.CreateScale(size.X, size.Y, 1.0f)
        let final = scaled * transform

        let mat = {
          Material3D.defaults with
              AlbedoColor = color
              AlbedoMap = ValueSome texture
        }

        drawMeshCore Primitive3D.plane final mat

    member _.DrawLine3D(start, finish, color) =
      if cameraActive then
        Raylib.DrawLine3D(start, finish, color)

    member _.DrawSkinnedMesh(mesh, transform, material, _boneMatrices) =
      drawMeshCore mesh transform material

    member _.DrawMeshInstanced(mesh, transforms, material, instanceCount) =
      if cameraActive then
        if lightsDirty then
          uploadLights()

        setMaterialUniforms material
        let mat = getOrCreateMaterial material
        Raylib.DrawMeshInstanced(mesh, mat, transforms, instanceCount)

    member _.DrawBillboardBatch(textures, positions, sizes, colors, count) =
      if cameraActive then
        for i = 0 to count - 1 do
          let transform =
            Matrix4x4.CreateBillboard(
              positions[i],
              currentCamera.Position,
              Vector3.UnitY,
              Vector3.UnitY
            )

          let scaled = Matrix4x4.CreateScale(sizes[i].X, sizes[i].Y, 1.0f)
          let final = scaled * transform

          let mat = {
            Material3D.defaults with
                AlbedoColor = colors[i]
                AlbedoMap = ValueSome textures[i]
          }

          drawMeshCore Primitive3D.plane final mat

    member _.AddPointLight light =
      pointLights.Add light
      lightsDirty <- true

    member _.AddDirectionalLight light =
      dirLights.Add light
      lightsDirty <- true

    member _.AddSpotLight light =
      spotLights.Add light
      lightsDirty <- true

    member _.SetAmbientLight light =
      ambient.Clear()
      ambient.Add light
      lightsDirty <- true

    member _.DrawImmediate action =
      let savedCam = cameraActive
      let savedShader = shaderActive

      if shaderActive then
        Raylib.EndShaderMode()
        shaderActive <- false

      if cameraActive then
        Raylib.EndMode3D()
        cameraActive <- false

      try
        action()
      finally
        if savedCam then
          Raylib.BeginMode3D currentCamera
          cameraActive <- true

        if savedShader then
          Raylib.BeginShaderMode forwardShader
          shaderActive <- true

  member internal _.WarmMaterial(mat3d: Material3D) =
    getOrCreateMaterial mat3d |> ignore

  member internal _.LocShadowPass = locShadowPass
  member internal _.LocShadowAtlas = locShadowAtlas
  member internal _.LocShadowCasterCount = locShadowCasterCount
  member internal _.LocShadowViewProjs = locShadowViewProjs
  member internal _.LocShadowUVOffsets = locShadowUVOffsets
  member internal _.LocShadowLightPositions = locShadowLightPositions
  member internal _.LocShadowBiases = locShadowBiases
  member internal _.LocShadowTypes = locShadowTypes
  member internal _.LocCameraPos = locCameraPos

  member internal _.Reset
    (
      gameContext: GameContext,
      shadowFbo: RenderTexture2D,
      lightViewProj: Matrix4x4
    ) =
    gameCtx <- gameContext
    activeShadowMap <- shadowFbo
    activeLightViewProj <- lightViewProj
    ambient.Clear()
    dirLights.Clear()
    pointLights.Clear()
    spotLights.Clear()
    lightsDirty <- true
    cameraActive <- false
    shaderActive <- false

  member internal _.EndAll() =
    if shaderActive then
      Raylib.EndShaderMode()
      shaderActive <- false

    if cameraActive then
      Raylib.EndMode3D()
      cameraActive <- false

// ------------------------------------------------------------------
// Shadow Pass Helpers
// ------------------------------------------------------------------

module private ShadowPassHelpers =

  type MeshDraw = { Mesh: Mesh; Transform: Matrix4x4 }

  let collectMeshDraws(buffer: RenderBuffer3D) =
    let pool = System.Buffers.ArrayPool<MeshDraw>.Shared
    let arr = pool.Rent(buffer.Count)
    let mutable count = 0

    for i = 0 to buffer.Count - 1 do
      match buffer[i] with
      | :? Command3D.DrawMeshCommand as cmd ->
        arr[count] <- {
          Mesh = cmd.Mesh
          Transform = cmd.Transform
        }

        count <- count + 1
      | :? Command3D.DrawSkinnedMeshCommand as cmd ->
        arr[count] <- {
          Mesh = cmd.Mesh
          Transform = cmd.Transform
        }

        count <- count + 1
      | :? Command3D.DrawModelCommand as cmd ->
        let m = cmd.Model

        for mi = 0 to m.MeshCount - 1 do
          let mesh = NativePtr.get m.Meshes mi

          arr[count] <- {
            Mesh = mesh
            Transform = cmd.Transform
          }

          count <- count + 1
      | :? Command3D.DrawMeshInstancedCommand as cmd ->
        for ti = 0 to cmd.InstanceCount - 1 do
          arr[count] <- {
            Mesh = cmd.Mesh
            Transform = cmd.Transforms[ti]
          }

          count <- count + 1
      | _ -> ()

    struct (arr, count)

// ------------------------------------------------------------------
// ForwardPbrPipeline
// ------------------------------------------------------------------

/// <summary>
/// Reference implementation of <see cref="T:Mibo.Elmish.Graphics3D.IRenderPipeline3D"/>.
/// A Forward PBR pipeline with shadow atlas mapping, PBR lighting,
/// material caching, and optional post-processing.
/// </summary>
/// <remarks>
/// This is the <b>reference implementation</b>, not the engine core.
/// It demonstrates how to build a complete forward renderer on top of
/// raylib's low-level primitives while keeping geometry universal.
///
/// Features:
/// <list type="bullet">
///   <item><description>Shadow atlas mapping for multiple shadow-casting lights (directional, point, spot)</description></item>
///   <item><description>Accumulated point and directional lights (configurable max point lights via uniform arrays, default 8)</description></item>
///   <item><description>Material caching: converts <see cref="T:Mibo.Elmish.Graphics3D.Material3D"/> to raylib <c>Material</c> on first use</description></item>
///   <item><description>Post-process via ping-pong render targets</description></item>
///   <item><description>CPU skinning fallback placeholder for <c>DrawSkinnedMesh</c></description></item>
/// </list>
/// </remarks>
type ForwardPbrPipeline
  (
    ?postProcess: PostProcessConfig3D,
    ?maxPointLights: int,
    ?maxSpotLights: int,
    ?shadowAtlasConfig: ShadowAtlasConfig,
    ?shadowBiasConfig: ShadowBiasConfig
  ) =

  let ppConfig = defaultArg postProcess PostProcessConfig3D.none
  let maxPt = defaultArg maxPointLights 8
  let maxSp = defaultArg maxSpotLights 4
  let atlasCfg = defaultArg shadowAtlasConfig ShadowAtlasConfig.defaults
  let biasCfg = defaultArg shadowBiasConfig ShadowBiasConfig.defaults

  let mutable forwardShader: Shader = Unchecked.defaultof<Shader>
  let mutable depthShadowShader: Shader = Unchecked.defaultof<Shader>
  let mutable postProcessShader: Shader = Unchecked.defaultof<Shader>
  let materialCache = Dictionary<MaterialKey, Material>()
  let mutable depthShadowMaterial: Material = Unchecked.defaultof<Material>

  let mutable shadowAtlas: ShadowAtlas = Unchecked.defaultof<ShadowAtlas>

  let mutable context: PipelineContext = Unchecked.defaultof<PipelineContext>

  let ppPasses: PostProcessPass3D[] =
    match ppConfig.Passes with
    | ValueSome passes -> passes
    | ValueNone -> Array.empty

  let applyPostProcess
    (ctx: GameContext)
    (sceneTarget: RenderTexture2D)
    (rtPool: IRenderTargetPool3D)
    =
    let mutable src = sceneTarget
    let w = ctx.WindowWidth
    let h = ctx.WindowHeight

    for i = 0 to ppPasses.Length - 1 do
      let pass = ppPasses[i]
      let isLast = i = ppPasses.Length - 1

      let dst: RenderTexture2D voption =
        if isLast then
          ValueNone
        else
          ValueSome(rtPool.Acquire(w, h))

      match dst with
      | ValueSome target ->
        Raylib.BeginTextureMode target
        Raylib.ClearBackground Color.Black
      | ValueNone -> ()

      Raylib.BeginShaderMode pass.Shader

      match pass.OnSetup with
      | ValueSome f -> f pass.Shader ctx
      | ValueNone -> ()

      let sourceRect = Raylib_cs.Rectangle(0.0f, 0.0f, float32 w, float32 -h)
      let destRect = Raylib_cs.Rectangle(0.0f, 0.0f, float32 w, float32 h)

      Raylib.DrawTexturePro(
        src.Texture,
        sourceRect,
        destRect,
        Vector2.Zero,
        0.0f,
        Color.White
      )

      Raylib.EndShaderMode()

      match dst with
      | ValueSome target ->
        Raylib.EndTextureMode()
        src <- target
      | ValueNone -> ()

  // ------------------------------------------------------------------
  // IRenderPipeline3D
  // ------------------------------------------------------------------

  interface IRenderPipeline3D with
    member _.Initialize() =
      forwardShader <- Shaders.loadForwardShader maxPt maxSp atlasCfg.MaxCasters

      depthShadowShader <- Shaders.loadDepthShadowShader()
      postProcessShader <- Shaders.loadPostProcessShader()

      depthShadowMaterial <- Raylib.LoadMaterialDefault()
      depthShadowMaterial.Shader <- depthShadowShader

      shadowAtlas <- ShadowAtlas(atlasCfg, biasCfg)
      shadowAtlas.Initialize()

      context <-
        PipelineContext(
          forwardShader,
          materialCache,
          maxPt,
          maxSp,
          atlasCfg.MaxCasters
        )

    member _.Shutdown() =
      Raylib.UnloadShader forwardShader
      Raylib.UnloadShader depthShadowShader
      Raylib.UnloadShader postProcessShader

      Raylib.UnloadMaterial depthShadowMaterial

      for KeyValue(_, mat) in materialCache do
        Raylib.UnloadMaterial mat

      materialCache.Clear()

      if shadowAtlas <> Unchecked.defaultof<ShadowAtlas> then
        shadowAtlas.Shutdown()

    member _.Execute gameCtx buffer rtPool =
      // ------------------------------------------------------------------
      // Pre-pass: collect camera, lights, and mesh draws for shadow mapping
      // ------------------------------------------------------------------
      let mutable activeCamera = Unchecked.defaultof<Camera3D>
      let mutable cameraFound = false

      let dirLights = ResizeArray<DirectionalLight3D>()
      let pointLights = ResizeArray<PointLight3D>()
      let spotLights = ResizeArray<SpotLight3D>()

      for i = 0 to buffer.Count - 1 do
        match buffer[i] with
        | :? Command3D.BeginCameraCommand as cmd ->
          activeCamera <- cmd.Camera
          cameraFound <- true
        | :? Command3D.AddDirectionalLightCommand as cmd ->
          dirLights.Add cmd.Light
        | :? Command3D.AddPointLightCommand as cmd -> pointLights.Add cmd.Light
        | :? Command3D.AddSpotLightCommand as cmd -> spotLights.Add cmd.Light
        | :? Command3D.DrawMeshCommand as cmd ->
          context.WarmMaterial(cmd.Material)
        | :? Command3D.DrawSkinnedMeshCommand as cmd ->
          context.WarmMaterial(cmd.Material)
        | :? Command3D.DrawMeshInstancedCommand as cmd ->
          context.WarmMaterial(cmd.Material)
        | :? Command3D.DrawModelCommand as cmd ->
          let m = cmd.Model

          for mi = 0 to m.MeshCount - 1 do
            let matIdx = NativePtr.get m.MeshMaterial mi
            let raylibMat = NativePtr.get m.Materials matIdx
            let mat3d = Material3D.fromRaylibMaterial raylibMat
            context.WarmMaterial(mat3d)
        | _ -> ()

      let struct (meshDraws, meshDrawCount) =
        ShadowPassHelpers.collectMeshDraws buffer

      let mutable hasShadowCasters = false

      try
        // ------------------------------------------------------------------
        // Shadow pass - render all shadow casters to atlas
        // ------------------------------------------------------------------

        // Clear previous frame's casters
        shadowAtlas.Clear()

        if cameraFound && meshDrawCount > 0 then
          // Register shadow casters
          for dir in dirLights do
            if dir.CastsShadows then
              hasShadowCasters <- true

              // Register caster with atlas
              shadowAtlas.AddCaster(
                ShadowCasterType.Directional,
                Vector3.Zero,
                dir.Direction,
                Vector3.Zero,
                true,
                ValueNone
              )
              |> ignore

          // TODO: Register point light casters
          // TODO: Register spot light casters

          // Render shadow passes
          if shadowAtlas.Count > 0 then
            // Set shadowPass = 1 for the shadow pass shader
            setShaderInt forwardShader context.LocShadowPass 1

            // Bind atlas FBO once for all casters. Viewport clips rasterization
            // to each caster's region, so they never interfere.
            Raylib.BeginTextureMode(shadowAtlas.Fbo)
            Raylib.ClearBackground(Color.White)

            // Render each caster to its atlas region
            for caster in shadowAtlas.Casters do
              if caster.Enabled then

                // Create light camera for this caster
                let lightCamera =
                  match caster.Type with
                  | ShadowCasterType.Directional ->
                    let lightFromDir = Vector3.Normalize(-caster.LightDirection)

                    // Compute shadow origin based on configured strategy
                    let rawOrigin =
                      match atlasCfg.OriginStrategy with
                      | ShadowOriginStrategy.CameraTarget -> activeCamera.Target
                      | ShadowOriginStrategy.SceneCenter -> Vector3.Zero
                      | ShadowOriginStrategy.Custom f -> f activeCamera

                    // Grid snapping: quantize shadow origin to eliminate per-frame flickering.
                    // Shadow map only re-renders when player crosses a grid boundary.
                    let gridSize = atlasCfg.GridSnapSize

                    let snappedX =
                      if gridSize > 0.0f then
                        MathF.Round(rawOrigin.X / gridSize) * gridSize
                      else
                        rawOrigin.X

                    let snappedZ =
                      if gridSize > 0.0f then
                        MathF.Round(rawOrigin.Z / gridSize) * gridSize
                      else
                        rawOrigin.Z

                    let shadowOrigin = Vector3(snappedX, rawOrigin.Y, snappedZ)

                    // Derive light distance from config or use default
                    let lightDistance =
                      match atlasCfg.DirectionalLightDistance with
                      | ValueSome d -> d
                      | ValueNone -> 100.0f // Default: 100 units behind origin

                    let lightPos = shadowOrigin + lightFromDir * lightDistance

                    let safeUp =
                      if abs caster.LightDirection.Y > 0.99f then
                        Vector3.UnitZ
                      else
                        Vector3.UnitY

                    // Derive ortho size from config or use default
                    let orthoSize =
                      match atlasCfg.DirectionalLightSize with
                      | ValueSome s -> s
                      | ValueNone -> 50.0f // Default: 50 unit coverage

                    Camera3D(
                      Position = lightPos,
                      Target = shadowOrigin,
                      Up = safeUp,
                      FovY = orthoSize,
                      Projection = CameraProjection.Orthographic
                    )
                  | _ ->
                    Camera3D(
                      Position = caster.LightPosition,
                      Target = caster.LightPosition + caster.LightDirection,
                      Up = Vector3.UnitY,
                      FovY = 90.0f,
                      Projection = CameraProjection.Perspective
                    )

                // Set viewport to this caster's atlas region, then BeginMode3D
                // uses it for orthographic projection.
                shadowAtlas.GetRegionViewport(caster.AtlasRegion)

                Raylib.BeginMode3D(lightCamera)

                // Capture VP inside BeginMode3D, same as C example
                let vp =
                  Raymath.MatrixMultiply(
                    Rlgl.GetMatrixModelview(),
                    Rlgl.GetMatrixProjection()
                  )

                shadowAtlas.SetCasterViewProj(caster.Id, vp)

                // Draw scene from light's perspective
                for i = 0 to meshDrawCount - 1 do
                  let draw = meshDraws[i]

                  Raylib.DrawMesh(
                    draw.Mesh,
                    depthShadowMaterial,
                    draw.Transform
                  )

                Raylib.EndMode3D()

            // Reset viewport to window size
            Rlgl.Viewport(0, 0, gameCtx.WindowWidth, gameCtx.WindowHeight)
            Raylib.EndTextureMode()

            // Set shadowPass = 0 for the main pass
            setShaderInt forwardShader context.LocShadowPass 0
      finally
        System.Buffers.ArrayPool<ShadowPassHelpers.MeshDraw>.Shared
          .Return(meshDraws, false)

      // ------------------------------------------------------------------
      // Forward pass — context is reset each frame, populated by commands
      // ------------------------------------------------------------------
      context.Reset(gameCtx, shadowAtlas.Fbo, Matrix4x4.Identity)

      let ctx = context :> IRenderContext3D

      // Upload shadow atlas uniforms
      if hasShadowCasters && cameraFound then
        shadowAtlas.PrepareUniforms()

        // Upload atlas texture
        if shadowAtlas.Fbo.Depth.Id <> 0u then
          Rlgl.EnableShader(forwardShader.Id)
          Rlgl.ActiveTextureSlot(15)
          Rlgl.EnableTexture(shadowAtlas.Fbo.Depth.Id)
          rlSetUniformInt context.LocShadowAtlas 15
          Rlgl.ActiveTextureSlot(0)

        // Upload per-caster uniforms
        let count = min shadowAtlas.ActiveCasterCount atlasCfg.MaxCasters

        for i = 0 to count - 1 do
          Raylib.SetShaderValueMatrix(
            forwardShader,
            context.LocShadowViewProjs[i],
            shadowAtlas.ViewProjs[i]
          )

          setShaderVec4
            forwardShader
            context.LocShadowUVOffsets[i]
            shadowAtlas.UVOffsets[i]

          setShaderVec3
            forwardShader
            context.LocShadowLightPositions[i]
            shadowAtlas.LightPositions[i]

          setShaderFloat
            forwardShader
            context.LocShadowBiases[i]
            shadowAtlas.Biases[i]

          setShaderInt
            forwardShader
            context.LocShadowTypes[i]
            shadowAtlas.CasterTypes[i]

        setShaderInt
          forwardShader
          context.LocShadowCasterCount
          shadowAtlas.ActiveCasterCount

        setShaderVec3 forwardShader context.LocCameraPos activeCamera.Position
        setShaderInt forwardShader context.LocShadowPass 0

      // Render
      match ppConfig.Passes with
      | ValueNone
      | ValueSome [||] ->
        for i = 0 to buffer.Count - 1 do
          buffer[i].Render(ctx)

        context.EndAll()
      | _ ->
        let sceneRT = rtPool.Acquire(gameCtx.WindowWidth, gameCtx.WindowHeight)
        Raylib.BeginTextureMode(sceneRT)
        Raylib.ClearBackground(Color.Black)

        for i = 0 to buffer.Count - 1 do
          buffer[i].Render(ctx)

        context.EndAll()
        Raylib.EndTextureMode()
        applyPostProcess gameCtx sceneRT rtPool

      // DEBUG: Render shadow atlas as overlay in bottom-right corner
      if atlasCfg.ShowDebugOverlay then
        shadowAtlas.RenderDebugOverlay(
          gameCtx.WindowWidth,
          gameCtx.WindowHeight
        )
