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

      // Set shadowMap sampler to texture unit 15 — matches C example's
      // rlSetUniform(sc.mapLoc, &sc.slot, SHADER_UNIFORM_INT, 1)
      rlSetUniformInt locShadowMap 15

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

    // Bind shadow map — matches C example's BindShadowMap exactly:
    // rlActiveTextureSlot(sc.slot); rlEnableTexture(sc.target.depth.id);
    // rlSetUniform(sc.mapLoc, &sc.slot, SHADER_UNIFORM_INT, 1);
    if activeShadowMap.Depth.Id <> 0u then
      Rlgl.ActiveTextureSlot(15)
      Rlgl.EnableTexture(activeShadowMap.Depth.Id)
      rlSetUniformInt locShadowMap 15
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
// EVSM Shadow Pass Helpers
// ------------------------------------------------------------------

module private EvsmPass =

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

  let renderShadowPass
    (depthShadowShader: Shader)
    (depthShadowMaterial: Material)
    (shadowFbo: RenderTexture2D)
    (lightDir: Vector3)
    (cameraPos: Vector3)
    (cameraTarget: Vector3)
    (cameraUp: Vector3)
    (fovY: float32)
    (aspect: float32)
    (shadowDistance: float32)
    (draws: MeshDraw[])
    : Matrix4x4 =

    let corners =
      EvsmMath.frustumCornersWorld
        cameraPos
        cameraTarget
        cameraUp
        fovY
        aspect
        0.1f
        shadowDistance

    let center =
      corners
      |> Array.fold (fun acc c -> acc + c) Vector3.Zero
      |> fun sum -> sum / float32 corners.Length

    let lightPos = center - lightDir * 100.0f

    let safeUp =
      if abs lightDir.Y > 0.99f then
        Vector3.UnitZ
      else
        Vector3.UnitY

    let lightView = Raymath.MatrixLookAt(lightPos, center, safeUp)

    let mutable minX, minY, minZ =
      System.Single.MaxValue, System.Single.MaxValue, System.Single.MaxValue

    let mutable maxX, maxY, maxZ =
      System.Single.MinValue, System.Single.MinValue, System.Single.MinValue

    for corner in corners do
      let p = Raymath.Vector3Transform(corner, lightView)
      minX <- min minX p.X
      maxX <- max maxX p.X
      minY <- min minY p.Y
      maxY <- max maxY p.Y
      minZ <- min minZ p.Z
      maxZ <- max maxZ p.Z

    let halfExtent = max (max (abs minX) (abs maxX)) (max (abs minY) (abs maxY))

    let mutable lightCamera = Camera3D()
    lightCamera.Position <- lightPos
    lightCamera.Target <- center
    lightCamera.Up <- safeUp
    lightCamera.FovY <- 2.0f * halfExtent
    lightCamera.Projection <- CameraProjection.Orthographic

    // ------------------------------------------------------------------
    // Render shadow pass — matches C example exactly:
    // BeginTextureMode → ClearBackground(WHITE) → BeginMode3D
    // → capture VP via rlGetMatrixModelview/projection
    // → SetShaderValueMatrix → DrawScene → EndMode3D → EndTextureMode
    // ------------------------------------------------------------------
    Raylib.BeginTextureMode(shadowFbo)
    Raylib.ClearBackground(Color.White)

    Raylib.BeginMode3D(lightCamera)

    // Capture VP inside BeginMode3D, same as C example:
    // Matrix vp = MatrixMultiply(rlGetMatrixModelview(), rlGetMatrixProjection());
    // This is the EXACT matrix the batch uses for mvp — must match forward pass.
    let vp =
      Raymath.MatrixMultiply(
        Rlgl.GetMatrixModelview(),
        Rlgl.GetMatrixProjection()
      )

    for draw in draws do
      Raylib.DrawMesh(draw.Mesh, depthShadowMaterial, draw.Transform)

    Raylib.EndMode3D()
    Raylib.EndTextureMode()

    // Return the VP captured from rlgl — this is what the forward shader uses
    // for shadow comparison. Must be identical to what the shadow pass used.
    vp

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
      forwardShader <- Shaders.loadForwardShader maxPt maxSp atlasCfg.MaxCasters

      depthShadowShader <- Shaders.loadDepthShadowShader()
      postProcessShader <- Shaders.loadPostProcessShader()

      depthShadowMaterial <- Raylib.LoadMaterialDefault()
      depthShadowMaterial.Shader <- depthShadowShader

      shadowAtlas <- ShadowAtlas(atlasCfg, biasCfg)
      shadowAtlas.Initialize()

      context <- PipelineContext(forwardShader, materialCache, maxPt, maxSp)

    member _.Shutdown() =
      Raylib.UnloadShader(forwardShader)
      Raylib.UnloadShader(depthShadowShader)
      Raylib.UnloadShader(postProcessShader)

      Raylib.UnloadMaterial(depthShadowMaterial)

      for KeyValue(_, mat) in materialCache do
        Raylib.UnloadMaterial(mat)

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
          dirLights.Add(cmd.Light)
        | :? Command3D.AddPointLightCommand as cmd -> pointLights.Add(cmd.Light)
        | :? Command3D.AddSpotLightCommand as cmd -> spotLights.Add(cmd.Light)
        | _ -> ()

      let meshDraws = EvsmPass.collectMeshDraws buffer

      // ------------------------------------------------------------------
      // Shadow pass - render all shadow casters to atlas
      // ------------------------------------------------------------------
      let mutable hasShadowCasters = false

      // Clear previous frame's casters
      shadowAtlas.Clear()

      if cameraFound && meshDraws.Length > 0 then
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
          setShaderInt
            forwardShader
            (Raylib.GetShaderLocation(forwardShader, "shadowPass"))
            1

          // Render each caster to its atlas region
          for caster in shadowAtlas.Casters do
            if caster.Enabled then
              // Set viewport to caster's region
              shadowAtlas.GetRegionViewport(caster.AtlasRegion)

              // Create light camera for this caster
              let lightCamera =
                match caster.Type with
                | ShadowCasterType.Directional ->
                  // Light shines in caster.LightDirection, light comes FROM opposite direction
                  // Place camera in the direction light comes FROM, looking at the player
                  // NOTE: activeCamera.Target = player position (camera looks AT the player)
                  //       activeCamera.Position = camera position (orbits around player)
                  //       Shadow must follow the PLAYER, not the camera
                  let lightFromDir = Vector3.Normalize(-caster.LightDirection)
                  let shadowOrigin = activeCamera.Target
                  let lightPos = shadowOrigin + lightFromDir * 100.0f

                  let safeUp =
                    if abs caster.LightDirection.Y > 0.99f then
                      Vector3.UnitZ
                    else
                      Vector3.UnitY

                  Camera3D(
                    Position = lightPos,
                    Target = shadowOrigin,
                    Up = safeUp,
                    FovY = 50.0f,
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

              // Render shadow pass - matches F#C sample exactly:
              // BeginTextureMode → ClearBackground(WHITE) → BeginMode3D
              // → capture VP via rlGetMatrixModelview/projection
              // → SetShaderValueMatrix → DrawScene → EndMode3D → EndTextureMode
              Raylib.BeginTextureMode(shadowAtlas.Fbo)
              Raylib.ClearBackground(Color.White)

              Raylib.BeginMode3D(lightCamera)

              // Capture VP inside BeginMode3D, same as C example
              let vp =
                Raymath.MatrixMultiply(
                  Rlgl.GetMatrixModelview(),
                  Rlgl.GetMatrixProjection()
                )

              shadowAtlas.SetCasterViewProj(caster.Id, vp)

              // Draw scene from light's perspective
              for draw in meshDraws do
                Raylib.DrawMesh(draw.Mesh, depthShadowMaterial, draw.Transform)

              Raylib.EndMode3D()
              Raylib.EndTextureMode()

          // Reset viewport
          Rlgl.Viewport(0, 0, gameCtx.WindowWidth, gameCtx.WindowHeight)

          // Set shadowPass = 0 for the main pass
          setShaderInt
            forwardShader
            (Raylib.GetShaderLocation(forwardShader, "shadowPass"))
            0

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

          rlSetUniformInt
            (Raylib.GetShaderLocation(forwardShader, "shadowAtlas"))
            15

          Rlgl.ActiveTextureSlot(0)

        // Upload per-caster uniforms
        let locViewProjs =
          Raylib.GetShaderLocation(forwardShader, "shadowViewProjs")

        let locUVOffsets =
          Raylib.GetShaderLocation(forwardShader, "shadowUVOffsets")

        let locLightPositions =
          Raylib.GetShaderLocation(forwardShader, "shadowLightPositions")

        let locBiases = Raylib.GetShaderLocation(forwardShader, "shadowBiases")

        let locCasterTypes =
          Raylib.GetShaderLocation(forwardShader, "shadowTypes")

        let locCasterCount =
          Raylib.GetShaderLocation(forwardShader, "shadowCasterCount")

        // Upload arrays
        for i = 0 to min shadowAtlas.ActiveCasterCount atlasCfg.MaxCasters - 1 do
          let vpLocation =
            Raylib.GetShaderLocation(forwardShader, $"shadowViewProjs[{i}]")

          Raylib.SetShaderValueMatrix(
            forwardShader,
            vpLocation,
            shadowAtlas.ViewProjs[i]
          )

          let uvLocation =
            Raylib.GetShaderLocation(forwardShader, $"shadowUVOffsets[{i}]")

          setShaderVec4 forwardShader uvLocation shadowAtlas.UVOffsets[i]

          let posLocation =
            Raylib.GetShaderLocation(
              forwardShader,
              $"shadowLightPositions[{i}]"
            )

          setShaderVec3 forwardShader posLocation shadowAtlas.LightPositions[i]

          let biasLocation =
            Raylib.GetShaderLocation(forwardShader, $"shadowBiases[{i}]")

          setShaderFloat forwardShader biasLocation shadowAtlas.Biases[i]

          let typeLocation =
            Raylib.GetShaderLocation(forwardShader, $"shadowTypes[{i}]")

          setShaderInt forwardShader typeLocation shadowAtlas.CasterTypes[i]

        setShaderInt forwardShader locCasterCount shadowAtlas.ActiveCasterCount

        setShaderVec3
          forwardShader
          (Raylib.GetShaderLocation(forwardShader, "cameraPos"))
          activeCamera.Position

        setShaderInt
          forwardShader
          (Raylib.GetShaderLocation(forwardShader, "shadowPass"))
          0

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
      shadowAtlas.RenderDebugOverlay(gameCtx.WindowWidth, gameCtx.WindowHeight)
