namespace Mibo.Elmish.Graphics3D.Pipelines

open System.Numerics
open Raylib_cs

/// <summary>
/// Helper functions for Cascaded Shadow Map (CSM) math.
/// </summary>
module CsmMath =

  /// <summary>
  /// Computes the 8 corners of a perspective frustum in world space
  /// using direct trigonometry, avoiding projection matrix convention mismatches.
  /// </summary>
  let frustumCornersWorld
    (cameraPos: Vector3)
    (cameraTarget: Vector3)
    (cameraUp: Vector3)
    (fovY: float32)
    (aspect: float32)
    (near: float32)
    (far: float32)
    : Vector3[] =

    let forward = Vector3.Normalize(cameraTarget - cameraPos)
    let right = Vector3.Normalize(Vector3.Cross(forward, cameraUp))

    // Re-orthogonalize up against right to handle non-orthogonal cameraUp
    let up = Vector3.Cross(right, forward)

    let halfV = tan(fovY * 0.5f)
    let halfH = halfV * aspect

    let nearCenter = cameraPos + forward * near
    let farCenter = cameraPos + forward * far

    let nw = halfH * near
    let nh = halfV * near
    let fw = halfH * far
    let fh = halfV * far

    // Near plane corners
    let nbl = nearCenter - right * nw - up * nh
    let nbr = nearCenter + right * nw - up * nh
    let ntl = nearCenter - right * nw + up * nh
    let ntr = nearCenter + right * nw + up * nh

    // Far plane corners
    let fbl = farCenter - right * fw - up * fh
    let fbr = farCenter + right * fw - up * fh
    let ftl = farCenter - right * fw + up * fh
    let ftr = farCenter + right * fw + up * fh

    [| nbl; nbr; ntl; ntr; fbl; fbr; ftl; ftr |]

  /// <summary>
  /// Computes an orthographic shadow view-projection matrix for a directional light.
  /// Uses raylib Raymath for all matrix operations to avoid System.Numerics layout issues.
  /// </summary>
  let computeShadowMatrix
    (frustumCorners: Vector3[])
    (lightDir: Vector3)
    (shadowMapSize: int)
    : Matrix4x4 =

    let center =
      frustumCorners
      |> Array.fold (fun acc c -> acc + c) Vector3.Zero
      |> fun sum -> sum / float32 frustumCorners.Length

    let lightPos = center - lightDir * 100.0f
    let lightView = Raymath.MatrixLookAt(lightPos, center, Vector3.UnitY)

    let mutable minX, minY, minZ =
      System.Single.MaxValue, System.Single.MaxValue, System.Single.MaxValue

    let mutable maxX, maxY, maxZ =
      System.Single.MinValue, System.Single.MinValue, System.Single.MinValue

    for corner in frustumCorners do
      let p = Raymath.Vector3Transform(corner, lightView)
      minX <- min minX p.X
      maxX <- max maxX p.X
      minY <- min minY p.Y
      maxY <- max maxY p.Y
      minZ <- min minZ p.Z
      maxZ <- max maxZ p.Z

    // Snap to texel grid to reduce shadow shimmering
    let texelSizeX = (maxX - minX) / float32 shadowMapSize
    let texelSizeY = (maxY - minY) / float32 shadowMapSize

    minX <- floor(minX / texelSizeX) * texelSizeX
    maxX <- floor(maxX / texelSizeX) * texelSizeX
    minY <- floor(minY / texelSizeY) * texelSizeY
    maxY <- floor(maxY / texelSizeY) * texelSizeY

    // Add small padding to Z to capture geometry outside frustum
    let zMult = 10.0f

    if minZ < 0.0f then
      minZ <- minZ * zMult
    else
      minZ <- minZ / zMult

    if maxZ < 0.0f then
      maxZ <- maxZ / zMult
    else
      maxZ <- maxZ * zMult

    let lightProj =
      Raymath.MatrixOrtho(float minX, float maxX, float minY, float maxY, float minZ, float maxZ)

    Raymath.MatrixMultiply(lightProj, lightView)

  /// <summary>
  /// Computes CSM cascade split distances (view-space depth).
  /// Uses practical split scheme: split = near * (far/near)^(i/count)
  /// with a linear/practical blend.
  /// </summary>
  let computeCascadeSplits
    (near: float32)
    (far: float32)
    (cascadeCount: int)
    : float32[] =

    let splits = Array.zeroCreate<float32>(cascadeCount - 1)
    let ratio = far / near

    for i = 1 to cascadeCount - 1 do
      let p = float32 i / float32 cascadeCount
      let logSplit = near * (ratio ** p)
      let uniSplit = near + (far - near) * p
      splits[i - 1] <- logSplit * 0.9f + uniSplit * 0.1f

    splits
