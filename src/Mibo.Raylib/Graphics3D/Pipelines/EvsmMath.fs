#nowarn "9"

namespace Mibo.Elmish.Graphics3D.Pipelines

open System.Numerics
open FSharp.NativeInterop
open Raylib_cs

/// <summary>
/// Helper functions for Exponential Variance Shadow Map (EVSM) math.
/// </summary>
module EvsmMath =

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

  // ------------------------------------------------------------------
  // EVSM Shadow Map FBO
  // ------------------------------------------------------------------

  /// <summary>
  /// Creates a custom FBO-backed render target with an RGBA32F color attachment
  /// and a depth renderbuffer for EVSM moments storage.
  /// Uses Rlgl directly because raylib's LoadRenderTexture only creates RGBA8.
  /// </summary>
  let createShadowFbo (width: int) (height: int) : RenderTexture2D =
    let fboId = Rlgl.LoadFramebuffer()
    Rlgl.EnableFramebuffer(fboId)

    let colorTexId =
      let data = NativePtr.ofNativeInt<byte> 0n |> NativePtr.toVoidPtr
      Rlgl.LoadTexture(data, width, height, PixelFormat.UncompressedR32G32B32A32, 1)

    Rlgl.TextureParameters(colorTexId, Rlgl.TEXTURE_MIN_FILTER, Rlgl.TEXTURE_FILTER_LINEAR)
    Rlgl.TextureParameters(colorTexId, Rlgl.TEXTURE_MAG_FILTER, Rlgl.TEXTURE_FILTER_LINEAR)
    Rlgl.TextureParameters(colorTexId, Rlgl.TEXTURE_WRAP_S, Rlgl.TEXTURE_WRAP_CLAMP)
    Rlgl.TextureParameters(colorTexId, Rlgl.TEXTURE_WRAP_T, Rlgl.TEXTURE_WRAP_CLAMP)

    Rlgl.FramebufferAttach(
      fboId, colorTexId,
      FramebufferAttachType.ColorChannel0, FramebufferAttachTextureType.Texture2D, 0
    )

    let depthId = Rlgl.LoadTextureDepth(width, height, true)

    Rlgl.FramebufferAttach(
      fboId, depthId,
      FramebufferAttachType.Depth, FramebufferAttachTextureType.Renderbuffer, 0
    )

    Rlgl.FramebufferComplete(fboId) |> ignore
    Rlgl.DisableFramebuffer()

    RenderTexture2D(
      Id = fboId,
      Texture = Texture2D(Id = colorTexId, Width = width, Height = height, Mipmaps = 1, Format = PixelFormat.UncompressedR32G32B32A32),
      Depth = Texture2D(Id = depthId, Width = width, Height = height, Mipmaps = 1, Format = PixelFormat.UncompressedR32G32B32A32)
    )

  /// <summary>
  /// Destroys a custom shadow FBO and its attachments.
  /// </summary>
  let destroyShadowFbo (rt: RenderTexture2D) =
    Rlgl.UnloadFramebuffer(rt.Id)
    Rlgl.UnloadTexture(rt.Texture.Id)
    Rlgl.UnloadTexture(rt.Depth.Id)
