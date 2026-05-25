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
// Shadow Configuration
// ------------------------------------------------------------------

/// <summary>Configuration for CSM shadow mapping in the forward pipeline.</summary>
[<Struct>]
type ShadowConfig = {
  /// <summary>Number of shadow cascades (1 = single shadow map, 3+ = CSM). Default 3.</summary>
  CascadeCount: int
  /// <summary>Resolution of each shadow map (square). Default 2048.</summary>
  ShadowMapSize: int
  /// <summary>Base shadow bias. Default 0.005.</summary>
  ShadowBias: float32
  /// <summary>Normal-based shadow bias multiplier. Default 0.02.</summary>
  NormalShadowBias: float32
  /// <summary>Camera near plane for cascade computation. Default 0.1.</summary>
  CameraNear: float32
  /// <summary>Camera far plane for cascade computation. Default 1000.0.</summary>
  CameraFar: float32
}

module ShadowConfig =
  let defaults: ShadowConfig = {
    CascadeCount = 3
    ShadowMapSize = 2048
    ShadowBias = 0.005f
    NormalShadowBias = 0.02f
    CameraNear = 0.1f
    CameraFar = 1000.0f
  }

// ------------------------------------------------------------------
// Internal Context
// ------------------------------------------------------------------

type private PipelineContext
  (
    forwardShader: Shader,
    materialCache: Dictionary<MaterialKey, Material>,
    maxPointLights: int
  ) =

  let mutable gameCtx = Unchecked.defaultof<GameContext>
  let mutable cameraActive = false
  let mutable currentCamera = Unchecked.defaultof<Camera3D>
  let mutable shaderActive = false

  let ambient = ResizeArray<AmbientLight3D>(1)
  let dirLights = ResizeArray<DirectionalLight3D>(1)
  let pointLights = ResizeArray<PointLight3D>(maxPointLights)
  let mutable lightsDirty = true

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
      Raylib.BeginShaderMode(forwardShader)
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
      Raylib.SetShaderValue(
        forwardShader,
        locAmbientColor,
        Vector3.Zero,
        ShaderUniformDataType.Vec3
      )

      Raylib.SetShaderValue(
        forwardShader,
        locAmbientIntensity,
        0.0f,
        ShaderUniformDataType.Float
      )
    | _ ->
      let a = ambient[0]

      Raylib.SetShaderValue(
        forwardShader,
        locAmbientColor,
        colorToVec3 a.Color,
        ShaderUniformDataType.Vec3
      )

      Raylib.SetShaderValue(
        forwardShader,
        locAmbientIntensity,
        a.Intensity,
        ShaderUniformDataType.Float
      )

    match dirLights.Count with
    | 0 ->
      Raylib.SetShaderValue(
        forwardShader,
        locDirLightDir,
        Vector3.Zero,
        ShaderUniformDataType.Vec3
      )

      Raylib.SetShaderValue(
        forwardShader,
        locDirLightColor,
        Vector3.Zero,
        ShaderUniformDataType.Vec3
      )

      Raylib.SetShaderValue(
        forwardShader,
        locDirLightIntensity,
        0.0f,
        ShaderUniformDataType.Float
      )

      Raylib.SetShaderValue(
        forwardShader,
        locDirLightCastsShadows,
        0,
        ShaderUniformDataType.Int
      )
    | _ ->
      let d = dirLights[0]

      Raylib.SetShaderValue(
        forwardShader,
        locDirLightDir,
        d.Direction,
        ShaderUniformDataType.Vec3
      )

      Raylib.SetShaderValue(
        forwardShader,
        locDirLightColor,
        colorToVec3 d.Color,
        ShaderUniformDataType.Vec3
      )

      Raylib.SetShaderValue(
        forwardShader,
        locDirLightIntensity,
        d.Intensity,
        ShaderUniformDataType.Float
      )

      Raylib.SetShaderValue(
        forwardShader,
        locDirLightCastsShadows,
        (if d.CastsShadows then 1 else 0),
        ShaderUniformDataType.Int
      )

    let ptCount = min pointLights.Count maxPointLights

    Raylib.SetShaderValue(
      forwardShader,
      locPointLightCount,
      ptCount,
      ShaderUniformDataType.Int
    )

    for i = 0 to ptCount - 1 do
      let l = pointLights[i]

      Raylib.SetShaderValue(
        forwardShader,
        locPointLightPos[i],
        l.Position,
        ShaderUniformDataType.Vec3
      )

      Raylib.SetShaderValue(
        forwardShader,
        locPointLightColor[i],
        colorToVec3 l.Color,
        ShaderUniformDataType.Vec3
      )

      Raylib.SetShaderValue(
        forwardShader,
        locPointLightRadius[i],
        l.Radius,
        ShaderUniformDataType.Float
      )

    lightsDirty <- false

  let getOrCreateMaterial(mat3d: Material3D) : Material =
    let key = MaterialKey.fromMaterial3D mat3d

    match materialCache.TryGetValue(key) with
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

    Raylib.SetShaderValue(
      forwardShader,
      locAlbedoColor,
      colorToVec4 mat3d.AlbedoColor,
      ShaderUniformDataType.Vec4
    )

    Raylib.SetShaderValue(
      forwardShader,
      locRoughness,
      mat3d.Roughness,
      ShaderUniformDataType.Float
    )

    Raylib.SetShaderValue(
      forwardShader,
      locMetallic,
      mat3d.Metallic,
      ShaderUniformDataType.Float
    )

    Raylib.SetShaderValue(
      forwardShader,
      locEmissionColor,
      colorToVec4 mat3d.EmissionColor,
      ShaderUniformDataType.Vec4
    )

    Raylib.SetShaderValue(
      forwardShader,
      locOpacity,
      mat3d.Opacity,
      ShaderUniformDataType.Float
    )

    Raylib.SetShaderValue(
      forwardShader,
      locTiling,
      mat3d.Tiling,
      ShaderUniformDataType.Vec2
    )

    let useNormal =
      match mat3d.NormalMap with
      | ValueSome _ -> 1
      | ValueNone -> 0

    Raylib.SetShaderValue(
      forwardShader,
      locUseNormalMap,
      useNormal,
      ShaderUniformDataType.Int
    )

  let drawMeshCore (mesh: Mesh) (transform: Matrix4x4) (material: Material3D) =
    if cameraActive then
      if lightsDirty then
        uploadLights()

      setMaterialUniforms(material)
      let mat = getOrCreateMaterial(material)
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

      Raylib.BeginMode3D(cam)
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

        setMaterialUniforms(material)
        let mat = getOrCreateMaterial(material)
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
      pointLights.Add(light)
      lightsDirty <- true

    member _.AddDirectionalLight light =
      dirLights.Add(light)
      lightsDirty <- true

    member _.SetAmbientLight light =
      ambient.Clear()
      ambient.Add(light)
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
          Raylib.BeginMode3D(currentCamera)
          cameraActive <- true

        if savedShader then
          Raylib.BeginShaderMode(forwardShader)
          shaderActive <- true

  member internal _.Reset(gameContext: GameContext) =
    gameCtx <- gameContext
    ambient.Clear()
    dirLights.Clear()
    pointLights.Clear()
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

module private ShadowPass =

  type MeshDraw = { Mesh: Mesh; Transform: Matrix4x4 }

  let collectMeshDraws(buffer: RenderBuffer3D) : MeshDraw[] =
    let draws = ResizeArray<MeshDraw>()

    for i = 0 to buffer.Count - 1 do
      match buffer[i] with
      | :? Command3D.DrawMeshCommand as cmd ->
        draws.Add(
          {
            Mesh = cmd.Mesh
            Transform = cmd.Transform
          }
        )
      | :? Command3D.DrawSkinnedMeshCommand as cmd ->
        draws.Add(
          {
            Mesh = cmd.Mesh
            Transform = cmd.Transform
          }
        )
      | :? Command3D.DrawModelCommand as cmd ->
        let m = cmd.Model

        for mi = 0 to m.MeshCount - 1 do
          let mesh = NativePtr.get m.Meshes mi

          draws.Add(
            {
              Mesh = mesh
              Transform = cmd.Transform
            }
          )
      | :? Command3D.DrawMeshInstancedCommand as cmd ->
        for ti = 0 to cmd.InstanceCount - 1 do
          draws.Add(
            {
              Mesh = cmd.Mesh
              Transform = cmd.Transforms[ti]
            }
          )
      | _ -> ()

    draws.ToArray()

  let renderShadowCascade
    (shadowShader: Shader)
    (shadowMaterial: Material)
    (shadowMap: RenderTexture2D)
    (shadowMapSize: int)
    (lightDir: Vector3)
    (near: float32)
    (far: float32)
    (cameraPos: Vector3)
    (cameraTarget: Vector3)
    (cameraUp: Vector3)
    (fovY: float32)
    (aspect: float32)
    (draws: MeshDraw[])
    : Matrix4x4 =

    let corners =
      CsmMath.frustumCornersWorld
        cameraPos
        cameraTarget
        cameraUp
        fovY
        aspect
        near
        far

    let shadowMatrix =
      CsmMath.computeShadowMatrix corners lightDir shadowMapSize

    Raylib.BeginTextureMode(shadowMap)
    Raylib.ClearBackground(Color.White)

    let lightMvpLoc = Raylib.GetShaderLocation(shadowShader, "lightMvp")

    Raylib.BeginShaderMode(shadowShader)

    for draw in draws do
      let mvp = shadowMatrix * draw.Transform
      Raylib.SetShaderValueMatrix(shadowShader, lightMvpLoc, mvp)
      Raylib.DrawMesh(draw.Mesh, shadowMaterial, Matrix4x4.Identity)

    Raylib.EndShaderMode()
    Raylib.EndTextureMode()

    shadowMatrix

// ------------------------------------------------------------------
// ClusteredForwardPipeline
// ------------------------------------------------------------------

/// <summary>
/// Reference implementation of <see cref="T:Mibo.Elmish.Graphics3D.IRenderPipeline3D"/>.
/// A Clustered Forward+ style pipeline with PBR lighting, CSM shadow mapping,
/// material caching, and optional post-processing.
/// </summary>
/// <remarks>
/// This is the <b>reference implementation</b>, not the engine core.
/// It demonstrates how to build a complete forward renderer on top of
/// raylib's low-level primitives while keeping geometry universal.
///
/// Features:
/// <list type="bullet">
///   <item><description>CSM shadow mapping for directional lights (configurable cascades)</description></item>
///   <item><description>Accumulated point and directional lights (configurable max point lights via uniform arrays, default 8)</description></item>
///   <item><description>Material caching: converts <see cref="T:Mibo.Elmish.Graphics3D.Material3D"/> to raylib <c>Material</c> on first use</description></item>
///   <item><description>Post-process via ping-pong render targets</description></item>
///   <item><description>CPU skinning fallback placeholder for <c>DrawSkinnedMesh</c></description></item>
/// </list>
/// </remarks>
type ClusteredForwardPipeline
  (
    ?postProcess: PostProcessConfig3D,
    ?maxPointLights: int,
    ?shadowConfig: ShadowConfig
  ) =

  let ppConfig = defaultArg postProcess PostProcessConfig3D.none
  let maxPt = defaultArg maxPointLights 8
  let shadowCfg = defaultArg shadowConfig ShadowConfig.defaults

  let mutable forwardShader: Shader = Unchecked.defaultof<Shader>
  let mutable shadowShader: Shader = Unchecked.defaultof<Shader>
  let mutable postProcessShader: Shader = Unchecked.defaultof<Shader>
  let materialCache = Dictionary<MaterialKey, Material>()
  let mutable shadowMaterial: Material = Unchecked.defaultof<Material>

  let mutable shadowMaps: RenderTexture2D[] = Array.empty
  let mutable cascadeSplits: float32[] = Array.empty

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
        Raylib.BeginTextureMode(target)
        Raylib.ClearBackground(Color.Black)
      | ValueNone -> ()

      Raylib.BeginShaderMode(pass.Shader)

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
      forwardShader <- Shaders.loadForwardShader maxPt shadowCfg.CascadeCount
      shadowShader <- Shaders.loadShadowShader()
      postProcessShader <- Shaders.loadPostProcessShader()

      shadowMaterial <- Raylib.LoadMaterialDefault()
      shadowMaterial.Shader <- shadowShader

      shadowMaps <-
        Array.init shadowCfg.CascadeCount (fun _ ->
          Raylib.LoadRenderTexture(
            shadowCfg.ShadowMapSize,
            shadowCfg.ShadowMapSize
          ))

      cascadeSplits <-
        if shadowCfg.CascadeCount > 1 then
          CsmMath.computeCascadeSplits
            shadowCfg.CameraNear
            shadowCfg.CameraFar
            shadowCfg.CascadeCount
        else
          Array.empty

      context <- PipelineContext(forwardShader, materialCache, maxPt)

    member _.Shutdown() =
      Raylib.UnloadShader(forwardShader)
      Raylib.UnloadShader(shadowShader)
      Raylib.UnloadShader(postProcessShader)

      Raylib.UnloadMaterial(shadowMaterial)

      for KeyValue(_, mat) in materialCache do
        Raylib.UnloadMaterial(mat)

      materialCache.Clear()

      for sm in shadowMaps do
        Raylib.UnloadRenderTexture(sm)

      shadowMaps <- Array.empty

    member _.Execute gameCtx buffer rtPool =
      // ------------------------------------------------------------------
      // Pre-pass: collect camera, lights, and mesh draws for shadow mapping
      // ------------------------------------------------------------------
      let mutable activeCamera = Unchecked.defaultof<Camera3D>
      let mutable cameraFound = false

      let dirLights = ResizeArray<DirectionalLight3D>()
      let ambient = ResizeArray<AmbientLight3D>()
      let pointLights = ResizeArray<PointLight3D>()

      for i = 0 to buffer.Count - 1 do
        match buffer[i] with
        | :? Command3D.BeginCameraCommand as cmd ->
          activeCamera <- cmd.Camera
          cameraFound <- true
        | :? Command3D.SetAmbientLightCommand as cmd -> ambient.Add(cmd.Light)
        | :? Command3D.AddDirectionalLightCommand as cmd ->
          dirLights.Add(cmd.Light)
        | :? Command3D.AddPointLightCommand as cmd -> pointLights.Add(cmd.Light)
        | _ -> ()

      let meshDraws = ShadowPass.collectMeshDraws buffer

      // ------------------------------------------------------------------
      // Shadow pass
      // ------------------------------------------------------------------
      let mutable shadowMatrices = Array.empty

      if
        cameraFound
        && dirLights.Count > 0
        && dirLights[0].CastsShadows
        && meshDraws.Length > 0
      then
        let dir = dirLights[0]
        let fovY = activeCamera.FovY * MathF.PI / 180.0f
        let aspect = float32 gameCtx.WindowWidth / float32 gameCtx.WindowHeight

        shadowMatrices <- Array.zeroCreate<Matrix4x4> shadowCfg.CascadeCount

        for i = 0 to shadowCfg.CascadeCount - 1 do
          let near =
            if i = 0 then shadowCfg.CameraNear else cascadeSplits[i - 1]

          let far =
            if i = shadowCfg.CascadeCount - 1 then
              shadowCfg.CameraFar
            else
              cascadeSplits[i]

          shadowMatrices[i] <-
            ShadowPass.renderShadowCascade
              shadowShader
              shadowMaterial
              shadowMaps[i]
              shadowCfg.ShadowMapSize
              dir.Direction
              near
              far
              activeCamera.Position
              activeCamera.Target
              activeCamera.Up
              fovY
              aspect
              meshDraws

      // ------------------------------------------------------------------
      // Forward pass — context is reset each frame, populated by commands
      // ------------------------------------------------------------------
      context.Reset(gameCtx)
      let ctx = context :> IRenderContext3D

      // Upload shadow uniforms if shadows were rendered
      if shadowMatrices.Length > 0 && cameraFound then
        let locCameraPos = Raylib.GetShaderLocation(forwardShader, "cameraPos")

        let locShadowBias =
          Raylib.GetShaderLocation(forwardShader, "shadowBias")

        let locNormalShadowBias =
          Raylib.GetShaderLocation(forwardShader, "normalShadowBias")

        let locShadowMatrices =
          Array.init shadowCfg.CascadeCount (fun i ->
            Raylib.GetShaderLocation(forwardShader, $"shadowMatrix{i}"))

        let locShadowMaps =
          Array.init shadowCfg.CascadeCount (fun i ->
            Raylib.GetShaderLocation(forwardShader, $"shadowMap{i}"))

        let locCascadeSplits =
          if cascadeSplits.Length > 0 then
            Raylib.GetShaderLocation(forwardShader, "cascadeSplits")
          else
            -1

        Raylib.BeginShaderMode(forwardShader)

        Raylib.SetShaderValue(
          forwardShader,
          locCameraPos,
          activeCamera.Position,
          ShaderUniformDataType.Vec3
        )

        Raylib.SetShaderValue(
          forwardShader,
          locShadowBias,
          shadowCfg.ShadowBias,
          ShaderUniformDataType.Float
        )

        Raylib.SetShaderValue(
          forwardShader,
          locNormalShadowBias,
          shadowCfg.NormalShadowBias,
          ShaderUniformDataType.Float
        )

        for i = 0 to shadowCfg.CascadeCount - 1 do
          Raylib.SetShaderValueMatrix(
            forwardShader,
            locShadowMatrices[i],
            shadowMatrices[i]
          )

          Raylib.SetShaderValueTexture(
            forwardShader,
            locShadowMaps[i],
            shadowMaps[i].Depth
          )

        if locCascadeSplits >= 0 then
          Raylib.SetShaderValueV(
            forwardShader,
            locCascadeSplits,
            cascadeSplits,
            ShaderUniformDataType.Float,
            cascadeSplits.Length
          )

        Raylib.EndShaderMode()

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
