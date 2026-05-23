namespace Mibo.Elmish

open System
open System.Numerics
open Raylib_cs

/// <summary>
/// A universal Camera definition containing View and Projection matrices.
/// </summary>
/// <remarks>
/// This struct is renderer-agnostic - both 2D and 3D renderers use the same type.
/// Use the <see cref="T:Mibo.Elmish.Camera2D"/> or <see cref="T:Mibo.Elmish.Camera3D"/> modules to create cameras.
/// </remarks>
/// <example>
/// <code>
/// // 2D camera centered on player
/// let camera = Camera2D.create playerPos 1.0f viewportSize
///
/// // 3D camera looking at origin
/// let camera = Camera3D.lookAt position Vector3.Zero Vector3.Up fov aspect 0.1f 1000f
/// </code>
/// </example>
type Camera = {
  /// The view matrix (camera position/rotation, transforms world to view space).
  View: Matrix4x4
  /// The projection matrix (perspective/orthographic, transforms view to clip space).
  Projection: Matrix4x4
}

/// <summary>
/// Represents a 3D ray with an origin position and normalized direction.
/// </summary>
[<Struct>]
type Ray = {
  Position: Vector3
  Direction: Vector3
}

/// <summary>
/// Helper functions for 2D Cameras (Orthographic projection).
/// </summary>
/// <remarks>
/// Use these for top-down, side-scrolling, or any 2D game rendering.
/// </remarks>
module Camera2D =

  /// <summary>Calculates the visible world bounds for a raylib Camera2D.</summary>
  let viewportBounds
    (camera: Raylib_cs.Camera2D)
    (width: float32)
    (height: float32)
    : Raylib_cs.Rectangle =
    let visibleW = width / camera.Zoom
    let visibleH = height / camera.Zoom
    let halfW = visibleW * 0.5f
    let halfH = visibleH * 0.5f

    Raylib_cs.Rectangle(
      camera.Target.X - halfW,
      camera.Target.Y - halfH,
      visibleW,
      visibleH
    )

  /// <summary>
  /// Creates a raylib <c>Camera2D</c> centered on the given position.
  /// </summary>
  let create
    (position: Vector2)
    (zoom: float32)
    (viewportSize: Vector2)
    : Raylib_cs.Camera2D =
    Raylib_cs.Camera2D(
      Vector2(viewportSize.X * 0.5f, viewportSize.Y * 0.5f),
      position,
      0.0f,
      zoom
    )

  /// <summary>Converts a screen position (pixels) to world position.</summary>
  let screenToWorld
    (camera: Raylib_cs.Camera2D)
    (screenPos: Vector2)
    : Vector2 =
    Raylib.GetScreenToWorld2D(screenPos, camera)

  /// <summary>Converts a world position to screen position (pixels).</summary>
  let worldToScreen (camera: Raylib_cs.Camera2D) (worldPos: Vector2) : Vector2 =
    Raylib.GetWorldToScreen2D(worldPos, camera)

  /// <summary>
  /// Smoothly interpolate the camera target toward a world position.
  /// </summary>
  /// <param name="camera">Passed by reference so mutations are visible to the caller.</param>
  let inline smoothFollow
    (camera: byref<Raylib_cs.Camera2D>)
    (target: Vector2)
    (speed: float32)
    =
    camera.Target.X <- camera.Target.X + (target.X - camera.Target.X) * speed
    camera.Target.Y <- camera.Target.Y + (target.Y - camera.Target.Y) * speed

  /// <summary>
  /// Clamp the camera target to a world bounds rectangle.
  /// </summary>
  /// <param name="camera">Passed by reference so mutations are visible to the caller.</param>
  let inline clampTarget
    (camera: byref<Raylib_cs.Camera2D>)
    (minX: float32)
    (minY: float32)
    (maxX: float32)
    (maxY: float32)
    =
    camera.Target.X <- MathF.Max(minX, MathF.Min(camera.Target.X, maxX))
    camera.Target.Y <- MathF.Max(minY, MathF.Min(camera.Target.Y, maxY))


/// <summary>
/// Helper functions for 3D Cameras (Perspective projection).
/// </summary>
/// <remarks>
/// Use these for first-person, third-person, or any 3D game rendering.
/// </remarks>
module Camera3D =

  /// <summary>
  /// Creates a camera that looks at a target from a position.
  /// </summary>
  /// <param name="position">Camera position in world space</param>
  /// <param name="target">Point the camera is looking at</param>
  /// <param name="up">Up vector (typically Vector3.UnitY)</param>
  /// <param name="fov">Field of view in radians (e.g., MathF.PI / 4.0f)</param>
  /// <param name="aspectRatio">Width / Height of the viewport</param>
  /// <param name="nearPlane">Near clipping distance (objects closer are not rendered)</param>
  /// <param name="farPlane">Far clipping distance (objects farther are not rendered)</param>
  /// <example>
  /// <code>
  /// let camera = Camera3D.lookAt
  ///     (Vector3(0f, 10f, 20f))  // position
  ///     Vector3.Zero              // target
  ///     Vector3.Up                // up
  ///     (MathF.PI / 4.0f)        // 45° FOV
  ///     (16f / 9f)                // aspect ratio
  ///     0.1f                      // near plane
  ///     1000f                     // far plane
  /// </code>
  /// </example>
  let lookAt
    (position: Vector3)
    (target: Vector3)
    (up: Vector3)
    (fov: float32)
    (aspectRatio: float32)
    (nearPlane: float32)
    (farPlane: float32)
    : Camera =
    {
      View = Matrix4x4.CreateLookAt(position, target, up)
      Projection =
        Matrix4x4.CreatePerspectiveFieldOfView(
          fov,
          aspectRatio,
          nearPlane,
          farPlane
        )
    }

  /// <summary>
  /// Creates an orbiting camera using spherical coordinates.
  /// </summary>
  /// <remarks>
  /// Useful for third-person cameras, inspection views, or editor cameras.
  /// </remarks>
  /// <param name="target">Point the camera orbits around</param>
  /// <param name="yaw">Horizontal rotation angle in radians</param>
  /// <param name="pitch">Vertical rotation angle in radians</param>
  /// <param name="radius">Distance from target</param>
  /// <param name="fov">Field of view in radians</param>
  /// <param name="aspect">Aspect ratio</param>
  /// <param name="near">Near plane</param>
  /// <param name="far">Far plane</param>
  let orbit
    (target: Vector3)
    (yaw: float32)
    (pitch: float32)
    (radius: float32)
    (fov: float32)
    (aspect: float32)
    (near: float32)
    (far: float32)
    : Camera =
    let position =
      Vector3(
        radius * sin(yaw) * cos(pitch),
        radius * sin(pitch),
        radius * cos(yaw) * cos(pitch)
      )
      + target

    lookAt position target Vector3.UnitY fov aspect near far

  /// <summary>
  /// Creates a ray from screen coordinates for mouse/touch picking.
  /// </summary>
  /// <remarks>
  /// The ray originates at the camera's near plane at the screen position
  /// and points into the scene.
  /// </remarks>
  /// <param name="camera">The camera to compute the ray for.</param>
  /// <param name="screenPos">The screen position in pixels.</param>
  /// <param name="viewportWidth">Viewport width in pixels.</param>
  /// <param name="viewportHeight">Viewport height in pixels.</param>
  let screenPointToRay
    (camera: Camera)
    (screenPos: Vector2)
    (viewportWidth: float32)
    (viewportHeight: float32)
    : Ray =
    let mutable invertedViewProj = Matrix4x4()

    Matrix4x4.Invert(camera.View * camera.Projection, &invertedViewProj)
    |> ignore

    let nx = (2.0f * screenPos.X / viewportWidth) - 1.0f
    let ny = 1.0f - (2.0f * screenPos.Y / viewportHeight)

    let nearClip = Vector4(nx, ny, 0.0f, 1.0f)
    let farClip = Vector4(nx, ny, 1.0f, 1.0f)

    let nearWorld = Vector4.Transform(nearClip, invertedViewProj)
    let farWorld = Vector4.Transform(farClip, invertedViewProj)

    let nearPos = Vector3(nearWorld.X, nearWorld.Y, nearWorld.Z) / nearWorld.W
    let farPos = Vector3(farWorld.X, farWorld.Y, farWorld.Z) / farWorld.W

    let direction = Vector3.Normalize(farPos - nearPos)

    {
      Position = nearPos
      Direction = direction
    }
