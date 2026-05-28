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
  /// Creates a depth-only shadow FBO, matching the C example's LoadShadowmapRenderTexture.
  /// Depth texture is attached as TEXTURE2D (not renderbuffer) so it can be sampled in shaders.
  /// </summary>
  let createShadowFbo (width: int) (height: int) : RenderTexture2D =
    let fboId = Rlgl.LoadFramebuffer()
    Rlgl.EnableFramebuffer(fboId)
    let depthId = Rlgl.LoadTextureDepth(width, height, false)
    Rlgl.FramebufferAttach(fboId, depthId, FramebufferAttachType.Depth, FramebufferAttachTextureType.Texture2D, 0)
    Rlgl.DisableFramebuffer()
    RenderTexture2D(
      Id = fboId,
      Texture = Texture2D(Id = 0u, Width = width, Height = height, Mipmaps = 1, Format = PixelFormat.UncompressedR8G8B8A8),
      Depth = Texture2D(Id = depthId, Width = width, Height = height, Mipmaps = 1, Format = enum<PixelFormat> 19)
    )

  /// <summary>
  /// Destroys a custom shadow FBO and its attachments.
  /// </summary>
  let destroyShadowFbo (rt: RenderTexture2D) =
    Rlgl.UnloadTexture(rt.Depth.Id)
    Rlgl.UnloadFramebuffer(rt.Id)
