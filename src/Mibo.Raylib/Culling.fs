namespace Mibo.Elmish

open System.Numerics
open Raylib_cs

/// <summary>
/// Describes the relationship between a frustum and a bounded volume.
/// </summary>
type ContainmentType =
  | Disjoint = 0
  | Intersects = 1
  | Contains = 2

/// <summary>
/// Represents a bounding sphere in 3D space.
/// </summary>
[<Struct>]
type BoundingSphere = { Center: Vector3; Radius: float32 }

/// <summary>
/// Represents a view frustum defined by six planes extracted from a View*Projection matrix.
/// </summary>
/// <remarks>
/// Each plane is stored as a <c>Vector4</c> where (x, y, z) is the normal and w is the distance
/// from the origin along the normal direction.
/// </remarks>
type Frustum(viewProjection: Matrix4x4) =

  // Gribb/Hartmann plane extraction for row-major matrices
  // Each plane: (normal.x, normal.y, normal.z, d) where d is the signed distance
  let normalizePlane(p: Vector4) =
    let len = float32(sqrt(double(p.X * p.X + p.Y * p.Y + p.Z * p.Z)))

    if len > 0.0001f then
      Vector4(p.X / len, p.Y / len, p.Z / len, p.W / len)
    else
      p

  let planes = [|
    // Left:   row4 + row1
    normalizePlane(
      Vector4(
        viewProjection.M41 + viewProjection.M11,
        viewProjection.M42 + viewProjection.M12,
        viewProjection.M43 + viewProjection.M13,
        viewProjection.M44 + viewProjection.M14
      )
    )
    // Right:  row4 - row1
    normalizePlane(
      Vector4(
        viewProjection.M41 - viewProjection.M11,
        viewProjection.M42 - viewProjection.M12,
        viewProjection.M43 - viewProjection.M13,
        viewProjection.M44 - viewProjection.M14
      )
    )
    // Bottom: row4 + row2
    normalizePlane(
      Vector4(
        viewProjection.M41 + viewProjection.M21,
        viewProjection.M42 + viewProjection.M22,
        viewProjection.M43 + viewProjection.M23,
        viewProjection.M44 + viewProjection.M24
      )
    )
    // Top:    row4 - row2
    normalizePlane(
      Vector4(
        viewProjection.M41 - viewProjection.M21,
        viewProjection.M42 - viewProjection.M22,
        viewProjection.M43 - viewProjection.M23,
        viewProjection.M44 - viewProjection.M24
      )
    )
    // Near:   row4 + row3
    normalizePlane(
      Vector4(
        viewProjection.M41 + viewProjection.M31,
        viewProjection.M42 + viewProjection.M32,
        viewProjection.M43 + viewProjection.M33,
        viewProjection.M44 + viewProjection.M34
      )
    )
    // Far:    row4 - row3
    normalizePlane(
      Vector4(
        viewProjection.M41 - viewProjection.M31,
        viewProjection.M42 - viewProjection.M32,
        viewProjection.M43 - viewProjection.M33,
        viewProjection.M44 - viewProjection.M34
      )
    )
  |]

  // Signed distance from point to plane: dot(normal.xyz, point) + d
  let dot4 (p: Vector4) (v: Vector3) = p.X * v.X + p.Y * v.Y + p.Z * v.Z + p.W

  /// <summary>Tests whether the given <see cref="T:Mibo.Elmish.BoundingSphere"/> is contained within the frustum.</summary>
  member _.Contains(sphere: BoundingSphere) =
    let mutable result = ContainmentType.Contains

    for p in planes do
      let dist = dot4 p sphere.Center

      if dist < -sphere.Radius then
        result <- ContainmentType.Disjoint
      elif abs dist < sphere.Radius then
        result <- ContainmentType.Intersects

    result

  /// <summary>Tests whether the given <see cref="T:Raylib_cs.BoundingBox"/> is contained within the frustum.</summary>
  member _.Contains(box: BoundingBox) =
    let mutable result = ContainmentType.Contains

    for p in planes do
      // Positive vertex (furthest along plane normal)
      let pvx = if p.X >= 0.0f then box.Max.X else box.Min.X
      let pvy = if p.Y >= 0.0f then box.Max.Y else box.Min.Y
      let pvz = if p.Z >= 0.0f then box.Max.Z else box.Min.Z
      let pVertex = Vector3(pvx, pvy, pvz)

      // Negative vertex (nearest along plane normal)
      let nvx = if p.X >= 0.0f then box.Min.X else box.Max.X
      let nvy = if p.Y >= 0.0f then box.Min.Y else box.Max.Y
      let nvz = if p.Z >= 0.0f then box.Min.Z else box.Max.Z
      let nVertex = Vector3(nvx, nvy, nvz)

      if dot4 p nVertex > 0.0f then
        // n-vertex is in front of the plane -> box is completely outside
        result <- ContainmentType.Disjoint
      elif dot4 p pVertex >= 0.0f then
        // p-vertex is in front -> box intersects this plane
        result <- ContainmentType.Intersects

    result


/// <summary>
/// Generic helper functions for visibility culling.
/// </summary>
/// <remarks>
/// These helpers operate on the custom frustum/primitives to separate
/// spatial partitioning logic from rendering logic.
/// </remarks>
module Culling =

  /// <summary>Checks if a bounding sphere is within the view frustum.</summary>
  /// <remarks>Returns true if fully inside or intersecting (partially visible).</remarks>
  let inline isVisible (frustum: Frustum) (sphere: BoundingSphere) =
    let containment = frustum.Contains(sphere)
    containment <> ContainmentType.Disjoint

  /// <summary>Checks if a bounding box is within the view frustum.</summary>
  /// <remarks>Returns true if fully inside or intersecting (partially visible). Useful for culling axis-aligned geometry or spatial partition nodes.</remarks>
  let inline isGenericVisible (frustum: Frustum) (box: BoundingBox) =
    let containment = frustum.Contains(box)
    containment <> ContainmentType.Disjoint

  /// <summary>Checks if a 2D rectangle intersects with the visible camera bounds.</summary>
  /// <remarks>Use with <see cref="M:Mibo.Elmish.Camera2D.viewportBounds"/> to get the view bounds.</remarks>
  /// <example>
  /// <code>
  /// let viewBounds = Camera2D.viewportBounds camera width height
  /// if Culling.isVisible2D viewBounds sprite.Bounds then
  ///     // Render sprite
  /// </code>
  /// </example>
  let inline isVisible2D
    (viewBounds: Raylib_cs.Rectangle)
    (itemBounds: Raylib_cs.Rectangle)
    =
    viewBounds.X < itemBounds.X + itemBounds.Width
    && viewBounds.X + viewBounds.Width > itemBounds.X
    && viewBounds.Y < itemBounds.Y + itemBounds.Height
    && viewBounds.Y + viewBounds.Height > itemBounds.Y
