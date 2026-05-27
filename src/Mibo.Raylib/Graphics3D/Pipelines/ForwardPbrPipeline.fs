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
// EVSM Shadow Configuration
// ------------------------------------------------------------------

/// <summary>Configuration for Exponential Variance Shadow Maps in the forward pipeline.</summary>
[<Struct>]
type EvsmConfig = {
  /// <summary>Resolution of the shadow map (square). Default 2048.</summary>
  ShadowMapSize: int
  /// <summary>Positive exponential warp constant. Higher values tighten shadows. Default 40.0.</summary>
  PositiveExponent: float32
  /// <summary>Negative exponential warp constant. Default 5.0.</summary>
  NegativeExponent: float32
  /// <summary>Minimum shadow value to reduce light bleeding. Default 0.2.</summary>
  LightBleedReduction: float32
  /// <summary>Show shadow map debug overlay in bottom-right corner.</summary>
  ShowDebugOverlay: bool
  /// <summary>Maximum distance from camera for shadow rendering. Default 100.0.</summary>
  ShadowDistance: float32
}

module EvsmConfig =
  let defaults: EvsmConfig = {
    ShadowMapSize = 2048
    PositiveExponent = 5.0f
    NegativeExponent = 2.5f
    LightBleedReduction = 0.1f
    ShowDebugOverlay = true
    ShadowDistance = 100.0f
  }

// ------------------------------------------------------------------
// Internal Context
// ------------------------------------------------------------------

type private PipelineContext
  (
    forwardShader: Shader,
    materialCache: Dictionary<MaterialKey, Material>,
    maxPointLights: int,
    maxSpotLights: int
  ) =

  let mutable gameCtx = Unchecked.defaultof<GameContext>
  let mutable cameraActive = false
  let mutable currentCamera = Unchecked.defaultof<Camera3D>
  let mutable shaderActive = false

  let ambient = ResizeArray<AmbientLight3D>(1)
  let dirLights = ResizeArray<DirectionalLight3D>(1)
  let pointLights = ResizeArray<PointLight3D>(maxPointLights)
  let spotLights = ResizeArray<SpotLight3D>(maxSpotLights)
  let mutable lightsDirty = true

  let mutable activeShadowMap: RenderTexture2D = Unchecked.defaultof<RenderTexture2D>
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
  let mutable locLightViewProj = -1
  let mutable locPositiveExp = -1
  let mutable locNegativeExp = -1
  let mutable locLightBleedReduction = -1
  let mutable locCameraPos = -1

  let mutable activePositiveExp = 0.0f
  let mutable activeNegativeExp = 0.0f
  let mutable activeLightBleedReduction = 0.0f

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

      locLightViewProj <- Raylib.GetShaderLocation(forwardShader, "lightViewProj")
      locPositiveExp <- Raylib.GetShaderLocation(forwardShader, "positiveExp")
      locNegativeExp <- Raylib.GetShaderLocation(forwardShader, "negativeExp")
      locLightBleedReduction <- Raylib.GetShaderLocation(forwardShader, "lightBleedReduction")
      locCameraPos <- Raylib.GetShaderLocation(forwardShader, "cameraPos")
      locShadowMap <- Raylib.GetShaderLocation(forwardShader, "shadowMap")

      Raylib.BeginShaderMode(forwardShader)
      Raylib.SetShaderValue(
        forwardShader,
        locShadowMap,
        15,
        ShaderUniformDataType.Int
      )
      Raylib.EndShaderMode()

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

    let spCount = min spotLights.Count maxSpotLights

    Raylib.SetShaderValue(
      forwardShader,
      locSpotLightCount,
      spCount,
      ShaderUniformDataType.Int
    )

    for i = 0 to spCount - 1 do
      let s: SpotLight3D = spotLights[i]

      Raylib.SetShaderValue(
        forwardShader,
        locSpotLightPos[i],
        s.Position,
        ShaderUniformDataType.Vec3
      )

      Raylib.SetShaderValue(
        forwardShader,
        locSpotLightDir[i],
        s.Direction,
        ShaderUniformDataType.Vec3
      )

      Raylib.SetShaderValue(
        forwardShader,
        locSpotLightColor[i],
        colorToVec3 s.Color,
        ShaderUniformDataType.Vec3
      )

      Raylib.SetShaderValue(
        forwardShader,
        locSpotLightIntensity[i],
        s.Intensity,
        ShaderUniformDataType.Float
      )

      Raylib.SetShaderValue(
        forwardShader,
        locSpotLightRadius[i],
        s.Radius,
        ShaderUniformDataType.Float
      )

      Raylib.SetShaderValue(
        forwardShader,
        locSpotLightInnerCutoff[i],
        s.InnerCutoff,
        ShaderUniformDataType.Float
      )

      Raylib.SetShaderValue(
        forwardShader,
        locSpotLightOuterCutoff[i],
        s.OuterCutoff,
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

    if activeShadowMap.Texture.Id <> 0u then
      Rlgl.ActiveTextureSlot(15)
      Rlgl.EnableTexture(activeShadowMap.Texture.Id)
      Rlgl.ActiveTextureSlot(0)

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

    member _.AddSpotLight light =
      spotLights.Add(light)
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

  member internal _.Reset
    (
      gameContext: GameContext,
      shadowMap: RenderTexture2D,
      lightViewProj: Matrix4x4,
      posExp: float32,
      negExp: float32,
      lightBleedReduction: float32
    ) =
    gameCtx <- gameContext
    activeShadowMap <- shadowMap
    activeLightViewProj <- lightViewProj
    activePositiveExp <- posExp
    activeNegativeExp <- negExp
    activeLightBleedReduction <- lightBleedReduction
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
// EVSM Shadow Pass Helpers
// ------------------------------------------------------------------

module private EvsmPass =

  type MeshDraw = { Mesh: Mesh; Transform: Matrix4x4 }

  let collectMeshDraws(buffer: RenderBuffer3D) : MeshDraw[] =
    let draws = ResizeArray<MeshDraw>()

    for i = 0 to buffer.Count - 1 do
      match buffer[i] with
      | :? Command3D.DrawMeshCommand as cmd ->
        draws.Add({ Mesh = cmd.Mesh; Transform = cmd.Transform })
      | :? Command3D.DrawSkinnedMeshCommand as cmd ->
        draws.Add({ Mesh = cmd.Mesh; Transform = cmd.Transform })
      | :? Command3D.DrawModelCommand as cmd ->
        let m = cmd.Model
        for mi = 0 to m.MeshCount - 1 do
          let mesh = NativePtr.get m.Meshes mi
          draws.Add({ Mesh = mesh; Transform = cmd.Transform })
      | :? Command3D.DrawMeshInstancedCommand as cmd ->
        for ti = 0 to cmd.InstanceCount - 1 do
          draws.Add({ Mesh = cmd.Mesh; Transform = cmd.Transforms[ti] })
      | _ -> ()

    draws.ToArray()

  let renderShadowPass
    (evsmShader: Shader)
    (evsmMaterial: Material)
    (shadowFbo: RenderTexture2D)
    (lightDir: Vector3)
    (cameraPos: Vector3)
    (cameraTarget: Vector3)
    (cameraUp: Vector3)
    (fovY: float32)
    (aspect: float32)
    (positiveExp: float32)
    (negativeExp: float32)
    (shadowDistance: float32)
    (draws: MeshDraw[])
    : Matrix4x4 =

    let corners =
      EvsmMath.frustumCornersWorld cameraPos cameraTarget cameraUp fovY aspect 0.1f shadowDistance

    let center =
      corners
      |> Array.fold (fun acc c -> acc + c) Vector3.Zero
      |> fun sum -> sum / float32 corners.Length

    let lightPos = center - lightDir * 100.0f
    let safeUp = if abs lightDir.Y > 0.99f then Vector3.UnitZ else Vector3.UnitY
    let lightView = Raymath.MatrixLookAt(lightPos, center, safeUp)

    let mutable minX, minY, minZ =
      System.Single.MaxValue, System.Single.MaxValue, System.Single.MaxValue

    let mutable maxX, maxY, maxZ =
      System.Single.MinValue, System.Single.MinValue, System.Single.MinValue

    for corner in corners do
      let p = Raymath.Vector3Transform(corner, lightView)
      minX <- min minX p.X; maxX <- max maxX p.X
      minY <- min minY p.Y; maxY <- max maxY p.Y
      minZ <- min minZ p.Z; maxZ <- max maxZ p.Z

    let halfExtent = max (max (abs minX) (abs maxX)) (max (abs minY) (abs maxY))

    let mutable lightCamera = Camera3D()
    lightCamera.Position <- lightPos
    lightCamera.Target <- center
    lightCamera.Up <- safeUp
    lightCamera.FovY <- 2.0f * halfExtent
    lightCamera.Projection <- CameraProjection.Orthographic

    // ------------------------------------------------------------------
    // Render shadow pass using BeginMode3D (proper state setup)
    // Compute forward-pass matrix manually using same parameters.
    // Option B (proj * view) — tested as correct order.
    // ------------------------------------------------------------------
    let halfExtentD = float halfExtent
    let manualView = Raymath.MatrixLookAt(lightPos, center, safeUp)
    let manualProj = Raymath.MatrixOrtho(-halfExtentD, halfExtentD, -halfExtentD, halfExtentD, 0.01, 1000.0)
    let yFlip = Matrix4x4.CreateScale(1.0f, -1.0f, 1.0f)

    // Render shadow pass
    Raylib.BeginTextureMode(shadowFbo)
    Rlgl.ClearColor(0uy, 0uy, 0uy, 0uy)
    Rlgl.ClearScreenBuffers()

    Raylib.BeginMode3D(lightCamera)

    Rlgl.EnableDepthTest()
    Rlgl.DisableBackfaceCulling()
    Raylib.BeginShaderMode(evsmShader)
    Raylib.SetShaderValue(evsmShader, Raylib.GetShaderLocation(evsmShader, "positiveExp"), positiveExp, ShaderUniformDataType.Float)
    Raylib.SetShaderValue(evsmShader, Raylib.GetShaderLocation(evsmShader, "negativeExp"), negativeExp, ShaderUniformDataType.Float)

    for draw in draws do
      Raylib.DrawMesh(draw.Mesh, evsmMaterial, draw.Transform)

    Raylib.EndShaderMode()
    Rlgl.EnableBackfaceCulling()
    Raylib.EndMode3D()
    Raylib.EndTextureMode()

    // Build forward-pass matrix (proj * view * yFlip order)
    let combined = Matrix4x4.Multiply(manualProj, manualView)
    let lightViewProj = Matrix4x4.Multiply(combined, yFlip)

    lightViewProj

// ------------------------------------------------------------------
// ForwardPbrPipeline
// ------------------------------------------------------------------

/// <summary>
/// Reference implementation of <see cref="T:Mibo.Elmish.Graphics3D.IRenderPipeline3D"/>.
/// A Forward PBR pipeline with EVSM shadow mapping, PBR lighting,
/// material caching, and optional post-processing.
/// </summary>
/// <remarks>
/// This is the <b>reference implementation</b>, not the engine core.
/// It demonstrates how to build a complete forward renderer on top of
/// raylib's low-level primitives while keeping geometry universal.
///
/// Features:
/// <list type="bullet">
///   <item><description>EVSM shadow mapping for directional lights (single shadow map, exponential variance)</description></item>
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
    ?evsmConfig: EvsmConfig
  ) =

  let ppConfig = defaultArg postProcess PostProcessConfig3D.none
  let maxPt = defaultArg maxPointLights 8
  let maxSp = defaultArg maxSpotLights 4
  let evsmCfg = defaultArg evsmConfig EvsmConfig.defaults

  let mutable forwardShader: Shader = Unchecked.defaultof<Shader>
  let mutable evsmShader: Shader = Unchecked.defaultof<Shader>
  let mutable postProcessShader: Shader = Unchecked.defaultof<Shader>
  let materialCache = Dictionary<MaterialKey, Material>()
  let mutable evsmMaterial: Material = Unchecked.defaultof<Material>

  let mutable shadowMap: RenderTexture2D = Unchecked.defaultof<RenderTexture2D>

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
      forwardShader <-
        Shaders.loadForwardShader maxPt maxSp

      evsmShader <- Shaders.loadEvsmShadowShader()
      postProcessShader <- Shaders.loadPostProcessShader()

      evsmMaterial <- Raylib.LoadMaterialDefault()
      evsmMaterial.Shader <- evsmShader

      shadowMap <- EvsmMath.createShadowFbo evsmCfg.ShadowMapSize evsmCfg.ShadowMapSize

      context <- PipelineContext(forwardShader, materialCache, maxPt, maxSp)

    member _.Shutdown() =
      Raylib.UnloadShader(forwardShader)
      Raylib.UnloadShader(evsmShader)
      Raylib.UnloadShader(postProcessShader)

      Raylib.UnloadMaterial(evsmMaterial)

      for KeyValue(_, mat) in materialCache do
        Raylib.UnloadMaterial(mat)

      materialCache.Clear()

      if shadowMap.Id <> 0u then
        EvsmMath.destroyShadowFbo shadowMap

    member _.Execute gameCtx buffer rtPool =
      // ------------------------------------------------------------------
      // Pre-pass: collect camera, lights, and mesh draws for shadow mapping
      // ------------------------------------------------------------------
      let mutable activeCamera = Unchecked.defaultof<Camera3D>
      let mutable cameraFound = false

      let dirLights = ResizeArray<DirectionalLight3D>()

      for i = 0 to buffer.Count - 1 do
        match buffer[i] with
        | :? Command3D.BeginCameraCommand as cmd ->
          activeCamera <- cmd.Camera
          cameraFound <- true
        | :? Command3D.AddDirectionalLightCommand as cmd ->
          dirLights.Add(cmd.Light)
        | _ -> ()

      let meshDraws = EvsmPass.collectMeshDraws buffer

      // ------------------------------------------------------------------
      // Shadow pass
      // ------------------------------------------------------------------
      let mutable shadowLightViewProj = Matrix4x4.Identity

      if
        cameraFound
        && dirLights.Count > 0
        && dirLights[0].CastsShadows
        && meshDraws.Length > 0
      then
        let dir = dirLights[0]
        let fovY = activeCamera.FovY * MathF.PI / 180.0f
        let aspect = float32 gameCtx.WindowWidth / float32 gameCtx.WindowHeight

        shadowLightViewProj <-
          EvsmPass.renderShadowPass
            evsmShader
            evsmMaterial
            shadowMap
            dir.Direction
            activeCamera.Position
            activeCamera.Target
            activeCamera.Up
            fovY
            aspect
            evsmCfg.PositiveExponent
            evsmCfg.NegativeExponent
            evsmCfg.ShadowDistance
            meshDraws

      // ------------------------------------------------------------------
      // Forward pass — context is reset each frame, populated by commands
      // ------------------------------------------------------------------
      context.Reset(
        gameCtx,
        shadowMap,
        shadowLightViewProj,
        evsmCfg.PositiveExponent,
        evsmCfg.NegativeExponent,
        evsmCfg.LightBleedReduction
      )

      let ctx = context :> IRenderContext3D

      // Upload shadow uniforms if shadows were rendered
      if shadowLightViewProj <> Matrix4x4.Identity && cameraFound then
        Raylib.BeginShaderMode(forwardShader)

        Raylib.SetShaderValueMatrix(
          forwardShader,
          Raylib.GetShaderLocation(forwardShader, "lightViewProj"),
          shadowLightViewProj
        )

        Raylib.SetShaderValue(
          forwardShader,
          Raylib.GetShaderLocation(forwardShader, "positiveExp"),
          evsmCfg.PositiveExponent,
          ShaderUniformDataType.Float
        )

        Raylib.SetShaderValue(
          forwardShader,
          Raylib.GetShaderLocation(forwardShader, "negativeExp"),
          evsmCfg.NegativeExponent,
          ShaderUniformDataType.Float
        )

        Raylib.SetShaderValue(
          forwardShader,
          Raylib.GetShaderLocation(forwardShader, "lightBleedReduction"),
          evsmCfg.LightBleedReduction,
          ShaderUniformDataType.Float
        )

        Raylib.SetShaderValue(
          forwardShader,
          Raylib.GetShaderLocation(forwardShader, "cameraPos"),
          activeCamera.Position,
          ShaderUniformDataType.Vec3
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

      // DEBUG: Render shadow map as overlay in bottom-right corner
      if evsmCfg.ShowDebugOverlay then
        let sm = shadowMap
        let w = float32 gameCtx.WindowWidth
        let h = float32 gameCtx.WindowHeight
        let previewW = 256.0f
        let previewH = 256.0f
        let srcRect = Raylib_cs.Rectangle(0.0f, 0.0f, float32 evsmCfg.ShadowMapSize, float32 -evsmCfg.ShadowMapSize)
        let dstRect = Raylib_cs.Rectangle(w - previewW - 10.0f, h - previewH - 10.0f, previewW, previewH)
        Raylib.DrawTexturePro(sm.Texture, srcRect, dstRect, Vector2.Zero, 0.0f, Color.White)
        Raylib.DrawRectangleLines(int (w - previewW - 10.0f), int (h - previewH - 10.0f), int previewW, int previewH, Color.Red)
